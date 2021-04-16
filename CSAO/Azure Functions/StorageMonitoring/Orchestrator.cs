using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace StorageMonitoring
{
    public static class Orchestrator
    {
        [FunctionName("Orchestrator")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            List<string> outputs = new List<string>();
            dynamic eventDetails = JsonConvert.DeserializeObject(context.GetInput<string>());
            if (eventDetails.eventData.operationName.ToString().ToLower().Equals("microsoft.storage/storageaccounts/write"))
            {
                outputs.Add(await context.CallActivityAsync<string>("StorageMonitor", context.GetInput<string>()));
                outputs.Add(await context.CallActivityAsync<string>("ContainerMonitor", context.GetInput<string>()));
                outputs.Add(await context.CallActivityAsync<string>("DiagnosticMonitor", context.GetInput<string>()));
                outputs.Add(await context.CallActivityAsync<string>("FileServiceMonitor", context.GetInput<string>()));
            }
            else if (eventDetails.eventData.operationName.ToString().ToLower().Equals("microsoft.storage/storageaccounts/blobservices/containers/write"))
            {
                outputs.Add(await context.CallActivityAsync<string>("ContainerMonitor", context.GetInput<string>()));
            }
            else if (eventDetails.eventData.operationName.ToString().ToLower().Equals("microsoft.storage/storageaccounts/fileservices/write"))
            {
                outputs.Add(await context.CallActivityAsync<string>("FileServiceMonitor", context.GetInput<string>()));
            }
            else if (eventDetails.eventData.operationName.ToString().ToLower().Equals("microsoft.storage/storageaccounts/delete"))
            {
                outputs.Add(await context.CallActivityAsync<string>("RemoveVnet", context.GetInput<string>()));
            }

            return outputs;
        }
    }
}