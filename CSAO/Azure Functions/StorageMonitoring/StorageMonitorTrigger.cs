using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace StorageMonitoring
{
    public static class StorageMonitorTrigger
    {
        [FunctionName("StorageMonitorTrigger")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req, [DurableClient]IDurableOrchestrationClient starter,
            ILogger log)
        {
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

                string instanceId = await starter.StartNewAsync<string>("Orchestrator", requestBody);
                log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
                return starter.CreateCheckStatusResponse(req, instanceId);
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult("Exception occured while executing the Azure Function. Exception details - " + ex);
            }
        }
    }
}
