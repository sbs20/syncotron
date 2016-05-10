using System;
using System.Threading.Tasks;

namespace Sbs20.Syncotron
{
    public interface IFileItemProvider
    {
        Task<string> ForEachAsync(string path, bool recursive, bool deleted, Action<FileItem> action);
        Task<string> ForEachContinueAsync(string cursor, Action<FileItem> action);
        Task MoveAsync(FileItem file, string desiredPath);
        Task DeleteAsync(FileItem file);
    }
}
