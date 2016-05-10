using System;
using System.IO;
using System.Security.Cryptography;

namespace Sbs20.Syncotron
{
    public class MD5Hash : IHashProvider
    {
        public string Hash(FileInfo fileInfo)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = fileInfo.OpenRead())
                {
                    var hash = md5.ComputeHash(stream);
                    return Convert.ToBase64String(hash);
                }
            }
        }
    }
}
