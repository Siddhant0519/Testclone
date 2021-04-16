using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Utilities
{
    /// <summary>
    /// This class contains the code to fetch configuration values from JSON config files for the specified tag.
    /// This class contains the code to
    /// </summary>
    public class ProcessResourceTag
    {
        /// <summary>
        /// This function is called by the almost every function which is performing the remediation action.
        /// This function fetches configuration values from JSON config files for the specified tag.
        /// This function updates a guardrails specified tag, if it already exists on the resource. If it does not, it applies a guardrails default tag and remediation functions perform remediation specified in that tag config.
        /// </summary>
        public static JObject GetConfigFromTag(JToken tagDetails, JObject resourceConfigDetails, JObject envConfig, ILogger log, string resourceURI)
        {
            JObject configResult;
            try
            {
                log.LogInformation("Inside process resource tag");
                
                //log.LogInformation(envConfig.ToString());
                //log.LogInformation(tagDetails.ToString());// tags attached to storage account
                //log.LogInformation(resourceConfigDetails.ToString());// tags mentioned in storage account config
                log.LogInformation("printing tag details value ");
                string tagNameCustom = string.Empty;
                string tagValueCustom = string.Empty;
                if (tagDetails != null)
                {
                    var dict = JObject.Parse(tagDetails.ToString());
                    
                    log.LogInformation(dict.ToString());
                    
                    foreach (var kv in dict)
                    {

                        //log.LogInformation(kv.Key + ":" + kv.Value);
                        tagNameCustom = kv.Key;
                        tagValueCustom = kv.Value.ToString();
                    }
                    
                    log.LogInformation(tagNameCustom + tagValueCustom);
                    log.LogInformation(resourceURI);
                }
                ;
                string tagName = envConfig["generalSetting"]["resourceTagName"].ToString();

                string currentVersion = envConfig["generalSetting"]["currentVersion"].ToString();
                //log.LogInformation(tagName);
                /*if (!ValidateResourceTag(tagDetails, tagName, log))
                {
                    log.LogError($"Tag Error:: The tag structure for the given resource does not adhere to the schema. Following is the details, Tag Details={tagDetails}");
                }*/

                /*string tagValue = string.Empty;
                
                if (tagDetails != null && !string.IsNullOrEmpty(tagDetails.ToString()) && tagDetails[tagName] != null && !string.IsNullOrEmpty(tagDetails[tagName].ToString()))
                {
                    try
                    {
                        JObject tagConfig = JObject.Parse(tagDetails[tagName].ToString());
                        if (tagConfig["config"] != null && !string.IsNullOrEmpty(tagConfig["config"].ToString()))
                        {
                            tagValue = tagConfig["config"].ToString();// tagvalue of storage account tagConfig["config"] == prod
                            log.LogInformation($"Tag configuration is {tagValue}");
                        }
                    }   
                    catch { }
                }*/
                
                configResult = new JObject
                {
                    ["tagDetails"] = tagValueCustom//JObject.Parse("{\"config\": \"" + tagValue + "\",\"time\": \"" + DateTime.UtcNow + "\",\"version\": \"" + currentVersion + "\"}")
                };
                
                configResult["configDetails"] = resourceConfigDetails["defaultConfig"];
                //log.LogInformation(configResult["configDetails"].ToString());
                
                foreach (var config in resourceConfigDetails["configuration"])
                {

                    if (config.ToString().Contains("resources"))
                    {
                        //log.LogInformation("inside first if of foreach"+ config["ID"].ToString());

                        
                        if (config["resources"].ToString().Contains(resourceURI))
                        {
                           //log.LogInformation("inside third if of foreach");
                           configResult["configDetails"] = config;
                        }
                        else
                        {
                            continue;
                        }
                            
                    }

                    if (resourceConfigDetails["configuration"].ToString().Contains("E001"))
                    {
                        //log.LogInformation("inside first else of foreach");
                        if (tagValueCustom == config["tagValue"].ToString() && tagNameCustom == config["tagName"].ToString())
                        {
                            //log.LogInformation("ran successfully");
                            configResult["configDetails"] = config;
                        }
                        else
                        {
                            continue;
                        }
                    }
                    int i = Convert.ToInt32(config["ID"].ToString().Substring(3)); 
                    int j = Convert.ToInt32(configResult["configDetails"]["ID"].ToString().Substring(3));
                    //log.LogInformation(i.ToString() + j.ToString());
                    
                    if (i > j)
                    {
                        configResult["configDetails"] = config;
                    }
                    

                }
                log.LogInformation("------------------");
                //log.LogInformation(configResult["configDetails"]["ID"].ToString());
                    
                
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to process the resource tag. Exception - " + ex);
            }

            log.LogInformation($"The constructed config/tag object is {configResult}");
            return configResult;
        }

        /// <summary>
        /// This function is called by the almost every function which is performing the remediation action.
        /// This function fetches email IDs from guardrails contact tag to which the alert emails would be sent.
        /// </summary>
        public static string GetContactFromTag(JToken tagDetails, string existingEmailIds, JObject envConfig, ILogger log)
        {
            string emailIds;    
            try
            {
                emailIds = existingEmailIds.Replace(",", ";");
                string tagName = envConfig["generalSetting"]["contactTagName"].ToString();
                if (tagDetails != null && !string.IsNullOrEmpty(tagDetails.ToString()) && tagDetails[tagName] != null && !string.IsNullOrEmpty(tagDetails[tagName].ToString()))
                {
                    string contactEmails = tagDetails[tagName].ToString();
                    foreach (string emailId in contactEmails.Contains(",") ? contactEmails.Split(",") : contactEmails.Split(";"))
                    {
                        if (RegexUtilities.IsValidEmail(emailId))
                        {
                            if (string.IsNullOrEmpty(emailIds))
                            {
                                emailIds += emailId;
                            }
                            else
                            {
                                emailIds += ";" + emailId;
                            }
                        }
                        else
                        {
                            log.LogInformation($"The email Id {emailId} is invalid and won't be added to the receipient list");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogInformation("Failed to process the contact tag. Exception - " + ex);
                emailIds = existingEmailIds;
            }

            log.LogInformation($"The existing email Ids were {existingEmailIds} and after processing we got {emailIds}");
            return emailIds;
        }

        /// <summary>
        /// This function is called by the almost every function which is performing the remediation action.
        /// This function checks whether the given resource falls under databricks. It checks for any databricks tag defined in EnvConfig.json to the resource. If it is present, the function apps does not perform any remediation to the resource.
        /// </summary>
        public static bool VerifyDatabrickTag(JObject resourceData, JObject envConfig)
        {
            try
            {
                bool resourceGroupMatch = false;
                if (resourceData["id"] != null)
                {
                    string[] resourceUriArray = resourceData["id"].ToString().Split("/");
                    if (resourceUriArray[3].ToLower().Equals("resourcegroups"))
                    {
                        string resourceGroup = resourceUriArray[4];
                        resourceGroupMatch = Regex.IsMatch(resourceGroup, envConfig["generalSetting"]["databricksRgRegex"].ToString(), RegexOptions.IgnoreCase);
                    }
                }

                if (!resourceGroupMatch)
                {
                    return false;
                }

                JObject tagDetails = null;
                if(resourceData["tags"] != null && !string.IsNullOrEmpty(resourceData["tags"].ToString()))
                {
                    tagDetails = resourceData["tags"].ToObject<JObject>();
                }
                else if (resourceData["properties"]["tags"] != null && !string.IsNullOrEmpty(resourceData["properties"]["tags"].ToString()))
                {
                    tagDetails = resourceData["properties"]["tags"].ToObject<JObject>();
                }

                if (tagDetails != null)
                {
                    foreach (var tag in envConfig["generalSetting"]["databricksTags"])
                    {
                        if (tag != null && tag["tagName"] != null && tag["tagValue"] != null)
                        {
                            if (tagDetails[tag["tagName"].ToString()] != null && tagDetails[tag["tagName"].ToString()].ToString() == tag["tagValue"].ToString())
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to verify the resource tags for databrick resource. Exception - " + ex);
            }
        }

        /// <summary>
        /// This function is called by the GetConfigFromTag function.
        /// This function validates if a given tag is structured as per the structure defined in Guardrails.
        /// </summary>
        private static bool ValidateResourceTag(JToken tagDetails, string tagName, ILogger log)
        {
            bool result = true;
            try
            {
                if (tagDetails == null || string.IsNullOrEmpty(tagDetails.ToString()) || tagDetails[tagName] == null || string.IsNullOrEmpty(tagDetails[tagName].ToString()))
                {
                    result = false;
                }
                else
                {
                    try
                    {
                        JObject tagConfig = JObject.Parse(tagDetails[tagName].ToString());
                        if (tagConfig["config"] == null || string.IsNullOrEmpty(tagConfig["config"].ToString()))
                        {
                            result = false;
                        }
                        else if (tagConfig["time"] == null)
                        {
                            result = false;
                        }
                        else if (tagConfig["version"] == null)
                        {
                            result = false;
                        }
                    }
                    catch (JsonReaderException jex)
                    {
                        result = false;
                        log.LogInformation($"The tag value is not a valid json, Exception details = {jex}");
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                log.LogInformation($"Failed to perform tag validation, Exception details = {ex}");
                throw ex;
            }
        }
    }
}
