using Microsoft.Azure.Cosmos.Table;

namespace GeneticAlghoritmAzF.Enities
{
    public class Parent : TableEntity
    {
        public string PopRowKey { get; set; }
    }
}