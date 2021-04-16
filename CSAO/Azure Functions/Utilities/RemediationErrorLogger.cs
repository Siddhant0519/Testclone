using Microsoft.Azure.Cosmos;
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
    /// This class contains the code which is responsible for logging the error logs into Cosmos DB.
    /// </summary>
    public class RemediationErrorLogger
    {
        /// <summary>
        /// This function is called by ProcessErrorLog.cs in RemediationLog.
        /// This function processes the error logs and provide a specified structure to them.
        /// This function categorizes the error logs in code error and various HTTP errors.
        /// </summary>
        public static async Task<string> CosmosDB(JObject appInsightData, string subscriptionId, string resourceGroup, string cosmoDB, JObject APIVersionObject, string mgmtAccessToken, ILogger log)
        {
            try
            {
                if (appInsightData["data"]["SearchResult"] != null && appInsightData["data"]["SearchResult"]["tables"] != null)
                {
                    // Fetch all the error log details and dump it within the list
                    List<ErrorData> errorDataList = new List<ErrorData>();
                    foreach (var table in appInsightData["data"]["SearchResult"]["tables"])
                    {
                        if (table["rows"] != null)
                        {
                            foreach (var row in table["rows"])
                            {
                                dynamic rowItem = JsonConvert.DeserializeObject(row[4].ToString());
                                errorDataList.Add(new ErrorData
                                {
                                    LogTimeStamp = DateTime.Parse(row[0].ToString()),
                                    RuleId = rowItem.prop__ruleId,
                                    ServiceType = rowItem.prop__serviceType,
                                    ResourceURI = rowItem.prop__resourceURI,
                                    OperationName = rowItem.prop__operationName,
                                    ExceptionDetails = rowItem.prop__exceptionData,
                                    FunctionAppName = row[23].ToString(),
                                    SubFunctionName = row[6].ToString(),
                                    Message = row[1].ToString()
                                });
                            }
                        }
                    }

                    // Categorizing the error
                    List<ErrorData> uniqueErrorList = errorDataList.GroupBy(x => x.Message).Select(x => x.First()).ToList<ErrorData>();
                    foreach (ErrorData error in uniqueErrorList)
                    {
                        if (error.ExceptionDetails.ToString().Contains("HttpRequestException"))
                        {
                            if (error.ExceptionDetails.ToString().Contains("Not Found"))
                            {
                                error.ErrorCode = "404 (Not Found)";
                                error.ErrorType = "HttpError";
                            }

                            if (error.ExceptionDetails.ToString().Contains("Conflict"))
                            {
                                error.ErrorCode = "429 (Conflict)";
                                error.ErrorType = "HttpError";
                            }

                            if (error.ExceptionDetails.ToString().Contains("PreconditionFailed"))
                            {
                                error.ErrorCode = "412 (PreconditionFailed)";
                                error.ErrorType = "HttpError";
                            }

                            if (error.ExceptionDetails.ToString().Contains("Unauthorized"))
                            {
                                error.ErrorCode = "401 (Unauthorized)";
                                error.ErrorType = "HttpError";
                            }

                            if (error.ExceptionDetails.ToString().Contains("Bad Request"))
                            {
                                error.ErrorCode = "400 (Bad Request)";
                                error.ErrorType = "HttpError";
                            }

                            if (error.ExceptionDetails.ToString().Contains("Service Unavailable"))
                            {
                                error.ErrorCode = "503 (Service Unavailable)";
                                error.ErrorType = "HttpError";
                            }

                            if (error.ExceptionDetails.ToString().Contains("Internal Server Error"))
                            {
                                error.ErrorCode = "500 (Internal Server Error)";
                                error.ErrorType = "HttpError";
                            }

                            if (error.ExceptionDetails.ToString().Contains("Gateway Timeout"))
                            {
                                error.ErrorCode = "504 (Internal Server Error)";
                                error.ErrorType = "HttpError";
                            }

                            if (error.ExceptionDetails.ToString().Contains("Bad Gateway"))
                            {
                                error.ErrorCode = "502 (Internal Server Error)";
                                error.ErrorType = "HttpError";
                            }

                            if (error.ExceptionDetails.ToString().Contains("Forbidden"))
                            {
                                error.ErrorCode = "502 (Forbidden)";
                                error.ErrorType = "HttpError";
                            }

                            if (error.ExceptionDetails.ToString().Contains("429"))
                            {
                                error.ErrorCode = "429";
                                error.ErrorType = "HttpError";
                            }
                        }
                        else
                        {
                            error.ErrorType = "CodeError";
                        }
                    }

                    log.LogInformation("The remediation error which will be logged in cosmos Db is " + JsonConvert.SerializeObject(uniqueErrorList));
                    string cosmosdbPrimaryKey;
                    dynamic cosmosDBData = null;
                    HttpResponseMessage response;
                    using (var httpClient = new HttpClient())
                    {
                        HttpContent nullcontent = null;
                        httpClient.DefaultRequestHeaders.Accept.Clear();
                        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgmtAccessToken);
                        string url = $"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.DocumentDB/databaseAccounts/{cosmoDB}/listKeys?api-version={APIVersionObject["api-versions"]["microsoft.documentdb/databaseaccounts/listkeys"].ToString()}";
                        log.LogInformation($"The get URL for Cosmos DB list keys is {url}");
                        response = await httpClient.PostAsync(url, nullcontent);
                        string result = string.Empty;
                        try
                        {
                            result = await response.Content.ReadAsStringAsync();
                            response.EnsureSuccessStatusCode();
                            cosmosDBData = JsonConvert.DeserializeObject(result);
                        }
                        catch (Exception ex)
                        {
                            log.LogError("Log Error:: Failed to log the remediation data within CosmosDB. Following is the details, Rule Id={ruleId}  Service Type={serviceType}, Resource URI={resourceURI}, Operation Name={operationName}. Exception details={exceptionData}", string.Empty, string.Empty, url, string.Empty, ex);
                        }

                        if (cosmosDBData != null)
                        {
                            cosmosdbPrimaryKey = cosmosDBData.primaryMasterKey.ToString();
                            string EndpointUrl = $"https://{cosmoDB}.documents.azure.com:443/";
                            string databaseId = "HealthCheck";
                            string containerId = "RemediationErrors";
                            var options = new CosmosClientOptions() { ConnectionMode = ConnectionMode.Gateway };
                            CosmosClient cosmosClient = new CosmosClient(EndpointUrl, cosmosdbPrimaryKey, options);
                            Container cosmosContainer = cosmosClient.GetContainer(databaseId, containerId);
                            foreach (ErrorData error in uniqueErrorList)
                            {
                                MonitorRemediationErrorLog remediationError = new MonitorRemediationErrorLog();
                                remediationError.id = Guid.NewGuid().ToString();
                                remediationError.LogTimeStamp = error.LogTimeStamp;
                                remediationError.ErrorType = error.ErrorType;
                                remediationError.ErrorCode = error.ErrorCode;
                                remediationError.FunctionAppName = error.FunctionAppName;
                                remediationError.SubFunctionName = error.SubFunctionName;
                                remediationError.ResourceURI = error.ResourceURI;
                                remediationError.ServiceType = error.ServiceType;
                                remediationError.OperationName = error.OperationName;
                                remediationError.ExceptionDetails = error.ExceptionDetails;
                                try
                                {
                                    ItemResponse<MonitorRemediationErrorLog> logResponse = await cosmosContainer.CreateItemAsync<MonitorRemediationErrorLog>(remediationError);
                                }
                                catch (Exception ex)
                                {
                                    log.LogInformation("Failed to insert the remediation log within CosmoDB. Error details - " + ex.Message);
                                    throw ex;
                                }
                            }
                        }
                    }
                }
                else
                {
                    log.LogInformation("No error log record found as part of App insight query result");
                }

                return "Added the Data to CosmosDB";
            }
            catch (Exception ex)
            {
                log.LogError("Exception occured while processing Remedition errors activity. Error details - " + ex);
                throw new Exception("Exception occured while processing Remedition errors activity. Error details - " + ex);
            }
        }
    }
}
