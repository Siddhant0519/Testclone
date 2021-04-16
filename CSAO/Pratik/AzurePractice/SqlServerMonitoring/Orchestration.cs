using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace SqlServerMonitoring
{
    public static class Orchestration
    {
        [FunctionName("Orchestration")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var outputs = new List<string>();
            dynamic eventDetails = JsonConvert.DeserializeObject(context.GetInput<string>());
            outputs.Add(await context.CallActivityAsync<string>("SqlSecurityMOnitor", eventDetails));
           
            return outputs;
        }

    }
}