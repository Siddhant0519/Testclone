using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Utilities
{
    /// <summary>
    /// This class contains the code which is responsible for retrying a failed HTTP request for remediation.
    /// </summary>
    public class RetryAPI
    {
        /// <summary>
        /// 
        /// This function fetches the retry threshold - number of times the retry process will work, and delay between each retry process from EnvConfig.json
        /// The delay between each retry is reset if a lower value is found within previous failed request. But it does not take values for more than 3 minutes. If it is more than 3 minutes, it sets the retry delay to 3 minutes.
        /// </summary>
        public static async Task<string> Process(JObject eventDetails, List<string> ruleList, HttpResponseMessage responseObject, string functionAppId, string functionName, ILogger log)
        {
            int retryCount = 0;
            string serviceType = string.Empty;
            string baseResourceURI = string.Empty;
            string baseOperationName = string.Empty;
            string ruleId = string.Join(",", ruleList);
            try
            {
                retryCount = Convert.ToInt32(eventDetails["retryCount"].ToString());
                serviceType = eventDetails["eventData"]["resourceProvider"].ToString();
                baseResourceURI = eventDetails["eventData"]["resourceUri"].ToString();
                baseOperationName = eventDetails["eventData"]["operationName"].ToString();

                JObject envConfigObject = await StorageAccountUtility.GetBlob(eventDetails["storageAccessToken"].ToString(), eventDetails["storageAccountURI"].ToString(), eventDetails["envFileName"].ToString(), log);
                JObject functionConfigObject = await StorageAccountUtility.GetBlob(eventDetails["storageAccessToken"].ToString(), eventDetails["StorageAccountOperationalURI"].ToString(), "FunctionAppConfig.json", log);
                FunctionDetails functionAppData = new FunctionDetails();
                foreach (var functionApp in functionConfigObject["functionApps"])
                {
                    if (functionApp["id"].ToString().Equals(functionAppId))
                    {
                        functionAppData.FunctionAppKey = functionApp["functionKey"].ToString();
                        functionAppData.FunctionAppName = functionApp["name"].ToString();
                        break;
                    }
                }

                int retryCountThreshold = Convert.ToInt32(envConfigObject["generalSetting"]["retryCount"].ToString());
                int retryDelay = Convert.ToInt32(envConfigObject["generalSetting"]["retryDelayInSeconds"].ToString());
                string result = await responseObject.Content.ReadAsStringAsync();

                log.LogInformation($"Fetched the function app info, the result of the failed response is {result} and retry threshold is {retryCountThreshold} and retry delay is {retryDelay} and the status code is {responseObject.StatusCode}");
                if (VerifyHttpStatusForRetry(responseObject.StatusCode))
                {
                    log.LogInformation("The status code is retryable");
                    if (retryCount < retryCountThreshold)
                    {
                        log.LogInformation($"Current retry count is {retryCount}");
                        retryCount++;
                        string retryAfter;
                        if (responseObject.Headers.TryGetValues("Retry-After", out IEnumerable<string> headerValues))
                        {
                            log.LogInformation($"Retry-After header value found, {headerValues.First().ToString()}");
                            retryAfter = headerValues.First().ToString();
                            if (Int32.TryParse(retryAfter, out int retryAfterSeconds))
                            {
                                if (retryDelay < retryAfterSeconds)
                                {
                                    retryDelay = retryAfterSeconds;
                                }
                            }

                            log.LogInformation($"Retry delay is now set to {retryDelay}");
                        }

                        retryDelay = retryDelay > 180 ? 180 : retryDelay;
                        Thread.Sleep(retryDelay * 1000);
                        using (var httpClient = new HttpClient())
                        {
                            httpClient.DefaultRequestHeaders.Accept.Clear();
                            string url = $"https://{functionAppData.FunctionAppName}.azurewebsites.net/api/{functionName}?code={functionAppData.FunctionAppKey}";
                            eventDetails["retryCount"] = retryCount;
                            StringContent content = new StringContent(JsonConvert.SerializeObject(eventDetails), Encoding.UTF8, "application/json");
                            log.LogInformation($"Retrying the url {url} with content {JsonConvert.SerializeObject(eventDetails["eventData"].ToString())}");
                            HttpResponseMessage response = await httpClient.PostAsync(url, content);
                            result = string.Empty;
                            try
                            {
                                result = await response.Content.ReadAsStringAsync();
                                log.LogInformation($"Initiated the durable function request {result}");
                                response.EnsureSuccessStatusCode();
                            }
                            catch (Exception ex)
                            {
                                log.LogError("Remediation Error:: Failed to trigger the function app {functionAppName} and retry count is {retryCount}. Following is the resource details, Rule Id={ruleId}  Service Type={serviceType}, Resource URI={resourceURI}, Operation Name={operationName}. Exception details={exceptionData}", functionAppData.FunctionAppName, retryCount, ruleId, serviceType, baseResourceURI, baseOperationName, ex);
                            }
                        }
                    }
                    else
                    {
                        throw new Exception($"Retry count exceeded the threshold value of {retryCountThreshold}");
                    }
                }
                else if (responseObject.StatusCode == HttpStatusCode.BadRequest || responseObject.StatusCode == HttpStatusCode.Unauthorized)
                {
                    log.LogError("Remediation Error:: Failed to remediate the resource. Following is the resource details, Rule Id={ruleId}  Service Type={serviceType}, Resource URI={resourceURI}, Operation Name={operationName}. Exception details={exceptionData}", ruleId, serviceType, baseResourceURI, baseOperationName, result);
                }
                else if (responseObject.StatusCode == HttpStatusCode.Forbidden)
                {
                    log.LogError("Forbidden Error:: Failed to remediate the resource. Following is the resource details, Rule Id={ruleId}  Service Type={serviceType}, Resource URI={resourceURI}, Operation Name={operationName}. Exception details={exceptionData}", ruleId, serviceType, baseResourceURI, baseOperationName, result);
                }
                else if (responseObject.StatusCode == HttpStatusCode.NotFound)
                {
                    log.LogInformation($"Resource Not Found:: The {baseResourceURI} was not found.");
                }
                else if (responseObject.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    log.LogError("Too Many Requests:: Failed to remediate the resource as it returned an error code of 429 (TooManyRequests). Following is the resource details, Rule Id={ruleId}  Service Type={serviceType}, Resource URI={resourceURI}, Operation Name={operationName}. Exception details={exceptionData}", ruleId, serviceType, baseResourceURI, baseOperationName, result);
                }
                else
                {
                    log.LogError($"Forbidden Error:: The {baseResourceURI} resource returned an error state which cannot be processed further. Result details {result}");
                }

                log.LogInformation("Retry Process Completed");
                return "Retry Process Completed";
            }
            catch (Exception ex)
            {
                log.LogError("Remediation Error:: Failed to remediate the service after retrying {retryCount} times. Following is the details, Rule Id={ruleId}  Service Type={serviceType}, Resource URI={resourceURI}, Operation Name={operationName}. Exception details={exceptionData}", retryCount, ruleId, serviceType, baseResourceURI, baseOperationName, ex);
                return $"Exception occured while processing the retry logic. {ex}";
            }
        }

        /// <summary>
        /// This function contains certain HTTP status codes for which the retryAPI Process function is called
        /// </summary>
        public static bool VerifyHttpStatus(HttpStatusCode httpStatusCode)
        {
            List<HttpStatusCode> statusCodeList = new List<HttpStatusCode>()
            {
                HttpStatusCode.BadRequest,
                HttpStatusCode.Unauthorized,
                HttpStatusCode.Forbidden,
                HttpStatusCode.NotFound,
                HttpStatusCode.RequestTimeout,
                HttpStatusCode.Conflict,
                HttpStatusCode.TooManyRequests,
                HttpStatusCode.InternalServerError,
                HttpStatusCode.BadGateway,
                HttpStatusCode.ServiceUnavailable
            };

            return statusCodeList.Any(item => item.Equals(httpStatusCode)) ? true : false;
        }

        /// <summary>
        /// This function checks whether the remediation request is retryable or not.
        /// </summary>
        public static bool VerifyHttpStatusForRetry(HttpStatusCode httpStatusCode)
        {
            List<HttpStatusCode> statusCodeList = new List<HttpStatusCode>()
            {
                HttpStatusCode.RequestTimeout,
                HttpStatusCode.Conflict,
                HttpStatusCode.InternalServerError,
                HttpStatusCode.BadGateway,
                HttpStatusCode.ServiceUnavailable
            };

            return statusCodeList.Any(item => item.Equals(httpStatusCode)) ? true : false;
        }
    }
}
