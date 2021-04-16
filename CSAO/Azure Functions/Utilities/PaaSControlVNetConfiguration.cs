using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Utilities
{
    /// <summary>
    /// This class contains the code to check whether a control vnet exists for a region or not. If it does not exists, it creates one and returns the subnet URI to the remediation functions.
    /// </summary>
    public class PaaSControlVNetConfiguration
    {
        /// <summary>
        /// This function checks whether a control vnet exists for a region or not. 
        /// If it does not exists, it creates one and returns the subnet URI to the remediation functions.
        /// The details of the vnet to be created exists in the EnvConfig.json
        /// If the details are not found in EnvConfig.json, it creates a control vnet and updates the details in the EnvConfig.json
        /// </summary>
        public static async Task<string> GetControlVNetId(string requestData, List<string> ruleList, string functionAppId, string functionName, ILogger log, string serverLocation)
        {
            try
            {
                string subnetId = string.Empty;
                dynamic eventDetails = JsonConvert.DeserializeObject(requestData);
                JObject eventObject = JObject.Parse(requestData);
                if (eventObject["retryCount"] == null)
                {
                    eventObject["retryCount"] = 0;
                }

                string resourceString = eventDetails.eventData.resourceUri.ToString();
                string subscriptionId = eventDetails.eventData.subscriptionId;
                string additionalDetails = string.Empty;
                string mgmtAccessToken = eventDetails.mgmtAccessToken;
                string env = eventDetails.envFileName.ToString();
                string[] resourceIDarraynew = resourceString.Split(new Char[] { '/' });
                string rgResource = resourceIDarraynew[4];

                // Calling blob service to download the subscription json file
                var tokenCredential = new TokenCredential(eventDetails.storageAccessToken.ToString());
                var storageCredentials = new StorageCredentials(tokenCredential);
                var container = new CloudBlobContainer(new Uri(eventDetails.storageAccountURI.ToString()), storageCredentials);
                log.LogInformation("The env file is " + env);
                CloudBlob cloudBlobClient = container.GetBlobReference(env);
                string envDetailsJson;
                using (var memoryStream = new MemoryStream())
                {
                    await cloudBlobClient.DownloadToStreamAsync(memoryStream);
                    envDetailsJson = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
                    log.LogInformation("Static VNet fetched from json - " + envDetailsJson);
                }

                cloudBlobClient = container.GetBlobReference("APIVersionConfig.json");
                string APIVersionConfig;
                using (var memoryStream = new MemoryStream())
                {
                    await cloudBlobClient.DownloadToStreamAsync(memoryStream);
                    APIVersionConfig = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
                }
                JObject APIVersionObject = JObject.Parse(APIVersionConfig);

                dynamic envDetails = JsonConvert.DeserializeObject(envDetailsJson);
                JObject envDetailsObj = JObject.Parse(envDetailsJson);
                string loc = serverLocation.ToLower().Replace(" ", string.Empty);
                log.LogInformation(loc);
                string result = string.Empty;
                string locDisplay = null;
                using (var httpClient = new HttpClient())
                {
                    string locurl = $"https://management.azure.com/subscriptions/{subscriptionId}/locations?api-version={APIVersionObject["api-versions"]["subscriptions/locations"]}";
                    httpClient.DefaultRequestHeaders.Accept.Clear();
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgmtAccessToken);
                    log.LogInformation($"The get url for locations is {locurl}");
                    HttpResponseMessage responseloc = await httpClient.GetAsync(locurl);
                    result = await responseloc.Content.ReadAsStringAsync();
                    dynamic locresult = JsonConvert.DeserializeObject(result);
                    if (responseloc.IsSuccessStatusCode)
                    {
                        foreach (var location in locresult.value)
                        {
                            if (loc.Equals(location.name.ToString()))
                            {
                                locDisplay = location.displayName;
                                log.LogInformation("The display location name is" + locDisplay);
                            }
                        }
                    }

                    bool vnetExists = false;
                    bool vnetExistsInConfig = false;
                    string virtualNetworkName = null;
                    string resourceGroup = null;
                    foreach (var staticVNet in envDetails.controlVnet.virtualNetworks)
                    {
                        if (loc.Equals(staticVNet.locationName.ToString()) || loc.Equals(staticVNet.locationDisplayName.ToString()))
                        {
                            vnetExistsInConfig = true;
                            log.LogInformation("the flag value" + vnetExists);
                            virtualNetworkName = staticVNet.name.ToString();
                            resourceGroup = staticVNet.resourceGroup.ToString();
                            log.LogInformation($"The location matched for {virtualNetworkName} vnet");
                            httpClient.DefaultRequestHeaders.Accept.Clear();
                            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgmtAccessToken);
                            string url = $"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Network/virtualNetworks/{virtualNetworkName}?api-version={APIVersionObject["api-versions"]["microsoft.network/virtualnetworks"]}";
                            log.LogInformation($"The get URL for VNet is {url}");
                            HttpResponseMessage responsevnet = await httpClient.GetAsync(url);
                            result = await responsevnet.Content.ReadAsStringAsync();
                            log.LogInformation(result);
                            if (responsevnet.IsSuccessStatusCode)
                            {
                                vnetExists = true;
                                result = string.Empty;
                                dynamic virtualNetwork;
                                try
                                {
                                    result = await responsevnet.Content.ReadAsStringAsync();
                                    responsevnet.EnsureSuccessStatusCode();
                                    log.LogInformation("The virtual network details are = " + result);
                                    virtualNetwork = JsonConvert.DeserializeObject(result);
                                    return virtualNetwork.properties.subnets[0].id.ToString();
                                }
                                catch (Exception ex)
                                {
                                    log.LogInformation("Failed to fetch the VNet details. Response details - " + result);
                                    throw ex;
                                }
                            }
                            else
                            {
                                log.LogInformation("The Vnet Doesn't Exists so creating one");
                            }

                            // If the VNet is not found in Azure then verify whether the Resource group exists or not
                            httpClient.DefaultRequestHeaders.Accept.Clear();
                            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgmtAccessToken);
                            string resourcegroupURI = $"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}?api-version={APIVersionObject["api-versions"]["resourcegroups"]}";
                            HttpResponseMessage responseRG = await httpClient.GetAsync(resourcegroupURI);
                            result = await responseRG.Content.ReadAsStringAsync();
                            log.LogInformation("Resource Group Result " + result);
                            if (responseRG.IsSuccessStatusCode)
                            {
                                log.LogInformation($"The Resource Group with name {resourceGroup} exist");
                            }
                            else
                            {
                                resourceGroup = rgResource;
                                log.LogInformation($"The Resource Group with name {resourceGroup} will be used ");
                                staticVNet.resourceGroup = resourceGroup;
                                string msg = JsonConvert.SerializeObject(staticVNet, Newtonsoft.Json.Formatting.Indented);
                                log.LogInformation(msg);
                            }

                            break;
                        }
                    }

                    string vnetName = null;
                    if (!vnetExistsInConfig || !vnetExists)
                    {
                        log.LogInformation("The flag value for vnet exists in config is  " + vnetExistsInConfig);
                        log.LogInformation("The flag value for Vnet exists is " + vnetExists);
                        if (!vnetExists)
                        {
                            vnetName = virtualNetworkName;
                        }
                        if (!vnetExistsInConfig)
                        {
                            vnetName = loc + "_staticvnet";
                            resourceGroup = rgResource;
                        }

                        httpClient.DefaultRequestHeaders.Accept.Clear();
                        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgmtAccessToken);
                        log.LogInformation(virtualNetworkName);
                        string subnetURI = $"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/" + resourceGroup + "/providers/Microsoft.Network/virtualNetworks/" + vnetName + "?api-version=" + APIVersionObject["api-versions"]["microsoft.network/virtualnetworks"].ToString();
                        string vnetJson = "{\"properties\": {\"addressSpace\": {\"addressPrefixes\": [\"" + envDetailsObj["generalSetting"]["controlVNetCIDR"].ToString() + "\"]},\"subnets\": [{\"name\": \"" + vnetName + "subnet\",\"properties\": {\"addressPrefix\": \"" + envDetailsObj["generalSetting"]["controlVNetCIDR"].ToString() + "\",\"serviceEndpoints\": [{\"service\": \"Microsoft.Storage\"},{\"service\": \"Microsoft.AzureActiveDirectory\"},{\"service\": \"Microsoft.AzureCosmosDB\"},{\"service\": \"Microsoft.ContainerRegistry\"},{\"service\": \"Microsoft.EventHub\"},{\"service\": \"Microsoft.KeyVault\"},{\"service\": \"Microsoft.ServiceBus\"},{\"service\": \"Microsoft.Web\"},{\"service\": \"Microsoft.Sql\"}]}}]},\"location\": \"" + loc + "\"}";
                        IEnumerable<string> headerValues;
                        StringContent content = new StringContent(vnetJson, Encoding.UTF8, "application/json");
                        log.LogInformation($"The URL to add VNet is {subnetURI} and content is {vnetJson}");
                        HttpResponseMessage response = await httpClient.PutAsync(subnetURI, content);
                        if (RetryAPI.VerifyHttpStatus(response.StatusCode))
                        {
                            log.LogInformation("Calling the retry functionality");
                            var taskResult = RetryAPI.Process(eventObject, ruleList, response, functionName, functionAppId, log);
                            log.LogInformation($"API request failed. The process to retry the API has been triggered. Counter - {eventObject["retryCount"]}");
                            return subnetId;
                        }
                        else
                        {
                            try
                            {
                                result = await response.Content.ReadAsStringAsync();
                                log.LogInformation("The response from vnet put is " + result);
                                response.EnsureSuccessStatusCode();
                                if (response.Headers.TryGetValues("azure-asyncoperation", out headerValues))
                                {
                                    subnetURI = headerValues.First();
                                }
                                else
                                {
                                    log.LogInformation("Failed to fetch the azure-asyncoperation header value");
                                    throw new Exception("Failed to fetch the azure-asyncoperation header value");
                                }

                                log.LogInformation("Submitted the request for adding new Virtual network. URL - " + subnetURI);
                            }

                            catch (Exception ex)
                            {
                                log.LogInformation("Failed to create the VNet. Error details - " + ex.Message);
                                throw ex;
                            }
                        }

                        string currentStatus = string.Empty;
                        List<string> statusValues = new List<string>() { "Succeeded", "Failed", "Canceled" };
                        do
                        {
                            log.LogInformation($"The URL to get the status is {subnetURI}");
                            response = await httpClient.GetAsync(subnetURI);
                            result = string.Empty;
                            dynamic statusData;
                            try
                            {
                                result = await response.Content.ReadAsStringAsync();
                                response.EnsureSuccessStatusCode();
                                log.LogInformation("The status for VNet create operation is = " + result);
                                statusData = JsonConvert.DeserializeObject(result);
                            }
                            catch (Exception ex)
                            {
                                log.LogInformation("Failed to fetch the create operation status data. Response details - " + result);
                                throw ex;
                            }

                            currentStatus = statusData.status.ToString();
                            Thread.Sleep(10000);
                        } while (!statusValues.Any(currentStatus.Contains));

                        if (currentStatus == "Succeeded")
                        {
                            subnetId = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Network/virtualNetworks/{vnetName}/subnets/{vnetName}subnet";
                            log.LogInformation($"The status for VNet create operation is -{currentStatus} and the subnet Id is {subnetId}");
                            string jsonToUpdate = string.Empty;

                            // This is in the case when the VNet data was not present in the config
                            if (!vnetExistsInConfig)
                            {
                                JArray staticVNetarray = (JArray)envDetailsObj["controlVnet"]["virtualNetworks"];
                                dynamic region = new JObject();
                                region.name = vnetName;
                                region.resourceGroup = resourceGroup;
                                region.locationName = loc;
                                region.locationDisplayName = locDisplay;
                                staticVNetarray.Add(region);
                                string updatedBlob = JsonConvert.SerializeObject(region);
                                log.LogInformation("Added this Static Vnet details in the env config" + updatedBlob);
                                jsonToUpdate = JsonConvert.SerializeObject(envDetailsObj, Newtonsoft.Json.Formatting.Indented);
                            }
                            // This is in the case when the VNet was in the config but the resource group details might not be correct or it was deleted
                            else
                            {
                                jsonToUpdate = JsonConvert.SerializeObject(envDetails, Newtonsoft.Json.Formatting.Indented);
                            }

                            log.LogInformation(jsonToUpdate);
                            //Updating the Json Config File.
                            try
                            {
                                CloudBlockBlob cloudblock = container.GetBlockBlobReference(env);
                                await cloudblock.UploadTextAsync(jsonToUpdate);
                                log.LogInformation("Updated the Json file");
                            }
                            catch (Exception ex)
                            {
                                log.LogInformation("Failed to update the config json file with new VNet details");
                                throw ex;
                            }
                        }
                        else
                        {
                            throw new Exception($"VNet creation failed. The status is {currentStatus}");
                        }
                    }
                }

                return subnetId;
            }
            catch (Exception ex)
            {
                log.LogError("Exception occured while processing Vnet Method activity. Error details - " + ex);
                throw new Exception("Exception occured while processing KeyVault monitoring activity. Error details - " + ex);
            }
        }
    }
}
