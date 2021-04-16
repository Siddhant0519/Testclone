using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Utilities
{
    /// <summary>
    /// This class contains the code to log a warning in Application Insights whenever a guardrails tag is deleted from a resource.
    /// </summary>
    public class TagDeleteAlert
    {
        /// <summary>
        /// This function is responsible to log a warning in Application Insights whenever a guardrails tag is deleted from a resource.
        /// </summary>
        public static async void GetServiceData(string resourceURI, ILogger log, JObject envConfig, JObject APIVersionObject, string mgmtAccessToken, string operationName)
        {
            try
            {
                string inScopeOperations = "{ \"inScope\":[ \"microsoft.datalakestore/accounts/write\", \"microsoft.streamanalytics/streamingjobs/write\", \"microsoft.eventhub/namespaces/write\", \"microsoft.logic/workflows/write\", \"microsoft.keyvault/vaults/write\", \"microsoft.documentdb/databaseaccounts/write\", \"microsoft.datafactory/factories/write\", \"microsoft.devices/iothubs/write\", \"microsoft.apimanagement/service/write\", \"microsoft.dbforpostgresql/servers/write\", \"microsoft.dbformysql/servers/write\", \"microsoft.eventgrid/domains/write\", \"microsoft.eventgrid/topics/write\", \"microsoft.network/azurefirewalls/write\", \"microsoft.network/virtualnetworks/write\", \"microsoft.web/sites/write\", \"microsoft.network/networksecuritygroups/write\", \"microsoft.network/loadbalancers/write\", \"microsoft.network/applicationgateways/write\", \"microsoft.sql/managedinstances/write\", \"microsoft.sql/managedinstances/databases/write\", \"microsoft.analysisservices/servers/write\", \"microsoft.cache/redis/write\", \"microsoft.databricks/workspaces/write\", \"microsoft.containerregistry/registries/write\", \"microsoft.containerservice/managedclusters/write\", \"microsoft.network/publicipaddresses/write\", \"microsoft.healthcareapis/services/write\", \"microsoft.cognitiveservices/accounts/write\", \"microsoft.machinelearningservices/workspaces/write\", \"microsoft.recoveryservices/vaults/write\", \"microsoft.automation/automationaccounts/write\", \"microsoft.servicebus/namespaces/write\", \"microsoft.datalakeanalytics/accounts/write\", \"microsoft.powerbidedicated/capacities/write\", \"microsoft.storage/storageaccounts/write\",  \"microsoft.network/networkinterfaces/write\", \"microsoft.sql/servers/write\", \"microsoft.sql/servers/databases/write\", \"microsoft.compute/virtualmachines/write\", \"microsoft.compute/virtualmachinescalesets/write\", \"microsoft.network/privatednszones/write\" ] }";
                dynamic dynamicInScopeOperations = JsonConvert.DeserializeObject(inScopeOperations);
                List<dynamic> inScoperOperationsList = dynamicInScopeOperations.inScope.ToObject<List<dynamic>>();
                if (inScoperOperationsList.Any(item => item.ToString().Equals(operationName.ToLower())))
                {

                    string tagName = envConfig["generalSetting"]["resourceTagName"].ToString();
                    var currentTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                    var lowerTimeLimit = DateTime.UtcNow.AddMinutes(-5).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                    JObject requestBody = new JObject();
                    {
                        requestBody["resourceId"] = resourceURI;
                        requestBody["interval"] = new JObject();
                        requestBody["interval"]["start"] = lowerTimeLimit;
                        requestBody["interval"]["end"] = currentTime;
                        requestBody["fetchPropertyChanges"] = true;
                    }

                    JObject activityLogDetails = null;
                    using (var httpClient = new HttpClient())
                    {
                        string result = string.Empty;
                        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgmtAccessToken);
                        string url = $"https://management.azure.com/providers/Microsoft.ResourceGraph/resourceChanges?api-version={APIVersionObject["api-versions"]["microsoft.resourcegraph"]}";
                        log.LogInformation("Calling the graph api to get activity log data");
                        StringContent content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
                        HttpResponseMessage response = await httpClient.PostAsync(url, content);
                        try
                        {
                            result = await response.Content.ReadAsStringAsync();
                            response.EnsureSuccessStatusCode();
                            log.LogInformation("The data is == " + result);
                            activityLogDetails = JObject.Parse(result);
                        }
                        catch (Exception ex)
                        {
                            log.LogInformation("Failed to get the resource change data - " + ex);
                            throw ex;
                        }
                    }

                    try
                    {
                        if (activityLogDetails["changes"] != null)
                        {
                            foreach (var data in activityLogDetails["changes"])
                            {
                                if (data["propertyChanges"] != null)
                                {
                                    foreach (var change in data["propertyChanges"])
                                    {
                                        if (change["changeType"].ToString().ToLower() == "remove" && change["propertyName"].ToString().ToLower() == "tags." + tagName.ToLower())
                                        {
                                            string changeID = JsonConvert.SerializeObject(data["changeId"]);
                                            string changeData = JsonConvert.SerializeObject(change);
                                            log.LogWarning("Remove Tag Alert:: Guardrails Tag has been removed from the resource. Following is the detail, Resource URI={resourceURI}. The resource property change data is={changeData} and change ID={changeID}", resourceURI, changeData, changeID);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        log.LogInformation("Failed to loop through resource data to search for tag remove operation- " + ex);
                        throw ex;
                    }
                }
                else
                {
                    log.LogInformation("Service not in scope to check for tag deletion alert");
                }
            }

            catch (Exception ex)
            {
                log.LogError("Exception occured. Error details - " + ex);
                throw ex;
            }
        }
    }
}
