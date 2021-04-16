// NOTE :- Add a central configuration file
using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
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

namespace EventMonitroing
{
    public static class EventSubscription
    {
        [FunctionName("EventSubscription")]
        public static async Task Runasync([EventGridTrigger]EventGridEvent eventGridEvent, ILogger log)
        {

            //log.LogInformation(eventGridEvent.Data.ToString());
            dynamic eventDataObject = JsonConvert.DeserializeObject(eventGridEvent.Data.ToString());
            string serviceType = eventDataObject.resourceProvider.ToString();
            string resourceURI = eventDataObject.resourceUri.ToString();
            string operationName = eventDataObject.operationName.ToString();
            string subscriptionId = eventDataObject.subscriptionId.ToString();

            string msiAppId = System.Environment.GetEnvironmentVariable("AppId", EnvironmentVariableTarget.Process);

            var azureServiceTokenProvider = new AzureServiceTokenProvider($"RunAs=App;AppId={msiAppId}");
            string mgmtAccessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://management.azure.com/");
            string storageAccessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://storage.azure.com/");

            
            JObject eventDetails = new JObject();
            eventDetails["operationName"] = operationName;
            eventDetails["mgmtAccessToken"] = mgmtAccessToken;
            eventDetails["storageAccessToken"] = storageAccessToken;
            eventDetails["eventData"] = eventDataObject;
            eventDetails["subscriptionId"] = subscriptionId;
            eventDetails["resourceGroupName"] = resourceURI;

            log.LogInformation(operationName);


            // if (operationName.ToLower().Equals("Microsoft.Sql/servers/securityAlertPolicies/write"))
            // {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgmtAccessToken);
                
                string url = "https://sqlservermonitoring.azurewebsites.net/api/SqlMonitorTrigger?code=canWZYyFgfDolCLSrB/Wuv3I4p50u5m73H6kCwSlKbxcPo7gHSj4Lg==";
                var content = new StringContent(JsonConvert.SerializeObject(eventDetails), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(url, content);
                string result = await response.Content.ReadAsStringAsync();
                response.EnsureSuccessStatusCode();
                
            // }


            
            

        }

    }
}
