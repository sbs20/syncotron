using System;
using System.Threading.Tasks;

namespace Sbs20.Syncotron
{
    public interface IFileItemProvider
    {
        Task<FileItem> FileSelect(string path);
        Task<string> ForEachAsync(string path, bool recursive, bool deleted, Action<FileItem> action);
        Task<string> ForEachContinueAsync(string cursor, Action<FileItem> action);
        Task<string> LatestCursor(string path, bool recursive, bool deleted);
        Task MoveAsync(FileItem file, string desiredPath);
        Task DeleteAsync(string path);
    }
}
