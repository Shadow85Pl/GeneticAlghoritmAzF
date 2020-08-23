using GeneticAlghoritmAzF.Enities;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeneticAlghoritmAzF
{
    public class Childs
    {
        private Random _random { get; }

        public Childs(Random random)
        {
            _random = random;
        }

        [FunctionName("ChildsOrchestrator")]
        public async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            [Table("population")] CloudTable storage,
            [Queue("population-fitness")] ICollector<string> populationFitness,
            ILogger log)
        {
            var parents = context.GetInput<IList<Population>>();
            log.LogInformation($"Get parents in couples");
            var parentPairs = parents.Batch(2);
            log.LogInformation($"Run Parents combining");
            var taskList = parentPairs.Select(a => context.CallActivityAsync<IList<int>>("Child_CreateChilds", a));
            var setOfChilds = await Task.WhenAll(taskList.ToArray());
            log.LogInformation($"Run Mutation alghoritm");
            var taskMutantsList = setOfChilds.SelectMany(a => a).Select(a => context.CallActivityAsync<int>("Child_Mutate", a));
            var setOfFinalChilds = await Task.WhenAll(taskMutantsList.ToArray());
            var batchInsertOperation = new TableBatchOperation();
            var generation = Int32.Parse(parents.First().PartitionKey);
            generation++;
            log.LogInformation($"Save new generation {generation} to DB");
            setOfFinalChilds.Select((a, index) => new { Child = a, index }).ToList().ForEach(a => batchInsertOperation.Insert(new Population()
            {
                PartitionKey = generation.ToString(),
                RowKey = a.index.ToString(),
                Value = a.Child
            }));
            storage.ExecuteBatch(batchInsertOperation);
            populationFitness.Add(generation.ToString());
        }

        [FunctionName("Child_CreateChilds")]
        public IList<int> CreateChilds([ActivityTrigger] IList<Population> pop, ILogger log)
        {
            if (pop.Count == 1)
            {
                log.LogInformation("Single parent, return as a child");
                return pop.Select(a => a.Value).ToList();
            }
            else
            {
                log.LogInformation("Combine parent");
                var encodedValues = pop.Select(a => Convert.ToString(a.Value, 2)).ToArray();
                var maxLength = encodedValues.Max(a => a.Length);
                log.LogInformation("Set same length for genotype");
                encodedValues = encodedValues.Select(a => a.PadLeft(maxLength, '0')).ToArray();
                log.LogInformation("Get split Index");
                var splitIndex = _random.Next(maxLength);
                log.LogInformation("Combine parents to new Childs");
                return new List<string>() {
                    encodedValues[0].Substring(0, splitIndex) + encodedValues[1].Substring(splitIndex),
                    encodedValues[1].Substring(0, splitIndex) + encodedValues[0].Substring(splitIndex)
                }.Select(a => Convert.ToInt32(a, 2)).ToList();
            }
        }

        [FunctionName("Child_Mutate")]
        public int MutateChilds([ActivityTrigger] int pop, ILogger log)
        {
            if (_random.Next(100) < Int32.Parse(Environment.GetEnvironmentVariable("MutationPercentage") ?? "10"))
            {
                log.LogInformation($"Mutation for value {pop}");
                var byteString = Convert.ToString(pop, 2);
                log.LogInformation($"Draw bit to mutation");
                var mutatedByte = _random.Next(byteString.Length);
                StringBuilder sb = new StringBuilder(byteString);
                log.LogInformation($"Mutate bit");
                sb[mutatedByte] = byteString[mutatedByte].Equals('1') ? '0' : '1';
                byteString = sb.ToString();
                log.LogInformation($"Return new mtated value {Convert.ToInt32(byteString, 2)}");
                return Convert.ToInt32(byteString, 2);
            }
            else return pop;
        }

        [FunctionName("Childs")]
        public static async Task HttpStart(
            [QueueTrigger("population-childs")]string populationChilds,
            [DurableClient]IDurableOrchestrationClient starter,
             [Table("population")] CloudTable population,
             [Table("parents")] CloudTable parents,
            ILogger log)
        {
            log.LogInformation($"Start Child select for {populationChilds} generation");
            TableQuery<Population> rangeQuery = new TableQuery<Population>().Where(
                   TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal,
                       populationChilds));
            TableQuery<Parent> parentQuery = new TableQuery<Parent>().Where(
                   TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal,
                       populationChilds));
            var pop = await population.ExecuteQuerySegmentedAsync(rangeQuery, null);
            var par = await parents.ExecuteQuerySegmentedAsync(parentQuery, null);
            var parList = par.Select(a => pop.Single(b => b.RowKey.Equals(a.PopRowKey)));
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync<IList<Population>>("ChildsOrchestrator", parList.ToList());

            log.LogInformation($"Started Child select orchestration for {populationChilds} generation with ID = '{instanceId}'.");
        }
    }
}