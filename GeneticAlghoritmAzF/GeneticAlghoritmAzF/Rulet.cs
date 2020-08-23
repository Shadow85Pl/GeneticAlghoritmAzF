using GeneticAlghoritmAzF.Enities;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GeneticAlghoritmAzF
{
    public class Roulette
    {
        private Random _random { get; }

        public Roulette(Random random)
        {
            _random = random;
        }

        [FunctionName("Roulette")]
        public async Task RouletteFunction(
            [QueueTrigger("population-roulette")]string populationRoulette,
            [Table("population")] CloudTable population,
            [Table("parents")] CloudTable parents,
            [Queue("population-childs")] ICollector<string> populationChilds,
            ILogger log)
        {
            TableQuery<Population> rangeQuery = new TableQuery<Population>().Where(
                   TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal,
                       populationRoulette));
            var pop = await population.ExecuteQuerySegmentedAsync(rangeQuery, null);

            var rulet = new List<string>(pop.SelectMany(a => Enumerable.Repeat(a.RowKey, (int)Math.Round(a.Adaptation * 100, 0))));
            var batchUpdateOperation = new TableBatchOperation();
            int i = 1;
            var par = new List<Parent>(rulet.OrderBy(x => _random.Next()).Take(pop.Count()).Select(a => new Parent()
            {
                PartitionKey = populationRoulette,
                RowKey = i++.ToString(),
                PopRowKey = a
            }));
            par.ForEach(a => batchUpdateOperation.Insert(a));
            await parents.ExecuteBatchAsync(batchUpdateOperation);
            populationChilds.Add(populationRoulette);
        }
    }
}