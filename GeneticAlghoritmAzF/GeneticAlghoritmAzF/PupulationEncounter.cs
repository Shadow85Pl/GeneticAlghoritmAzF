using GeneticAlghoritmAzF.Enities;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GeneticAlghoritmAzF
{
    public static class PupulationEncounter
    {
        [FunctionName("PupulationEncounterOrchestrator")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context

            )
        {
            var population = context.GetInput<IList<Population>>();
            var taskList = from pop in population
                           select context.CallActivityAsync<string>("PupulationEncounter_Hello", pop);
            return new List<string>(await Task.WhenAll(taskList.ToArray()));
        }

        [FunctionName("PupulationEncounter_Hello")]
        public static string SayHello([ActivityTrigger] Population pop, ILogger log)
        {
            return $"Hello {pop.Value}!";
        }

        [FunctionName("PupulationEncounter")]
        public static async Task HttpStart(
            [QueueTrigger("population-generation")]string populationGeneration,
            [DurableClient]IDurableOrchestrationClient starter,
             [Table("population")] CloudTable population,
            ILogger log)
        {
            TableQuery<Population> rangeQuery = new TableQuery<Population>().Where(
                   TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal,
                       populationGeneration));
            var pop = await population.ExecuteQuerySegmentedAsync(rangeQuery, null);
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync<IList<Population>>("PupulationEncounterOrchestrator", pop.Results);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
        }
    }
}