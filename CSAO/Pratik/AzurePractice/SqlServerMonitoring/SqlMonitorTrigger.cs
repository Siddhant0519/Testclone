using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace SqlServerMonitoring
{
    public static class SqlMonitorTrigger
    {
        [FunctionName("SqlMonitorTrigger")]
        public static async Task<IActionResult> RunAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,[DurableClient]IDurableOrchestrationClient client,
            ILogger log)
        {           
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            string instanceId = await client.StartNewAsync<string>("Orchestration", requestBody);
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
            
            return client.CreateCheckStatusResponse(req, instanceId);
        }
    }
}
