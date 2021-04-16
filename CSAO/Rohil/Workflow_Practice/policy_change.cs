// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Collections.Generic;
using Microsoft.Azure.Services.AppAuthentication;
using System.Globalization;


namespace Company.Function
{
    public static class policy_change
    {
        [FunctionName("policy_change")]
        public static void Run([EventGridTrigger]JObject eventGridEvent, ILogger log)
        {
            log.LogInformation(eventGridEvent.ToString());
            var operation_name = eventGridEvent["data"]?["operationName"]?.Value<string>();
            var resource_uri = eventGridEvent["data"]?["resourceUri"]?.Value<string>();
            var sub_id = eventGridEvent["data"]?["subscriptionId"]?.Value<string>();
            //HttpClient httpClient = new HttpClient();
            //String url = string.Format("https://rohil-practice.azurewebsites.net/api/function_to_chain?resource_uri={0}&sub_id={1}",resource_uri,sub_id);
            //var chainer = httpClient.GetStringAsync(url);
            //log.LogInformation(chainer.ToString());
            
        }

        private static async Task<string> Read_central_file(ILogger log)
        {
            string central_file_uri = "https://rohillistcontainers.blob.core.windows.net/trial/tag_check.json";
            string client_id = "559ebaaf-1d27-4022-82bd-3cd5c3f77d3d";
            var azureServiceTokenProvider = new AzureServiceTokenProvider($"RunAs=App;AppId={client_id}");
            string mgmtAccessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://storage.azure.com/");
            DateTime now = DateTime.UtcNow;
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("x-ms-date", now.ToString("R", CultureInfo.InvariantCulture));
            httpClient.DefaultRequestHeaders.Add("x-ms-version", "2020-04-08");
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgmtAccessToken);
            var response = await httpClient.GetStringAsync(central_file_uri);
            JObject response_json = JObject.Parse(response);
            var funtion_to_chain = response_json["funtion_to_chain"]?.Value<string>();
            return funtion_to_chain;
        }

        /*private static async Task get_policy_by_id(string resource_uri,ILogger log)
        {
            String get_api_url = string.Format("https://management.azure.com/{0}?api-version=2020-09-01",resource_uri);
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Clear();
            string client_id = "559ebaaf-1d27-4022-82bd-3cd5c3f77d3d";
            var azureServiceTokenProvider = new AzureServiceTokenProvider($"RunAs=App;AppId={client_id}");
            string mgmtAccessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://management.azure.com/");
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgmtAccessToken);
            var response = await httpClient.GetStringAsync(get_api_url);
            JObject response_json = JObject.Parse(response);
            JObject checker = (JObject)response_json["properties"]?["parameters"]?["storageAccountsShouldUseCustomerManagedKeyForEncryptionMonitoringEffect"];
            var value = checker["value"]?.Value<string>();
            if(value.Equals("Disabled"))
            {
                checker.Property("value").Remove();
                checker.Add("value","Audit");
                var Content = new StringContent(response_json.ToString(),Encoding.UTF8, "application/json");
                var put_response = await httpClient.PutAsync(get_api_url,Content);
                put_response.EnsureSuccessStatusCode();
                //log.LogInformation(put_response.ToString());
            }
        }*/

    }
}
