using System.Collections.Generic;
using System.Linq;

namespace GeneticAlghoritmAzF
{
    public static class Extension
    {
        public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> list, int batchSize)

        {
            int i = 0;

            return list.GroupBy(x => (i++ / batchSize)).ToList();
        }
    }
}