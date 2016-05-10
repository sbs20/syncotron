using System;
using System.Threading.Tasks;

namespace Sbs20.Syncotron
{
    public interface ICloudService : IFileItemProvider
    {
        bool IsAuthorised { get; }
        Uri StartAuthorisation();
        Task FinishAuthorisation(string code);
        Task DownloadAsync(FileItem fileItem, String localName);
        Task DownloadAsync(FileItem file);
        Task UploadAsync(FileItem file);
    }
}
