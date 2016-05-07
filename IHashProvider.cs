using System.IO;

namespace Sbs20.Syncotron
{
    public interface IHashProvider
    {
        string Hash(FileInfo fileInfo);
    }
}
