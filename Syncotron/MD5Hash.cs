using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Sbs20.Syncotron
{
    public class MD5Hash : IHashProvider
    {
        public byte[] HashBytes(Stream stream)
        {
            using (var md5 = MD5.Create())
            {
                return md5.ComputeHash(stream);
            }
        }

        public byte[] HashBytes(FileInfo fileInfo)
        {
            using (var stream = fileInfo.OpenRead())
            {
                return this.HashBytes(stream);
            }
        }

        public byte[] HashBytes(string s)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(s)))
            {
                return this.HashBytes(stream);
            }
        }

        public string Hash(FileInfo fileInfo)
        {
            return Convert.ToBase64String(this.HashBytes(fileInfo));
        }
    }
}
