using GeneticAlghoritmAzF.Enities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GeneticAlghoritmAzF
{
    public class GeneticAlghoritm
    {
        private Random _random { get; }

        public GeneticAlghoritm(Random random)
        {
            _random = random;
        }

        [FunctionName("GeneticAlghoritm_Init")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            [Table("population")] CloudTable population,
            [Queue("population-fitness")] ICollector<string> populationFitness,
            ILogger log)
        {
            log.LogInformation("Genetic Alghoritm initialization");
            try
            {
                if (!Int32.TryParse(req.Query["popCount"], out int popCount))
                {
                    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                    dynamic data = JsonConvert.DeserializeObject(requestBody);
                    popCount = data?.popCount ?? throw new Exception("Please pass a popCount on the query string or in the request body");
                }
                log.LogInformation($"Generation with {popCount} items");
                var batchInsertOperation = new TableBatchOperation();
                Enumerable.Range(1, popCount).Select(a => new { key = a, value = _random.Next(0, 100) }).ToList().ForEach(a => batchInsertOperation.InsertOrReplace(new Population()
                {
                    PartitionKey = "1",
                    RowKey = a.key.ToString(),
                    Value = a.value
                }));
                await population.ExecuteBatchAsync(batchInsertOperation);
                populationFitness.Add("1");
                return new OkObjectResult($"Initialize first population with count: {popCount}");
            }
            catch (Exception Ex)
            {
                return new BadRequestObjectResult(Ex.Message);
            }
        }
    }
}