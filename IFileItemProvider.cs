using System;
using System.Threading.Tasks;

namespace Sbs20.Syncotron
{
    interface IFileItemProvider
    {
        Task ForEachAsync(string path, bool recursive, bool deleted, Action<FileItem> action);
        Task MoveAsync(FileItem file, string desiredPath);
        Task DeleteAsync(FileItem file);
    }
}
