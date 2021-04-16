using System;
using System.IO;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Globalization;
using System.Text;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using Microsoft.Azure.Services.AppAuthentication;

namespace Company.Function
{
    public static class SQLMonitoring
    {
        [FunctionName("Orchestrator")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            var outputs = new List<string>();
            log.LogInformation(context.GetInput<string>());
            dynamic eventDetails = JsonConvert.DeserializeObject(context.GetInput<string>());
            string operationName = eventDetails.eventData.operationName;
            //log.LogInformation(operationName);
            if(eventDetails.eventData.operationName.ToString().Equals("Microsoft.Sql/servers/databases/auditingSettings/write"))
            {
                //log.LogInformation(context.GetInput<string>());
                outputs.Add(await context.CallActivityAsync<string>("SQLMonitoring",context.GetInput<string>()));
            }

            return outputs;
        }

        [FunctionName("SQLMonitoring")]
        public static async Task<string> SQLMonitoring_Changer([ActivityTrigger] string requestData, ILogger log)
        {
            dynamic eventDetails = JsonConvert.DeserializeObject(requestData);
            //JObject eventObject = JObject.Parse(requestData);
            string resourceURI = eventDetails.eventData.resourceUri;
            string sub_id = resourceURI.Substring(0,resourceURI.IndexOf("/resourceGroups"));
            string resource_group = resourceURI.Substring(resourceURI.IndexOf("/resourceGroups"),(resourceURI.IndexOf("/providers")-resourceURI.IndexOf("/resourceGroups")));
            string server_name = resourceURI.Substring(resourceURI.IndexOf("/servers"),(resourceURI.IndexOf("/databases")-resourceURI.IndexOf("/servers")));
            string database_name = resourceURI.Substring(resourceURI.IndexOf("/databases"),resourceURI.IndexOf("/auditingSettings")-resourceURI.IndexOf("/databases"));
            string central_file = Read_Central_File(log).GetAwaiter().GetResult();
            dynamic file = JObject.Parse(central_file);
            dynamic configs_all = file.configuration;
            string db_tags = Read_DB_Tag(sub_id,resource_group,server_name,database_name,log).GetAwaiter().GetResult();
            //log.LogInformation(db_tags);
            string db_tag_name = db_tags.Substring(2,db_tags.IndexOf(":")-2);
            string db_tag_value = db_tags.Substring(db_tags.IndexOf(":"),db_tags.Length-db_tags.IndexOf(":"));
            //log.LogInformation(database_name);
            String api_url = string.Format("https://management.azure.com{0}{1}/providers/Microsoft.Sql{2}{3}/auditingSettings/default?api-version=2017-03-01-preview",sub_id,resource_group,server_name,database_name);
            string client_id = "559ebaaf-1d27-4022-82bd-3cd5c3f77d3d";
            var azureServiceTokenProvider = new AzureServiceTokenProvider($"RunAs=App;AppId={client_id}");
            string mgmtAccessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://management.azure.com/");
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgmtAccessToken);
            var audit_response = await httpClient.GetStringAsync(api_url);
            dynamic audit_settings = JObject.Parse(audit_response);
            string audit_state = audit_settings.properties.state;
            foreach(dynamic configs in configs_all)
            {
                string tagName = configs.tagName;
                string tagValue = configs.tagValue;
                if(db_tag_name.Contains(tagName) && !db_tag_value.Contains(audit_state))
                {
                    JObject api_body = configs.Body;
                    var content = new StringContent(api_body.ToString(),Encoding.UTF8,"application/json");
                    var put_response = await httpClient.PutAsync(api_url,content);
                    log.LogInformation("Successfully changed the Status");
                }
            }
            return "hello";
        }

        private static async Task<string> Read_Central_File(ILogger log)
        {
            string file_url = "https://rohillistcontainers.blob.core.windows.net/trial/tag_check.json";
            string client_id = "559ebaaf-1d27-4022-82bd-3cd5c3f77d3d";
            var azureServiceTokenProvider = new AzureServiceTokenProvider($"RunAs=App;AppId={client_id}");
            string mgmtAccessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://storage.azure.com/");
            DateTime now = DateTime.UtcNow;
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("x-ms-date", now.ToString("R", CultureInfo.InvariantCulture));
            httpClient.DefaultRequestHeaders.Add("x-ms-version", "2020-04-08");
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgmtAccessToken);
            var response = await httpClient.GetStringAsync(file_url);
            return response;
        }

        private static async Task<string> Read_DB_Tag(string sub_id, string resource_group, string server_name, string database_name, ILogger log)
        {
            //string db_url = "https://management.azure.com/subscriptions/ce577dae-a256-44f3-8058-a8876600fe5c/resourceGroups/rohilpractice/providers/Microsoft.Sql/servers/rohil-practice-sql-server/databases/rohil-sql-database?api-version=2020-08-01-preview";
            String db_url = string.Format("https://management.azure.com{0}{1}/providers/Microsoft.Sql{2}{3}?api-version=2020-08-01-preview",sub_id,resource_group,server_name,database_name);
            //log.LogInformation(db_url);
            string client_id = "559ebaaf-1d27-4022-82bd-3cd5c3f77d3d";
            var azureServiceTokenProvider = new AzureServiceTokenProvider($"RunAs=App;AppId={client_id}");
            string mgmtAccessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://management.azure.com/");
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgmtAccessToken);
            var response = await httpClient.GetStringAsync(db_url);
            JObject response_json = JObject.Parse(response);
            JObject tags = (JObject)response_json["tags"];
            return tags.ToString();
            //return db_url;
        }

        [FunctionName("SQLDBTrigger")]
        public static async Task<IActionResult> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post",Route = null)] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            //set body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync<string>("Orchestrator", requestBody);
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
            return starter.CreateCheckStatusResponse(req, instanceId);
        }


    }
}