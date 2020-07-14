using Microsoft.Azure.Cosmos.Table;

namespace GeneticAlghoritmAzF.Enities
{
    public class Population : TableEntity
    {
        public int Value { get; set; }
    }
}