// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Azure.Services.AppAuthentication;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;


namespace Company.Function
{
    public static class EventGridTriggerCSharp1
    {
        [FunctionName("EventGridTriggerCSharp1")]
        public static async void Run([EventGridTrigger]EventGridEvent eventGridEvent, ILogger log)
        {
            log.LogInformation(eventGridEvent.Data.ToString()); 
            string URL = "https://management.azure.com/subscriptions/b875964f-a4e7-48d6-b8f5-0296a0206087/resourceGroups/remedation/providers/Microsoft.Storage/storageAccounts/remedation/blobServices/default/containers/test?api-version=2019-06-01";
            string client_id = "6556585f-9f80-45a6-b902-274ddc762a4f";
            var azureServiceTokenProvider = new AzureServiceTokenProvider($"RunAs=App; AppId={client_id}");
            var managementAccessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://management.azure.com/");
            var storageAccessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://storage.azure.com/");
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managementAccessToken);
            var respone = await client.GetStringAsync(URL);
            JObject responseJson = JObject.Parse(respone);
            JToken token = responseJson["publicAccess"];
            if (token.ToString() !="None")
            {
                IDictionary<string, IDictionary<string, string>> reqBody = new Dictionary<string, IDictionary<string, string>>();
                IDictionary<string, string> subbody = new Dictionary<string, string>();
                subbody.Add("publicAccess","None");
                reqBody.Add("properties",subbody);
                string json = JsonConvert.SerializeObject(reqBody);
                var content = new StringContent(json);
                var patchResponse = await client.PatchAsync(URL,content); 
            }   
        }
    }
}
