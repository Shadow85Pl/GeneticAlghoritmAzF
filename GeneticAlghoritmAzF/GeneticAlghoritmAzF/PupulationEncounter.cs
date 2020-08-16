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
        private Random _random { get; }

        public PupulationEncounter(Random random)
        {
            _random = random;
        }

        [FunctionName("PupulationEncounterOrchestrator")]
        public async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            [Table("population")] CloudTable storage,
            [Queue("population-rullet")] ICollector<string> populationRulet)
        {
            var population = context.GetInput<IList<Population>>();
            var taskList = from pop in population
                           select context.CallActivityAsync<Population>("PupulationEncounter_FitnessFunction", pop);
            var encountedpopulation = new List<Population>(await Task.WhenAll(taskList.ToArray()));
            var populationFitnessSum = encountedpopulation.Sum(a => a.Fitness);
            //var rulet = new List<int>();
            var batchUpdateOperation = new TableBatchOperation();
            foreach (var pop in encountedpopulation)
            {
                pop.Adaptation = 1 - (pop.Fitness / populationFitnessSum); //WHY 1- ??
                //rulet.AddRange(Enumerable.Repeat(encountedpopulation.IndexOf(pop), (int)Math.Round(pop.Adaptation * 100, 0)));
                batchUpdateOperation.Replace(pop);
            }

            storage.ExecuteBatch(batchUpdateOperation);
            populationRulet.Add(encountedpopulation.FirstOrDefault().PartitionKey);
            //var parents = new List<int>(rulet.OrderBy(x => _random.Next()).Take(encountedpopulation.Count));
        }

        [FunctionName("PupulationEncounter_FitnessFunction")]
        public static Population FitnessFunction([ActivityTrigger] Population pop, ILogger log)
        {
            //f(x)=-(x-4)^3+(x+1)^2-30
            pop.Fitness = -Math.Pow(pop.Value - 4, 3) + Math.Pow(pop.Value + 1, 2) - 30;
            return pop;
        }

        [FunctionName("PupulationEncounter")]
        public static async Task HttpStart(
            [QueueTrigger("population-fitness")]string populationFitness,
            [DurableClient]IDurableOrchestrationClient starter,
             [Table("population")] CloudTable population,
            ILogger log)
        {
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