// Default URL for triggering event grid function in the local environment. 
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Http.Headers;     
using System.Net.Http.Formatting;
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
    public static class policyPractice
    {
        [FunctionName("policyPractice")]
        public static void Run([EventGridTrigger]EventGridEvent eventGridEvent, ILogger log)
        {
            //log.LogInformation(eventGridEvent.Data.ToString());
            policyChange(log).GetAwaiter().GetResult();
        }
        private static async Task policyChange(ILogger log)
        {
            var azureServiceTokenProvider = new AzureServiceTokenProvider($"RunAs=App;AppId=ce780717-4149-4a56-91ec-5e9ff527660a");
            var managementAccessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://management.azure.com/");
            var storageAccessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://storage.azure.com/");
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managementAccessToken);
            
            // Valid scopes are:
            //  management group (format: '/providers/Microsoft.Management/managementGroups/{managementGroup}')
            // subscription (format: '/subscriptions/{subscriptionId}')
            // resource group (format: '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}'
            // resource (format: '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceProviderNamespace}/[{parentResourcePath}/]{resourceType}/{resourceName}'.
            //string scope ="subscriptions/0bf4d827-27f5-4d30-8637-40002aaab9be/resourceGroups/pratikdemoresource/"
            //assignmentID = /subscriptions/0bf4d827-27f5-4d30-8637-40002aaab9be/providers/Microsoft.Authorization/policyAssignments/SecurityCenterBuiltIn
            //https://management.azure.com/{scope}/providers/Microsoft.Authorization/policyAssignments/{policyAssignmentName}?api-version=2018-05-01 
            
            var policyUri = "https://management.azure.com/subscriptions/0bf4d827-27f5-4d30-8637-40002aaab9be/providers/Microsoft.Authorization/policyAssignments/SecurityCenterBuiltIn?api-version=2020-09-01";        


            //get request
            var get_response = await client.GetStringAsync(policyUri);
            //get_response.EnsureSuccessStatusCode();
            JObject getResult = JObject.Parse(get_response);
            JObject value = (JObject)getResult["properties"]["parameters"];
            log.LogInformation(getResult.ToString());

            foreach (var prop in value.Properties())
            {
                string parameter = prop.Name ;
                //parameter1
                if (parameter == "storageAccountsShouldUseCustomerManagedKeyForEncryptionMonitoringEffect")
                {
                    if(value[prop.Name]["value"].ToString() == "Disabled")
                    {   
                        JObject temp = (JObject)value[parameter];
                        temp.Property("value").Remove();
                        temp.Add("value","Audit");
                        //log.LogInformation(getResult.ToString());
                    }
                }
                //parameter2
                 if (parameter == "sqlDbEncryptionMonitoringEffect")
                {
                    if(value[prop.Name]["value"].ToString() == "Disabled")
                    {   
                        JObject temp = (JObject)value[parameter];
                        temp.Property("value").Remove();
                        temp.Add("value","AuditIfNotExists");
                        //log.LogInformation(getResult.ToString());
                    }
                }
                
            }

            //put request
            //var putBody = JsonConvert.SerializeObject(getResult);
            var content = new StringContent(getResult.ToString(),Encoding.UTF8,"application/json");
            var putResponse = client.PutAsync(policyUri, content).Result;  
            string presult = await putResponse.Content.ReadAsStringAsync();
            log.LogInformation(presult);    

        }
    }
}

