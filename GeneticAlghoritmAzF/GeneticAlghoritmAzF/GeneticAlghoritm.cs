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
            log.LogInformation("C# HTTP trigger function processed a request.");
            if (!Int32.TryParse(req.Query["popCount"], out int popCount))
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);
                popCount = data?.popCount;
            }
            population.CreateIfNotExists();
            var batchInsertOperation = new TableBatchOperation();
            for (int i = 0; i < popCount; i++)
            {
                batchInsertOperation.InsertOrReplace(new Population()
                {
                    PartitionKey = "1",
                    RowKey = i.ToString(),
                    Value = _random.Next(0, 100)
                });
            }
            await population.ExecuteBatchAsync(batchInsertOperation);
            populationFitness.Add("1");
            return popCount != null
                ? (ActionResult)new OkObjectResult($"Initialize first population with count: {popCount}")
                : new BadRequestObjectResult("Please pass a popCount on the query string or in the request body");
        }
    }
}