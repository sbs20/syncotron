using System;
using System.IO;
using System.Text;

namespace Sbs20.Syncotron
{
    class DateTimeSizeHash : IHashProvider
    {
        public string Hash(FileInfo fileInfo)
        {
            string s = fileInfo.LastWriteTimeUtc.ToString("o") + ":" + fileInfo.Length;
            byte[] b = Encoding.UTF8.GetBytes(s);
            return Convert.ToBase64String(b);
        }
    }
}
