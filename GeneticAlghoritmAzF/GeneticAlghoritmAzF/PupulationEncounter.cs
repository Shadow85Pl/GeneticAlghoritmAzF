using GeneticAlghoritmAzF.Enities;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GeneticAlghoritmAzF
{
    public class PupulationEncounter
    {
        [FunctionName("PupulationEncounterOrchestrator")]
        public async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            [Table("population")] CloudTable storage,
            [Table("generation")] CloudTable generation,
            [Queue("population-roulette")] ICollector<string> populationRulet)
        {
            var population = context.GetInput<IList<Population>>();
            var taskList = from pop in population
                           select context.CallActivityAsync<Population>("PupulationEncounter_FitnessFunction", pop);
            var encountedpopulation = new List<Population>(await Task.WhenAll(taskList.ToArray()));
            var populationFitnessSum = encountedpopulation.Sum(a => a.Fitness);

            var batchUpdateOperation = new TableBatchOperation();
            foreach (var pop in encountedpopulation)
            {
                pop.Adaptation = (pop.Fitness / populationFitnessSum);

                batchUpdateOperation.Replace(pop);
            }
            storage.ExecuteBatch(batchUpdateOperation);
            var generationNumber = encountedpopulation.FirstOrDefault().PartitionKey;
            TableOperation insertOrMergeOperation = TableOperation.InsertOrMerge(new Generation()
            {
                PartitionKey = generationNumber,
                RowKey = generationNumber,
                PopulationFitness = populationFitnessSum,
                BestResult = encountedpopulation.Where(a => a.Adaptation == encountedpopulation.Max(a => a.Adaptation)).Select(a => a.Value.ToString()).Aggregate((a, b) => a + ", " + b)
            });
            generation.Execute(insertOrMergeOperation);
            populationRulet.Add(generationNumber);
        }

        [FunctionName("PupulationEncounter_FitnessFunction")]
        public static Population FitnessFunction([ActivityTrigger] Population pop, ILogger log)
        {
            //f(x)=2sin(0.1x)+1
            pop.Fitness = 2 * Math.Sin(((Math.PI * pop.Value) / 180) * 0.1) + 1;
            return pop;
        }

        [FunctionName("PupulationEncounter")]
        public static async Task HttpStart(
            [QueueTrigger("population-fitness")]string populationFitness,
            [DurableClient]IDurableOrchestrationClient starter,
             [Table("population")] CloudTable population,
            ILogger log)
        {
            if (Int32.Parse(populationFitness) > 10)
                return;
            TableQuery<Population> rangeQuery = new TableQuery<Population>().Where(
                   TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal,
                       populationFitness));
            var pop = await population.ExecuteQuerySegmentedAsync(rangeQuery, null);
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync<IList<Population>>("PupulationEncounterOrchestrator", pop.Results);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
        }
    }
}