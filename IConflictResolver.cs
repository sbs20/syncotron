using System.Threading.Tasks;

namespace Sbs20.Syncotron
{
    interface IConflictResolver
    {
        Task ResolveAsync(FileItemPair pair);
    }
}
