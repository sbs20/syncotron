using System;
using System.IO;

namespace Sbs20.Syncotron
{
    public class DateTimeSizeHash : IHashProvider
    {
        public string Hash(FileInfo fileInfo)
        {
            byte[] b = new byte[16];
            Array.Copy(BitConverter.GetBytes(fileInfo.LastWriteTimeUtc.Ticks), b, 8);
            Array.Copy(BitConverter.GetBytes(fileInfo.Length), 0, b, 8, 8);
            return Convert.ToBase64String(b);
        }
    }
}
