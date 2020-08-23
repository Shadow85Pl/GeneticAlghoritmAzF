using Microsoft.Azure.Cosmos.Table;

namespace GeneticAlghoritmAzF.Enities
{
    public class Generation : TableEntity
    {
        public double PopulationFitness { get; set; }
        public string BestResult { get; set; }
    }
}