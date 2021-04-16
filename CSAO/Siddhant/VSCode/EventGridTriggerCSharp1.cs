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
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Company.Function
{
    public static class EventGridTriggerCSharp1
    {
        [FunctionName("EventGridTriggerCSharp1")]
        public static async void Run([EventGridTrigger] EventGridEvent eventGridEvent, ILogger log)
        {
            log.LogInformation(eventGridEvent.Data.ToString());
            string get_req="https://management.azure.com/subscriptions/74005fdc-162d-410f-8658-cc20356cd779/resourceGroups/testfunctiondemo2/providers/Microsoft.Storage/storageAccounts/testfunctiondemo2/blobServices/default/containers/democontainer?api-version=2019-06-01";
            string patch_req="https://management.azure.com/subscriptions/74005fdc-162d-410f-8658-cc20356cd779/resourceGroups/testfunctiondemo2/providers/Microsoft.Storage/storageAccounts/testfunctiondemo2/blobServices/default/containers/democontainer?api-version=2019-06-01";
            //string file_url = "https://testfunctiondemo2.blob.core.windows.net/democontainer";
            string client_id = "eba80521-5550-4166-b564-e955df61e286";
            var azureServiceTokenProvider = new AzureServiceTokenProvider($"RunAs=App;AppId={client_id}");
            var managementAccessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://management.azure.com/");
            var storageAccessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://storage.azure.com/");
            //log.LogInformation("Reached this point");
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managementAccessToken);
            log.LogInformation(managementAccessToken.ToString());
            var response = await client.GetStringAsync(get_req);
            log.LogInformation("!!!!");
            //response.EnsureSuccessStatusCode();
            log.LogInformation(response.ToString());
            //log.LogInformation("Reached point again");
            JObject Jresponse = JObject.Parse(response);
            JToken token = Jresponse["properties"]["publicAccess"];
            if (token.ToString() != "None")
            {
                IDictionary<string, IDictionary<string, string>> reqBody = new Dictionary<string, IDictionary<string, string>>();
                IDictionary<string, string> subbody = new Dictionary<string, string>();
                subbody.Add("publicAccess", "None");
                reqBody.Add("properties", subbody);
                string json = JsonConvert.SerializeObject(reqBody, Formatting.Indented);
                var content = new StringContent(json);
                var patchResponse = await client.PatchAsync(patch_req, content);
                patchResponse.EnsureSuccessStatusCode();

            }

        }
    }
}

