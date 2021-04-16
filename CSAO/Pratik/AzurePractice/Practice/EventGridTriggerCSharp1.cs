// Default URL for triggering event grid function in the local environment. 
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;
using Microsoft.Azure.Services.AppAuthentication;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Company.Function
{
    public static class EventGridTriggerCSharp1
    {
        [FunctionName("EventGridTriggerCSharp1")]
        public static void Run([EventGridTrigger]EventGridEvent eventGridEvent, ILogger log)
        {
            
            getContainerProperties(log).GetAwaiter().GetResult();
        }

        private static async Task getContainerProperties(ILogger log)
        {
            //var clientId = "ce780717-4149-4a56-91ec-5e9ff527660a";

            var azureServiceTokenProvider = new AzureServiceTokenProvider($"RunAs=App;AppId=ce780717-4149-4a56-91ec-5e9ff527660a");
            var managementAccessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://management.azure.com/");
            var storageAccessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://storage.azure.com/");
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managementAccessToken);
            //az ad sp create-for-rbac -n "testaccount"
            //string containerUri = "https://management.azure.com/subscriptions/0bf4d827-27f5-4d30-8637-40002aaab9be/resourceGroups/pratikdemoresource/providers/Microsoft.Storage/storageAccounts/pratikdemostorage/blobServices/default/containers?api-version=2019-06-01";


            JObject configJson = readConfigFile(log).GetAwaiter().GetResult();
            List<string> containerList = listContainerProperties(log).Result;
            foreach (var container_name in containerList)
            {
                log.LogInformation(container_name);
                string storageUri = $"https://management.azure.com/subscriptions/0bf4d827-27f5-4d30-8637-40002aaab9be/resourceGroups/pratikdemoresource/providers/Microsoft.Storage/storageAccounts/pratikdemostorage/blobServices/default/containers/{container_name}?api-version=2019-06-01";
                var get_response = await client.GetStringAsync(storageUri);
                JObject getResult = JObject.Parse(get_response);
                string currentAccessvalue = getResult["properties"]["publicAccess"].ToString().ToUpper();
                string updateAccessValue = string.Empty;

                
                if( configJson["enableRemediation"].ToString().ToUpper().Equals("FALSE"))
                break;
                else
                {
                    JObject tag = (JObject)getResult["tags"];
                    string tagName = configJson["tagName"].ToString(); // generalise it
                    string tagValue = configJson["tagValue"].ToString().ToUpper();
                    
                    if ( tag[tagName].ToString().Equals("DEFAULT") )
                    {   
                        updateAccessValue = configJson["containerAccessPolicy"].ToString().ToUpper();
                        if (updateAccessValue == "PUBLIC" || updateAccessValue == "CONTAINER")
                        updateAccessValue = "Container";
                        else if (updateAccessValue == "BLOB")
                        updateAccessValue = "Blob";
                        else updateAccessValue = "None";

                        //patch request
                        IDictionary<string, IDictionary<string,string>> httpcontent = new Dictionary<string, IDictionary<string, string>>();
                        IDictionary<string,string> publicAccess = new Dictionary<string,string>();
    
                        publicAccess.Add("publicAccess",updateAccessValue);
                        httpcontent.Add("properties",publicAccess);

                        var patchBody = JsonConvert.SerializeObject(httpcontent);
                        var content = new StringContent(patchBody);
                        HttpResponseMessage patch_response = await client.PatchAsync(storageUri,content);
                        patch_response.EnsureSuccessStatusCode();
                        //string presult = await patch_response.Content.ReadAsStringAsync();
                        //log.LogInformation(presult);
                    }

                    // else 
                    // {
                    //     if(currentAccessvalue !=  )
                    // }
                }


                //log.LogInformation(value);
            }

        }
        
    
        private static async Task<List<string>> listContainerProperties(ILogger log)
        {
            var azureServiceTokenProvider = new AzureServiceTokenProvider($"RunAs=App;AppId=ce780717-4149-4a56-91ec-5e9ff527660a");
            var managementAccessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://management.azure.com/");
            var storageAccessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://storage.azure.com/");
            string containerUri = "https://management.azure.com/subscriptions/0bf4d827-27f5-4d30-8637-40002aaab9be/resourceGroups/pratikdemoresource/providers/Microsoft.Storage/storageAccounts/pratikdemostorage/blobServices/default/containers?api-version=2019-06-01";
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managementAccessToken);
            
            var get_response = await client.GetStringAsync(containerUri);
            JObject getResult = JObject.Parse(get_response);
            JArray value = (JArray)getResult["value"];
            //log.LogInformation(getResult.ToString());
            
            List<string> containerList = new List<string>();
            foreach(var item in value.Children())
            {
                var itemProperties = item.Children<JProperty>();
                var myElement = itemProperties.FirstOrDefault(x => x.Name == "name");
                containerList.Add(myElement.Value.ToString()); 
            }
            return containerList;
        }

        private static async Task<JObject> readConfigFile(ILogger log)
        {
            string uri = "https://configdemo.blob.core.windows.net/configuration/configuration.json";
            var azureServiceTokenProvider = new AzureServiceTokenProvider($"RunAs=App;AppId=ce780717-4149-4a56-91ec-5e9ff527660a");
            var storageAccessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://storage.azure.com/");
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", storageAccessToken);
            //client.DefaultRequestHeaders.Add("")
            var get_response = await client.GetStringAsync(uri);
            JObject getResult = JObject.Parse(get_response);
            return getResult;
        }







    }
}

