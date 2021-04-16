using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Utilities;

namespace StorageMonitoring
{
    /// <summary>
    /// This class contains the code to remediate container access policy for the containers of Azure Storage Accounts.
    /// </summary>
    public static class ContainerMonitor
    {
        /// <summary>
        /// This function is an HTTP triggered functions and is called by the orchestrator.
        /// This function performs remediation to configure container access policy of storage account containers.
        /// </summary>
        [FunctionName("ContainerMonitor")]
        public static async Task<string> RunAsync([ActivityTrigger] string requestData, ILogger log)
        {
            bool suppressException = false;
            bool isRemediationError = true;
            string serviceType = string.Empty;
            string baseResourceURI = string.Empty;
            string operationName = string.Empty;
            string actionId = string.Empty;
            string activityName = string.Empty;
            List<string> ruleList = new List<string>() { "R012" };
            try
            {
                dynamic eventDetails = JsonConvert.DeserializeObject(requestData);
                JObject eventObject = JObject.Parse(requestData);
                if (eventObject["retryCount"] == null)
                {
                    eventObject["retryCount"] = 0;
                }

                serviceType = eventDetails.eventData.resourceProvider.ToString();
                baseResourceURI = eventDetails.eventData.resourceUri.ToString();
                operationName = eventDetails.eventData.operationName.ToString();
                string resourceURI = eventDetails.eventData.resourceUri;
                string accessToken = eventDetails.mgmtAccessToken;
                activityName = "StorageMonitoring_ContainerMonitor";
                actionId = Guid.NewGuid().ToString();

                log.LogInformation("Activity Started, details are, Activity Name={activityName}, Resource URI={resourceURI}, Service Type={serviceType}, Operation Name={operationName}, Action ID={actionId}.", activityName, baseResourceURI, serviceType, operationName, actionId);

                List<string> containerList = new List<string>();
                string storageAccountURI = string.Empty;
                string storageAccountKey = string.Empty;
                string listKeyURL = string.Empty;

                JObject APIVersionObject = eventObject["APIVersionObject"].ToObject<JObject>();
                storageAccountURI = resourceURI;
                listKeyURL = resourceURI;
                bool isSingleContainer = false;
                if (resourceURI.Contains("/blobServices"))
                {
                    isSingleContainer = true;
                    log.LogInformation("The request call is for the container event");
                    listKeyURL = resourceURI.Substring(0, resourceURI.IndexOf("/blobServices"));
                    storageAccountURI = listKeyURL;
                    int startIndex = resourceURI.IndexOf("containers/") + 11;
                    containerList.Add(resourceURI.Substring(startIndex, resourceURI.Length - startIndex));
                }

                dynamic storageDetails = null;
                JObject storageObject = null;
                using (var httpClient = new HttpClient())
                {
                    // Sending a get request on the resource to fetch resource data
                    log.LogInformation("Getting resource data for, Resource URI={resourceURI}, Action ID={actionId}", baseResourceURI, actionId);
                    httpClient.DefaultRequestHeaders.Accept.Clear();
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    string url = $"https://management.azure.com" + storageAccountURI + "?api-version=" + APIVersionObject["api-versions"]["microsoft.storage/storageaccounts"].ToString();
                    log.LogInformation($"The url for Storage account configuration is {url}");
                    HttpResponseMessage response = await httpClient.GetAsync(url);
                    string result = string.Empty;
                    if (RetryAPI.VerifyHttpStatus(response.StatusCode))
                    {
                        log.LogInformation("Calling the retry functionality");
                        var taskResult = RetryAPI.Process(eventObject, ruleList, response, "Storage", "StorageMonitorTrigger", log);
                        log.LogInformation($"API request failed. The process to retry the API has been triggered. Counter - {eventObject["retryCount"].ToString()}");
                        return $"API request failed. The process to retry the API has been triggered. Counter - {eventObject["retryCount"].ToString()}";
                    }
                    else
                    {
                        try
                        {
                            result = await response.Content.ReadAsStringAsync();
                            response.EnsureSuccessStatusCode();
                            log.LogInformation("The storage account data is = " + result);
                            storageDetails = JsonConvert.DeserializeObject(result);
                            storageObject = JObject.Parse(result);
                        }
                        catch (Exception ex)
                        {
                            log.LogInformation("Failed to fetch the storage account data. Response details - " + result);
                            throw ex;
                        }
                    }
                }

                if (storageDetails.kind.ToString().ToLower() != "filestorage" && !isSingleContainer)
                {
                    using (var httpClient = new HttpClient())
                    {
                        httpClient.DefaultRequestHeaders.Accept.Clear();
                        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                        string url = $"https://management.azure.com{resourceURI}/blobServices/default/containers?api-version={APIVersionObject["api-versions"]["microsoft.storage/storageaccounts/blobServices/containers"].ToString()}";
                        log.LogInformation($"The get URL to get the container list is  {url}");
                        HttpResponseMessage response = await httpClient.GetAsync(url);
                        string result = string.Empty;
                        dynamic containerData;
                        if (RetryAPI.VerifyHttpStatus(response.StatusCode))
                        {
                            log.LogInformation("Calling the retry functionality");
                            var taskResult = RetryAPI.Process(eventObject, ruleList, response, "Storage", "StorageMonitorTrigger", log);
                            log.LogInformation($"API request failed. The process to retry the API has been triggered. Counter - {eventObject["retryCount"].ToString()}");
                            return $"API request failed. The process to retry the API has been triggered. Counter - {eventObject["retryCount"].ToString()}";
                        }
                        else
                        {
                            try
                            {
                                result = await response.Content.ReadAsStringAsync();
                                response.EnsureSuccessStatusCode();
                                log.LogInformation("The container list is = " + result);
                                containerData = JsonConvert.DeserializeObject(result);
                            }
                            catch (Exception ex)
                            {
                                log.LogInformation("Failed to fetch the container list. Response details - " + result);
                                throw ex;
                            }
                        }

                        foreach (var containerObject in containerData.value)
                        {
                            containerList.Add(containerObject.name.ToString());
                        }
                    }
                }
                // checking if the storage account is a data lake storage gen 2
                bool isStorageAccount = storageDetails.properties.isHnsEnabled == null || storageDetails.properties.isHnsEnabled.ToString().ToLower().Equals("false");
                dynamic containerConfig = null;
                if (isStorageAccount)
                {
                    containerConfig = eventDetails.storageAccountConfig;
                }
                else
                {
                    containerConfig = eventDetails.dataLakeGen2Config;
                }

                if (containerConfig.enableRemediation.ToString() == "true")
                {
                    using (var httpClient = new HttpClient())
                    {
                        httpClient.DefaultRequestHeaders.Accept.Clear();
                        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                        string auditMode = string.Empty;
                        string investigationRequired = "false";
                        string remediateContainerAccessPolicy = "true";
                        string accessPolicy = string.Empty;

                        // Checking for databricks resource
                        /*if (ProcessResourceTag.VerifyDatabrickTag(storageObject, eventObject["envConfig"].ToObject<JObject>()))
                        {
                            log.LogInformation($"Databrick Resource Found: The resource uri is {resourceURI}");
                            return "Databrick resource found and hence no remediation will be performed";
                        }*/
                        // Fetchig the config data
                        JObject configResult = ProcessResourceTag.GetConfigFromTag(storageObject["tags"], containerConfig, eventObject["envConfig"].ToObject<JObject>(), log,resourceURI);
                        if (configResult == null)
                        {
                            throw new Exception("The guardrails remediation config could not be fetched");
                        }

                        eventDetails.userId = ProcessResourceTag.GetContactFromTag(storageObject["tags"], eventDetails.userId.ToString(), eventObject["envConfig"].ToObject<JObject>(), log);
                        log.LogInformation("user id ------------");
                        log.LogInformation(configResult["configDetails"].ToString());
                        auditMode = configResult["configDetails"]["auditMode"].ToString();
                        //investigationRequired = configResult["configDetails"]["investigationRequired"].ToString();
                        accessPolicy = configResult["configDetails"]["containerAccessPolicy"].ToString();
                        log.LogInformation("all variables coming");
                        if (auditMode == "false")
                        {
                            log.LogInformation("inside if condition");
                            remediateContainerAccessPolicy = configResult["configDetails"]["remediateContainerAccessPolicy"].ToString();
                        }
                        log.LogInformation("outside if");
                        // Converting the keyword Private to None
                        accessPolicy = accessPolicy == "Private" ? "None" : accessPolicy;
                        dynamic keyList;
                        string url = string.Empty;
                        keyList = eventDetails.storageAccountKey;
                        log.LogInformation("Entering for each");
                        foreach (var keyData in keyList.keys)
                        {
                           
                            if (keyData.keyName.ToString() == "key1")
                            {
                                storageAccountKey = keyData.value.ToString();
                                log.LogInformation(storageAccountKey);
                                break;
                            }
                        }
                        log.LogInformation("Exit foreach");
                        foreach (string containerName in containerList)
                        {
                            // Fetching container data
                            log.LogInformation($"Processing the {containerName} container");
                            string currentPolicy = string.Empty;
                            url = $"https://management.azure.com{listKeyURL}/blobServices/default/containers/{containerName}?api-version={APIVersionObject["api-versions"]["microsoft.storage/storageaccounts/blobServices/containers"].ToString()}";
                            log.LogInformation($"The URL to get the container is  {url}");
                            HttpResponseMessage response = await httpClient.GetAsync(url);
                            string result = string.Empty;
                            dynamic containerDetails;
                            if (RetryAPI.VerifyHttpStatus(response.StatusCode))
                            {
                                log.LogInformation("Calling the retry functionality");
                                var taskResult = RetryAPI.Process(eventObject, ruleList, response, "Storage", "StorageMonitorTrigger", log);
                                log.LogInformation($"API request failed. The process to retry the API has been triggered. Counter - {eventObject["retryCount"].ToString()}");
                                return $"API request failed. The process to retry the API has been triggered. Counter - {eventObject["retryCount"].ToString()}";
                            }
                            else
                            {
                                try
                                {
                                    result = await response.Content.ReadAsStringAsync();
                                    response.EnsureSuccessStatusCode();
                                    log.LogInformation("The container data is = " + result);
                                    containerDetails = JsonConvert.DeserializeObject(result);
                                }
                                catch (Exception ex)
                                {
                                    log.LogInformation("Failed to fetch the container detail. Response details - " + result);
                                    throw ex;
                                }
                            }

                            StringContent content = new StringContent(string.Empty);
                            if (!containerDetails.properties.publicAccess.ToString().Equals(accessPolicy))
                            {
                                if (auditMode == "false")
                                {
                                    if (remediateContainerAccessPolicy == "true")
                                    {
                                        log.LogInformation("Starting the remediation action for, Resource URI={resourceURI}, Action ID={actionId}", baseResourceURI, actionId);
                                        url = $"https://management.azure.com{listKeyURL}/blobServices/default/containers/{containerName}?api-version={APIVersionObject["api-versions"]["microsoft.storage/storageaccounts/blobServices/containers"].ToString()}";
                                        log.LogInformation($"The URL to update the container is  {url}");
                                        string tagName = eventObject["envConfig"]["generalSetting"]["resourceTagName"].ToString();

                                        JObject updateContainerObj = new JObject();
                                        updateContainerObj["properties"] = new JObject();
                                        updateContainerObj["properties"]["publicAccess"] = accessPolicy;

                                        log.LogInformation("The object value for container access policy is: " + JsonConvert.SerializeObject(updateContainerObj));
                                        JArray deletedLocks = await ProcessManagementLock.DeleteLocks(accessToken, storageAccountURI, APIVersionObject["api-versions"]["microsoft.authorization/locks"].ToString(), log);
                                        content = new StringContent(JsonConvert.SerializeObject(updateContainerObj), Encoding.UTF8, "application/json");
                                        response = await httpClient.PatchAsync(url, content);
                                        await ProcessManagementLock.CreateLocks(deletedLocks, accessToken, storageAccountURI, APIVersionObject["api-versions"]["microsoft.authorization/locks"].ToString(), log);

                                        if (RetryAPI.VerifyHttpStatus(response.StatusCode))
                                        {
                                            log.LogInformation("Calling the retry functionality");
                                            var taskResult = RetryAPI.Process(eventObject, ruleList, response, "Storage", "StorageMonitorTrigger", log);
                                            log.LogInformation($"API request failed. The process to retry the API has been triggered. Counter - {eventObject["retryCount"].ToString()}");
                                            return $"API request failed. The process to retry the API has been triggered. Counter - {eventObject["retryCount"].ToString()}";
                                        }
                                        else
                                        {
                                            try
                                            {
                                                response.EnsureSuccessStatusCode();
                                            }
                                            catch (Exception ex)
                                            {
                                                log.LogInformation("Failed to update the container details. Error details - " + ex.Message);
                                                throw ex;
                                            }
                                        }

                                        log.LogInformation($"Successfully remediated the container access policy for {containerName}");

                                        /*string urlSotrage = $"https://management.azure.com" + storageAccountURI + "?api-version=" + APIVersionObject["api-versions"]["microsoft.storage/storageaccounts"].ToString();
                                        // creating the tag object to update tags
                                        JObject updateTagObj;
                                        if (storageObject["tags"] != null)
                                        {
                                            updateTagObj = JObject.Parse(storageObject["tags"].ToString());
                                        }
                                        else
                                        {
                                            updateTagObj = new JObject();
                                        }

                                        updateTagObj[eventObject["envConfig"]["generalSetting"]["resourceTagName"].ToString()] = JsonConvert.SerializeObject(configResult["tagDetails"]);

                                        JObject tagObj = new JObject();
                                        {
                                            tagObj["tags"] = updateTagObj;
                                        }
                                        log.LogInformation("The object value for tag is: " + JsonConvert.SerializeObject(tagObj));
                                        StringContent content1 = new StringContent(JsonConvert.SerializeObject(tagObj), Encoding.UTF8, "application/json");
                                        deletedLocks = await ProcessManagementLock.DeleteLocks(accessToken, storageAccountURI, APIVersionObject["api-versions"]["microsoft.authorization/locks"].ToString(), log);
                                        log.LogInformation("Updating tag for, Resource URI={resourceURI}, Action ID={actionId}", baseResourceURI, actionId);
                                        response = await httpClient.PatchAsync(urlSotrage, content1);
                                        await ProcessManagementLock.CreateLocks(deletedLocks, accessToken, storageAccountURI, APIVersionObject["api-versions"]["microsoft.authorization/locks"].ToString(), log);
                                        if (RetryAPI.VerifyHttpStatus(response.StatusCode))
                                        {
                                            log.LogInformation("Calling the retry functionality");
                                            var taskResult = RetryAPI.Process(eventObject, ruleList, response, "Storage", "StorageMonitorTrigger", log);
                                            log.LogInformation($"API request failed. The process to retry the API has been triggered. Counter - {eventObject["retryCount"].ToString()}");
                                            return $"API request failed. The process to retry the API has been triggered. Counter - {eventObject["retryCount"].ToString()}";
                                        }
                                        else
                                        {
                                            try
                                            {
                                                response.EnsureSuccessStatusCode();
                                                log.LogInformation("Successfully updated tag for Container Monitor");
                                            }
                                            catch (Exception ex)
                                            {
                                                log.LogInformation("Failed to update the tag. Error details - " + ex.Message);
                                                throw ex;
                                            }
                                        }*/
                                    }
                                }

                                // Sending the remediation data for Storage Account Container Access Policy to be logged in Cosmos DB and Email API.
                                if (remediateContainerAccessPolicy == "true")
                                {
                                    RemediationData remediationData = new RemediationData()
                                    {
                                        MgmtAccessToken = accessToken,
                                        StorageAccessToken = eventDetails.storageAccessToken.ToString(),
                                        StorageAccountURI = eventDetails.storageAccountURI.ToString(),
                                        StorageAccountOperationalURI = eventDetails.StorageAccountOperationalURI.ToString(),
                                        RemediationEnabled = auditMode == "true" ? "false" : "true",
                                        RuleName = "R012",
                                        ResourceType = "microsoft.storage/storageaccounts/blobservices/containers",
                                        AdditionalDetails = $"The container {containerName} is updated with the access policy as {accessPolicy}",
                                        PrincipalType = eventDetails.principalType,
                                        UserId = eventDetails.userId,
                                        Location = storageDetails.location.ToString().ToLower().Replace(" ", string.Empty),
                                        SubscriptionId = eventDetails.eventData.subscriptionId.ToString(),
                                        ResourceURI = resourceURI,
                                        actionId = actionId,
                                        investigationRequired = investigationRequired
                                    };

                                    url = eventDetails.logFunctionURL;
                                    log.LogInformation("Calling the Function App to log the remediation data");
                                    content = new StringContent(JsonConvert.SerializeObject(remediationData), Encoding.UTF8, "application/json");
                                    response = await httpClient.PostAsync(url, content);
                                    try
                                    {
                                        response.EnsureSuccessStatusCode();
                                    }
                                    catch (Exception ex)
                                    {
                                        log.LogInformation("Failed to log the remediation data. Error details - " + ex.Message);
                                        isRemediationError = false;
                                        throw ex;
                                    }
                                    log.LogInformation("Successfully performed the remediation action for, Activity Name={activityName}, Resource URI={resourceURI}, Action ID={actionId}", activityName, baseResourceURI, actionId);

                                }
                            }
                            else
                            {
                                log.LogInformation($"The access policy of the container {containerName} is not public. No changes are made");
                            }
                        }

                        ruleList.Remove("R012");
                        log.LogInformation("Activity ended, Activity Name={activityName}, Resource URI={resourceURI}, Action ID={actionId}", activityName, baseResourceURI, actionId);
                        return "Successfully processed the remediation for Storage account container";
                    }
                }
                else
                {
                    log.LogInformation("The enable remediation config property is set to false and hence the process is bypassed, Activity Name={activityName}, Resource URI={resourceURI}, Action ID={actionId}", activityName, baseResourceURI, actionId);
                    log.LogInformation("Activity ended, Activity Name={activityName}, Resource URI={resourceURI}, Action ID={actionId}", activityName, baseResourceURI, actionId);
                    return "The enable remediation config property is set to false and hence the process is bypassed";
                }
            }
            catch (Exception ex)
            {
                if (!suppressException)
                {
                    string ruleId = string.Join(",", ruleList);
                    if (isRemediationError)
                    {
                        if (ex.ToString().ToLower().Contains("a connection attempt failed because the connected party did not properly respond after a period of time"))
                        {
                            log.LogError("Connection Attempt Failed:: Failed to remediate the Storage account Container. Following is the details, Rule Id={ruleId}  Service Type={serviceType}, Resource URI={resourceURI}, Operation Name={operationName}, Action ID={actionId}. Exception details={exceptionData}", ruleId, serviceType, baseResourceURI, operationName, actionId, ex);
                            log.LogInformation("Activity ended, Activity Name={activityName}, Resource URI={resourceURI}, Action ID={actionId}", activityName, baseResourceURI, actionId);
                            return "Exception occured while processing Storage account monitoring activity. Error details - " + ex;
                        }
                        log.LogError("Remediation Error:: Failed to remediate the Storage account Container. Following is the details, Rule Id={ruleId}  Service Type={serviceType}, Resource URI={resourceURI}, Operation Name={operationName}, Action ID={actionId}. Exception details={exceptionData}", ruleId, serviceType, baseResourceURI, operationName, actionId, ex);
                    }
                    else
                    {
                        log.LogError("Log Error:: Failed to log the remediation data for Storage account Container. Following is the details, Rule Id={ruleId}  Service Type={serviceType}, Resource URI={resourceURI}, Operation Name={operationName}, Action ID={actionId}. Exception details={exceptionData}", ruleId, serviceType, baseResourceURI, operationName, actionId, ex);
                    }
                }
                log.LogInformation("Activity ended, Activity Name={activityName}, Resource URI={resourceURI}, Action ID={actionId}", activityName, baseResourceURI, actionId);
                return "Exception occured while processing Storage account container monitoring activity. Error details - " + ex;
            }
        }
    }
}
