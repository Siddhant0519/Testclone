// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Company.Function
{
    public static class EventSubscription
    {
        [FunctionName("EventSubscription")]
        public static async Task Run([EventGridTrigger]JObject eventGridEvent, ILogger log)
        {
            //log.LogInformation(eventGridEvent.Data.ToString());
            var operation_name = eventGridEvent["data"]?["operationName"]?.Value<string>();
            if(operation_name.Equals("Microsoft.Sql/servers/databases/auditingSettings/write"))
            {
                log.LogInformation(eventGridEvent["data"]?["resourceUri"]?.Value<string>());
                using (var httpclient = new HttpClient())
                {
                    string url = "https://rohil-deloitte-laptop.azurewebsites.net/api/SQLMonitoring_HttpStart";
                    var content = new StringContent(JsonConvert.SerializeObject(eventGridEvent),Encoding.UTF8,"application/json");
                    HttpResponseMessage response = await httpclient.PostAsync(url,content);
                    
                            string result = await response.Content.ReadAsStringAsync();
                            response.EnsureSuccessStatusCode();
                        
                }
            }
        }
    }
}
