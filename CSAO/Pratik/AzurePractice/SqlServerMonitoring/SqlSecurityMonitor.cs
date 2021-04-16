using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Linq;
using System.Net.Http.Headers;

namespace SqlServerMonitoring
{
    public static class SqlSecurityMonitor
    {
        [FunctionName("SqlSecurityMonitor")]
        public static async Task RunAsync([ActivityTrigger] string data, ILogger log)
        {
            dynamic eventDetails = JsonConvert.DeserializeObject(data);
            JObject eventObject = JObject.Parse(data);
            log.LogInformation(eventObject.ToString());

            string clientID = "c00e1530-1108-45cf-a3c8-59e6e77dd518"; 
            var azureServiceTokenProvider = new AzureServiceTokenProvider($"RunAs=App;AppId={clientID}");
            var managementAccessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://management.azure.com/");
            var storageAccessToken = await azureServiceTokenProvider.GetAccessTokenAsync("https://storage.azure.com/");
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managementAccessToken);

            //string listServerUri = $"https://management.azure.com/subscriptions/{subscriptionId}/providers/Microsoft.Sql/servers?api-version=2019-06-01-preview";
            //string vulnerabilityAssessmentsssessUrl = "https://management.azure.com/subscriptions/0bf4d827-27f5-4d30-8637-40002aaab9be/resourceGroups/practicedemo2/providers/Microsoft.Sql/servers/pratikdemoserver/vulnerabilityAssessments/Default/?api-version=2018-06-01-preview";
            string secpolicyuri =$"https://management.azure.com/subscriptions/0bf4d827-27f5-4d30-8637-40002aaab9be/resourceGroups/practicedemo2/providers/Microsoft.Sql/servers/pratikdemoserver/securityAlertPolicies/Default?api-version=2017-03-01-preview";
            string get_response = await client.GetStringAsync(secpolicyuri);
            JObject getResult = JObject.Parse(get_response);

            string storageEndpoint = "https://sqlvafl7o7xnzn56aq.blob.core.windows.net";
            string storageAccesskey = "JUYD7tbGdrRoxA9pXP8hdWQJuS3aqHyDCSe35lScrtq/srhUcsADpteXKW3/pAL1605TIAq4nSxJ9JRiosmTjg==";
            string storageContainerPath = $"{storageEndpoint}/vulnerability-assessment";
            
            JObject value = (JObject)getResult["properties"];
            
            value["storageAccountAccessKey"] = storageAccesskey;
            value["storageEndpoint"] = storageEndpoint;
            value["state"] = "Enabled";
            value["emailAccountAdmins"] = "true";            

            dynamic putData = new JObject();
            putData.properties = value;
            JObject test = (JObject)data;
            //put request

            var content = new StringContent(test.ToString(),Encoding.UTF8,"application/json");
            var putResponse = client.PutAsync(secpolicyuri, content).Result;  
            string presult = await putResponse.Content.ReadAsStringAsync();
            log.LogInformation(presult);


        }

    }
}