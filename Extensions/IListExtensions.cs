using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sbs20.Extensions
{
    public static class IListExtensions
    {
        public static void Add<T>(this IList<T> list, IEnumerable<T> addand)
        {
            foreach (var t in addand)
            {
                list.Add(t);
            }
        }
    }
}
