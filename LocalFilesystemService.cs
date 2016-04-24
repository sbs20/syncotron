using System;
using System.IO;
using System.Threading.Tasks;

namespace Sbs20.Syncotron
{
    public class LocalFilesystemService : IFileItemProvider
    {
        public Task DeleteAsync(FileItem file)
        {
            return Task.Run(() =>
            {
                var localItem = file.Object as FileSystemInfo;
                localItem.Delete();
            });
        }

        public async Task ForEachAsync(string path, bool recursive, bool deleted, Action<FileItem> action)
        {
            await Task.Run(() =>
            {
                DirectoryInfo root = new DirectoryInfo(path);
                if (!root.Exists)
                {
                    return;
                }

                //this.Add(new FileItem(root));
                SearchOption option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

                Parallel.ForEach(root.EnumerateFiles("*", option), f =>
                {
                    var item = FileItem.Create(f);
                    action(item);
                });

                Parallel.ForEach(root.EnumerateDirectories("*", option), d =>
                {
                    var item = FileItem.Create(d);
                    action(item);
                });
            });
        }

        public Task MoveAsync(FileItem file, string desiredPath)
        {
            throw new NotImplementedException();
        }
    }
}
