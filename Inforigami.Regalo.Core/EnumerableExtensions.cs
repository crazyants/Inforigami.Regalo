using System.Collections.Generic;
using System.Linq;

namespace Inforigami.Regalo.Core
{
    public static class EnumerableExtensions
    {
        public static bool IsEmpty<TSource>(this IEnumerable<TSource> source)
         {
             return false == source.Any();
         }
    }
}
