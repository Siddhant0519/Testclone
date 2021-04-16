using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Utilities
{
    /// <summary>
    /// This class contains the code to fetch blob details from storage accounts
    /// </summary>
    public class StorageAccountUtility
    {
        /// <summary>
        /// This function is responsible for fetching blob details from storage accounts
        /// </summary>
        public static async Task<JObject> GetBlob(string storageToken, string storageAccountURI, string fileName, ILogger log)
        {
            JObject blobDetails = null;
            try
            {
                var tokenCredential = new TokenCredential(storageToken);
                var storageCredentials = new StorageCredentials(tokenCredential);
                var container = new CloudBlobContainer(new Uri(storageAccountURI), storageCredentials);
                CloudBlob cloudBlobClient = container.GetBlobReference(fileName);
                string blobDetailsStr;
                using (var memoryStream = new MemoryStream())
                {
                    IRetryPolicy linearRetryPolicy = new LinearRetry(TimeSpan.FromSeconds(10), 5);
                    BlobRequestOptions requestOptions = new BlobRequestOptions()
                    {
                        RetryPolicy = linearRetryPolicy,
                    };
                    await cloudBlobClient.DownloadToStreamAsync(memoryStream, null, requestOptions, new OperationContext());
                    blobDetailsStr = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
                    blobDetails = JObject.Parse(blobDetailsStr);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"Failed to fetch the blob details for uri {storageAccountURI}. Exception details {ex}");
            }

            return blobDetails;
        }
    }
}
