using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json.Linq;
using Microsoft.Azure.Services.AppAuthentication;
using System.Globalization;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Web;

namespace Company.Function
{
    public static class function_to_chain
    {
        [FunctionName("function_to_chain")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,[DurableClient] IDurableOrchestrationClient starter,ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            string resource_uri = req.Query["resource_uri"];
            string sub_id = req.Query["sub_id"];
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            /*name = name ?? data?.name;
            string responseMessage = string.IsNullOrEmpty(name)
                ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, {name}. This HTTP triggered function executed successfully.";*/
            //get_policy_by_id(resource_uri,log).GetAwaiter().GetResult();
            IDictionary<string,string> orchestration_data = new Dictionary<string,string>();
            orchestration_data.Add("resource_uri",resource_uri);
            //orchestration_data.Add("sub_id",sub_id);
            //string json = JsonConvert.SerializeObject(orchestration_data, Formatting.Indented);
            string instance_id =  await starter.StartNewAsync<string>("orchestration_trial",resource_uri);
            return new OkObjectResult("resource_uri");
        }

        [FunctionName("orchestration_trial")]
        public static async Task<List<string>> RunOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext context,ILogger log)
        {
            var outputs = new List<string>();
            var data = context.GetInput<string>();
            if(data.Contains("Microsoft.Sql"))
            {
                outputs.Add(await context.CallActivityAsync<string>("change_automatic_tuning",data));
                //log.LogInformation(outputs[0]);
                //outputs.Add(await context.CallActivityAsync<string>("main_logger",outputs[0]));
            }
            else if(data.Contains("Microsoft.Storage"))
            {
                outputs.Add(await context.CallActivityAsync<string>("change_storage_access",data));
            }
            return outputs;
        }

        [FunctionName("change_automatic_tuning")]
        public static async Task<string> change_automatic_tuning([ActivityTrigger] IDurableActivityContext context,ILogger log)
        {
            string resource_uri = context.GetInput<string>();
            //log.LogInformation(data);
            var server_name = resource_uri.Substring(resource_uri.IndexOf("servers")+8,(resource_uri.IndexOf("databases")-resource_uri.IndexOf("servers"))-9);
            var resource_grop_name = resource_uri.Substring(resource_uri.IndexOf("resourceGroups")+15,(resource_uri.IndexOf("providers")-resource_uri.IndexOf("resourceGroups"))-16);
            var database_name = resource_uri.Substring(resource_uri.IndexOf("databases")+10,(resource_uri.IndexOf("automaticTuning")-resource_uri.IndexOf("databases"))-11);
            var sub_id = resource_uri.Substring(resource_uri.IndexOf("subscriptions")+14,(resource_uri.IndexOf("resourceGroups")-resource_uri.IndexOf("subscriptions"))-15);
            //String api_url = string.Format(Read_central_file("api_call").GetAwaiter().GetResult(),sub_id,resource_grop_name,server_name,database_name);
            string client_id = "559ebaaf-1d27-4022-82bd-3cd5c3f77d3d";
            var azureServiceTokenProvider = new AzureServiceTokenProvider($"RunAs=App;AppId={client_id}");
            string mgmtAccessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://management.azure.com/");
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgmtAccessToken);
            //string properties = Read_central_file("properties").GetAwaiter().GetResult();
            //log.LogInformation(properties);
            var properties = @"{'properties': {
    'desiredState': 'Inherit',
    'options': {
      'createIndex': {
        'desiredState': 'Default'
      },
      'dropIndex': {
        'desiredState': 'Default'
      },
      'forceLastGoodPlan': {
        'desiredState': 'Default'
      }
    }
  }}";
            //string json = JsonConvert.SerializeObject(properties, Formatting.Indented);
            var httpContent = new StringContent(properties,Encoding.UTF8, "application/json");
            //var patch_response = await httpClient.PatchAsync(api_url,httpContent);
            //patch_response.EnsureSuccessStatusCode();
            var log_info = "RESOURCE INFO : "+resource_uri;
            //log.LogInformation(patch_response.ToString());
            return log_info;
        }

        [FunctionName("change_storage_access")]
        public static async Task<string> change_storage_access([ActivityTrigger] IDurableActivityContext context,ILogger log)
        {
           var config_file = Read_central_file().GetAwaiter().GetResult();
           //log.LogInformation(config_file);
           JObject config_file_json = JObject.Parse(config_file);
           JArray configs = (JArray)config_file_json["configuration"];
           foreach(JObject obj in configs.Children<JObject>())
           {
               var tagname = obj["tagName"]?.Value<string>();
               var tag_container = ReadBlob_tagname(log).GetAwaiter().GetResult();
               //log.LogInformation(tagname.ToString()+"\n");
               if(tagname.Contains("timeBasedException-publicAccess"))
                {
                    DateTime now = DateTime.UtcNow;
                    var starttime = obj["starttime"]?.Value<string>();
                    var endtime = obj["endtime"]?.Value<string>();
                    var id = obj["ID"]?.Value<string>();
                    if (now.CompareTo(Convert.ToDateTime(starttime)) > 0 & now.CompareTo(Convert.ToDateTime(endtime)) < 0)
                    {
                        //log.LogInformation("Yeh sahi hai");
                        string uri = context.GetInput<string>();
                        string content = starttime + " " + endtime + " " + id + " " + uri;
                        blob_maker(content).GetAwaiter().GetResult();
                    }
                }

                else if(tagname.Contains("publicAccess"))
                {
                    string uri = context.GetInput<string>();
                    var resource_group = uri.Substring(uri.IndexOf("resourceGroups") + 15, (uri.IndexOf("providers") - uri.IndexOf("resourceGroups")) - 16);
                    var account_name = uri.Substring(uri.IndexOf("storageAccounts") + 16, (uri.IndexOf("blobServices") - uri.IndexOf("storageAccounts")) - 17);
                    var container_name_temp = uri.Substring(uri.IndexOf("default/containers") + 19, (uri.Length - uri.IndexOf("default/containers")) - 19);
                    var container_name = container_name_temp.Trim('"');
                    var sub_id = uri.Substring(uri.IndexOf("subscriptions") + 14, (uri.IndexOf("resourceGroups") - uri.IndexOf("subscriptions")) - 15);

                    String api_url = string.Format("https://management.azure.com/subscriptions/{0}/resourceGroups/{1}/providers/Microsoft.Storage/storageAccounts/{2}/blobServices/default/containers/{3}?api-version=2019-06-01", sub_id, resource_group, account_name, container_name);

                    HttpClient httpClient = new HttpClient();
                    httpClient.DefaultRequestHeaders.Clear();
                    string client_id = "559ebaaf-1d27-4022-82bd-3cd5c3f77d3d";
                    var azureServiceTokenProvider = new AzureServiceTokenProvider($"RunAs=App;AppId={client_id}");
                    string mgmtAccessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://management.azure.com/");
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgmtAccessToken);

                    IDictionary<string, IDictionary<string, string>> patch_body = new Dictionary<string, IDictionary<string, string>>();
                    IDictionary<string, string> public_access = new Dictionary<string, string>();
                    public_access.Add("publicAccess", obj["containerAccessPolicy"]?.Value<string>());
                    patch_body.Add("properties", public_access);
                    string json = JsonConvert.SerializeObject(patch_body, Formatting.Indented);
                    var httpContent = new StringContent(json);
                    var patch_response = await httpClient.PatchAsync(api_url, httpContent);
                    patch_response.EnsureSuccessStatusCode();
                }
           }
           /*if(tagname.Contains("timeBasedException-publicAccess"))
           {
                DateTime now = DateTime.UtcNow;
               if(now.CompareTo(Convert.ToDateTime(Read_central_file("starttime").GetAwaiter().GetResult())) >0 & now.CompareTo(Convert.ToDateTime(Read_central_file("endtime").GetAwaiter().GetResult()))<0)
               {
                   //log.LogInformation("Yeh sahi hai");
                   string starttime = Read_central_file("starttime").GetAwaiter().GetResult();
                   string endtime = Read_central_file("endtime").GetAwaiter().GetResult();
                   string id = Read_central_file("ID").GetAwaiter().GetResult();
                   string uri = context.GetInput<string>();
                   string content = starttime+" "+endtime+" "+id+" "+uri;
                   blob_maker(content).GetAwaiter().GetResult();
               }
           }
           //log.LogInformation(config.ToString());
            foreach (JObject tagname in config)
            {
                log.LogInformation(tagname.ToString()+"\n");
            }
            string uri = context.GetInput<string>();
            var resource_group = uri.Substring(uri.IndexOf("resourceGroups")+15,(uri.IndexOf("providers")-uri.IndexOf("resourceGroups"))-16);
            var account_name = uri.Substring(uri.IndexOf("storageAccounts")+16,(uri.IndexOf("blobServices")-uri.IndexOf("storageAccounts"))-17);
            var container_name_temp = uri.Substring(uri.IndexOf("default/containers")+19,(uri.Length-uri.IndexOf("default/containers"))-19);
            var container_name = container_name_temp.Trim('"');
            var sub_id = uri.Substring(uri.IndexOf("subscriptions")+14,(uri.IndexOf("resourceGroups")-uri.IndexOf("subscriptions"))-15);

            String api_url = string.Format("https://management.azure.com/subscriptions/{0}/resourceGroups/{1}/providers/Microsoft.Storage/storageAccounts/{2}/blobServices/default/containers/{3}?api-version=2019-06-01",sub_id,resource_group,account_name,container_name);
            
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Clear();
            string client_id = "559ebaaf-1d27-4022-82bd-3cd5c3f77d3d";
            var azureServiceTokenProvider = new AzureServiceTokenProvider($"RunAs=App;AppId={client_id}");
            string mgmtAccessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://management.azure.com/");
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgmtAccessToken);
            
            IDictionary<string, IDictionary<string, string>> patch_body = new Dictionary<string, IDictionary<string, string>>();
            IDictionary<string,string> public_access = new Dictionary<string,string>();
            public_access.Add("publicAccess","Container");
            patch_body.Add("properties",public_access);
            string json = JsonConvert.SerializeObject(patch_body, Formatting.Indented);
            var httpContent = new StringContent(json);
            var patch_response = await httpClient.PatchAsync(api_url,httpContent);
            patch_response.EnsureSuccessStatusCode();*/
            //log.LogInformation("CONTAINER NAME IS: "+container_name);
            return "ok";
        }

        [FunctionName("main_logger")]
        public static async Task<string> main_logger([ActivityTrigger] IDurableActivityContext context,ILogger log)
        {
            string resource_uri = context.GetInput<string>();
            var auth_token = createToken("rohil-event-hub-namespace.servicebus.windows.net","rohil-policy","E97AjfaF1SZgn+3O2zDCzRC/E/yDZgSfrIZNDWA6ASs=");
            //var auth_token = " sb://rohil-event-hub-namespace.servicebus.windows.net/;SharedAccessKeyName=rohil-policy;SharedAccessKey=E97AjfaF1SZgn+3O2zDCzRC/E/yDZgSfrIZNDWA6ASs=;EntityPath=rohil-event-hub"
            var event_hub_api = "https://rohil-event-hub-namespace.servicebus.windows.net/rohil-event-hub/messages?timeout=60&api-version=2014-01";
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("SharedAccessSignature",auth_token);
            log.LogInformation("auth token created");
            var uri_json = JsonConvert.SerializeObject(resource_uri);
            var httpContent =new StringContent(uri_json);
            var post_response = await httpClient.PostAsync(event_hub_api,httpContent);
            log.LogInformation("post sent");
            log.LogInformation(post_response.ToString());
            return "ok";
        }

        public static async Task<string> blob_maker(string content)
        {
            //String new_file_uri = string.Format("https://{0}.blob.core.windows.net/{1}/{2}",account_name,container_name,blob_name); //create or update an existing blob(overwrite)
            string new_file_uri = "https://rohillistcontainers.blob.core.windows.net/trial/time_exception.txt";
            string client_id = "559ebaaf-1d27-4022-82bd-3cd5c3f77d3d";
            var azureServiceTokenProvider = new AzureServiceTokenProvider($"RunAs=App;AppId={client_id}");
            string mgmtAccessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://storage.azure.com/");
            DateTime now = DateTime.UtcNow;
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("x-ms-date", now.ToString("R", CultureInfo.InvariantCulture));
            httpClient.DefaultRequestHeaders.Add("x-ms-version", "2020-04-08");
            httpClient.DefaultRequestHeaders.Add("x-ms-blob-type","BlockBlob");
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgmtAccessToken);  
            string body = content;
            var Content = new StringContent(body,Encoding.UTF8, "text/plain");
            var put_response = await httpClient.PutAsync(new_file_uri,Content);
            put_response.EnsureSuccessStatusCode();
            return "hello";
        }

        private static async Task<string> Read_central_file()
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
            //JObject response_json = JObject.Parse(response);
            //var funtion_to_chain = response_json[read_tag]?.Value<string>();
            return response;
        }

        private static string createToken(string resourceUri, string keyName, string key)
        {
            TimeSpan sinceEpoch = DateTime.UtcNow - new DateTime(1970, 1, 1);
            var week = 60 * 60 * 24 * 7;
            var expiry = Convert.ToString((int)sinceEpoch.TotalSeconds + week);
            string stringToSign = HttpUtility.UrlEncode(resourceUri) + "\n" + expiry;
            HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
            var sasToken = String.Format(CultureInfo.InvariantCulture, "sr={0}&sig={1}&se={2}&skn={3}", HttpUtility.UrlEncode(resourceUri), HttpUtility.UrlEncode(signature), expiry, keyName);
            return sasToken;
        }

        public static async Task<string> ReadBlob_tagname(ILogger log)
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
            var tag_name = response_json["tagName"]?.Value<string>();
            return tag_name;
        }



        private static async Task get_policy_by_id(string resource_uri,ILogger log)
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
            JObject checker_cmk = (JObject)response_json["properties"]?["parameters"]?["storageAccountsShouldUseCustomerManagedKeyForEncryptionMonitoringEffect"];
            var value_cmk = checker_cmk["value"]?.Value<string>();
            if(value_cmk.Equals("Disabled"))
            {
                checker_cmk.Property("value").Remove();
                checker_cmk.Add("value","Audit");
                var Content = new StringContent(response_json.ToString(),Encoding.UTF8, "application/json");
                var put_response = await httpClient.PutAsync(get_api_url,Content);
                put_response.EnsureSuccessStatusCode();
                //log.LogInformation(put_response.ToString());
            }

            JObject checker_sql = (JObject)response_json["properties"]?["parameters"]?["sqlDbEncryptionMonitoringEffect"];
            var value_sql = checker_sql["value"]?.Value<string>();
            if(value_sql.Equals("Disabled"))
            {
                checker_sql.Property("value").Remove();
                checker_sql.Add("value","AuditIfNotExists");
                var Content = new StringContent(response_json.ToString(),Encoding.UTF8, "application/json");
                var put_response = await httpClient.PutAsync(get_api_url,Content);
                put_response.EnsureSuccessStatusCode();
                //log.LogInformation(put_response.ToString());
            }

        }
    }
}
