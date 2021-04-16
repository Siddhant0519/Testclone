using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Utilities
{
    public class ProcessManagementLock
    {
        public static async Task<JArray> DeleteLocks(string mgmtToken, string resourceURI, string lockAPIVersion, ILogger log, bool ignoreLockType = false)
        {
            JArray deletedLocks = new JArray();
            try
            {
                HttpClient httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgmtToken);
                string url = $"https://management.azure.com/{resourceURI}/providers/Microsoft.Authorization/locks?api-version={lockAPIVersion}";
                log.LogInformation($"The URL to get the locks is {url}");
                HttpResponseMessage response = await httpClient.GetAsync(url);
                dynamic lockDetails = null;
                try
                {
                    response.EnsureSuccessStatusCode();
                    string result = await response.Content.ReadAsStringAsync();
                    log.LogInformation($"Fetched the following list of locks {result}");
                    lockDetails = JsonConvert.DeserializeObject(result);
                }
                catch (Exception ex)
                {
                    log.LogInformation($"Error occcured while fetching the list of locks. The error code - {response.StatusCode} and the original exception is {ex}");
                    return deletedLocks;
                }

                if (lockDetails != null && lockDetails.value.Count > 0)
                {
                    foreach (var lockValue in lockDetails.value)
                    {
                        if (ignoreLockType || lockValue.properties.level != "CanNotDelete")
                        {
                            JObject lockObject = lockValue;
                            deletedLocks.Add(lockObject);
                            string deleteURL = $"https://management.azure.com/{lockValue.id}?api-version={lockAPIVersion}";
                            log.LogInformation($"The URL to delete the lock {lockValue} is {deleteURL}");
                            HttpResponseMessage deleteLockResponse = await httpClient.DeleteAsync(deleteURL);
                            try
                            {
                                deleteLockResponse.EnsureSuccessStatusCode();
                                string result = await deleteLockResponse.Content.ReadAsStringAsync();
                                log.LogInformation($"Deleted the lock successfully - {result}");
                            }
                            catch (Exception ex)
                            {
                                log.LogInformation($"Error occcured while deleting the lock. The error code - {response.StatusCode} and the original exception is {ex}");
                                return deletedLocks;
                            }
                        }
                    }

                    log.LogInformation("Successfully processed the deletion of locks");
                }
                else
                {
                    log.LogInformation("No Locks found");
                }

                return deletedLocks;
            }
            catch (Exception ex)
            {
                log.LogError("Delete Lock Error:: Failed to delete the lock for Resource={resourceURI}. Following is the Exception details={exceptionData}", resourceURI, ex);
                return deletedLocks;
            }
        }

        public static async Task<string> CreateLocks(JArray deletedLocks, string mgmtToken, string resourceURI, string lockAPIVersion, ILogger log)
        {
            try
            {
                if (deletedLocks != null && deletedLocks.Count > 0)
                {
                    log.LogInformation($"Processing the create operation for management locks for {resourceURI}");
                    HttpClient httpClient = new HttpClient();
                    httpClient.DefaultRequestHeaders.Accept.Clear();
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgmtToken);
                    foreach (var lockObject in deletedLocks)
                    {
                        JObject updateData = new JObject
                        {
                            ["properties"] = lockObject["properties"]
                        };

                        string updateDetails = JsonConvert.SerializeObject(updateData);
                        string url = $"https://management.azure.com/{lockObject["id"]}?api-version={lockAPIVersion}";
                        log.LogInformation($"The URL to create the lock is {url} and the data is {updateDetails}");
                        StringContent content = new StringContent(updateDetails, Encoding.UTF8, "application/json");
                        HttpResponseMessage response = await httpClient.PutAsync(url, content);
                        try
                        {
                            response.EnsureSuccessStatusCode();
                            string result = await response.Content.ReadAsStringAsync();
                            log.LogInformation($"Created the lock successfully - {result}");
                        }
                        catch (Exception ex)
                        {
                            log.LogInformation($"Exception occured while creating the lock. The error code - {response.StatusCode} and the original exception is {ex}");
                            if (response.StatusCode != System.Net.HttpStatusCode.ServiceUnavailable && response.StatusCode != System.Net.HttpStatusCode.NotFound)
                            {
                                throw new Exception($"Exception occured while creating the lock. The error code - {response.StatusCode} and the original exception is {ex}");
                            }
                            else
                            {
                                log.LogInformation($"The service {resourceURI} has either been deleted or cannot be accessed");
                                return "The service has either been deleted or cannot be accessed";
                            }
                        }
                    }

                    log.LogInformation("Successfully processed the creation of locks");
                }
                else
                {
                    log.LogInformation("No Locks found");
                }

                return "Successfully processed the creation of locks";
            }
            catch (Exception ex)
            {
                log.LogError("Create Lock Error:: Failed to create the lock for Resource={resourceURI}. Following is the Exception details={exceptionData}", resourceURI, ex);
                throw new Exception($"Failed to process the lock creation logic.The exception is {ex}");
            }
        }
    }
}
