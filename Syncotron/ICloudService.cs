using System;
using System.Threading.Tasks;

namespace Sbs20.Syncotron
{
    public interface ICloudService : IFileItemProvider
    {
        string CurrentAccountEmail { get; }
        bool IsAuthorised { get; }
        Uri StartAuthorisation();
        void FinishAuthorisation(string code);
        Task DownloadAsync(FileItem fileItem, String localName);
        Task DownloadAsync(FileItem file);
        Task UploadAsync(FileItem file);
    }
}
