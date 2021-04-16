using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net.Http;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Net;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;

namespace Utilities
{
    public class HttpWrapper
    {
        public static async Task<HttpResponseMessage> GetResponseObject(string url, string httpMethod, string bearerAccessToken, Dictionary<string, string> headerList, string bodyContent, ILogger log)
        {
            HttpResponseMessage responseObject = null;
            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Accept.Clear();
                    if (!string.IsNullOrEmpty(bearerAccessToken))
                    {
                        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerAccessToken);
                    }

                    foreach (var header in headerList)
                    {
                        httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }

                    StringContent content = new StringContent(string.Empty);
                    if (!string.IsNullOrEmpty(bodyContent))
                    {
                        content = new StringContent(bodyContent, Encoding.UTF8, "application/json");
                    }

                    if (httpMethod.ToLower().Equals("get"))
                    {
                        return await httpClient.GetAsync(url);
                    }
                    else if (httpMethod.ToLower().Equals("post"))
                    {
                        return await httpClient.PostAsync(url, content);
                    }
                    else if (httpMethod.ToLower().Equals("put"))
                    {
                        return await httpClient.PutAsync(url, content);
                    }
                    else if (httpMethod.ToLower().Equals("patch"))
                    {
                        return await httpClient.PatchAsync(url, content);
                    }
                    else if (httpMethod.ToLower().Equals("delete"))
                    {
                        return await httpClient.DeleteAsync(url);
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogInformation($"HTTP API call failed. Error details - {ex}");
                throw ex;
            }

            return responseObject;
        }

        public static async Task<string> GetResult(JObject eventDetails, List<string> ruleList, string functionAppId, string functionName, string url, string httpMethod, string bearerAccessToken, Dictionary<string, string> headerList, string bodyContent, ILogger log)
        {
            string result = string.Empty;
            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Accept.Clear();
                    if (!string.IsNullOrEmpty(bearerAccessToken))
                    {
                        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerAccessToken);
                    }

                    foreach (var header in headerList)
                    {
                        httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }

                    HttpResponseMessage responseObject = null;
                    StringContent content = new StringContent(string.Empty);
                    if (!string.IsNullOrEmpty(bodyContent))
                    {
                        content = new StringContent(bodyContent, Encoding.UTF8, "application/json");
                    }

                    if (httpMethod.ToLower().Equals("get"))
                    {
                        responseObject = await httpClient.GetAsync(url);
                    }
                    else if (httpMethod.ToLower().Equals("post"))
                    {
                        responseObject = await httpClient.PostAsync(url, content);
                    }
                    else if (httpMethod.ToLower().Equals("put"))
                    {
                        responseObject = await httpClient.PutAsync(url, content);
                    }
                    else if (httpMethod.ToLower().Equals("patch"))
                    {
                        responseObject = await httpClient.PatchAsync(url, content);
                    }
                    else if (httpMethod.ToLower().Equals("delete"))
                    {
                        responseObject = await httpClient.DeleteAsync(url);
                    }

                    if (RetryAPI.VerifyHttpStatus(responseObject.StatusCode))
                    {
                        log.LogInformation("Calling the retry functionality");
                        var taskResult = RetryAPI.Process(eventDetails, ruleList, responseObject, functionAppId, functionName, log);
                        log.LogInformation($"API request failed. The process to retry the API has been triggered. Counter - {eventDetails["retryCount"].ToString()}");
                        return $"RetryTriggered";
                    }
                    else
                    {
                        result = await responseObject.Content.ReadAsStringAsync();
                        try
                        {
                            responseObject.EnsureSuccessStatusCode();
                            if(responseObject.StatusCode == HttpStatusCode.Accepted)
                            {
                                if (responseObject.Headers.TryGetValues("azure-asyncoperation", out IEnumerable<string> headerValues))
                                {
                                    string apiUrl = headerValues.First();
                                    string currentStatus = string.Empty;
                                    List<string> statusValues = new List<string>() { "Succeeded", "Failed", "Canceled" };
                                    do
                                    {
                                        log.LogInformation($"The URL to get the status is {apiUrl}");
                                        responseObject = await httpClient.GetAsync(apiUrl);
                                        result = string.Empty;
                                        dynamic statusData;
                                        result = await responseObject.Content.ReadAsStringAsync();
                                        try
                                        {
                                            responseObject.EnsureSuccessStatusCode();
                                            log.LogInformation("The status for azure async operation is = " + result);
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
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            log.LogInformation("Failed to trigger the API. Response details - " + result);
                            throw ex;
                        }
                    }

                    //Retry logic 
                }
            }
            catch (Exception ex)
            {
                log.LogInformation($"HTTP API call failed. Error details - {ex}");
                throw ex;
            }

            return result;
        }
    }
}
