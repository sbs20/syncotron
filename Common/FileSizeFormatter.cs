using System;

namespace Sbs20.Common
{
    public class FileSizeFormatter
    {
        public static string Format(ulong size)
        {
            ulong kb = 1 << 10;
            ulong mb = kb << 10;

            if (size < 0)
            {
                return "0";
            }
            else if (size == 1)
            {
                return "1 Byte";
            }
            else if (size < kb << 1)
            {
                return size + " Bytes";
            }
            else if (size < mb << 1)
            {
                return ((int)(size / kb)) + " KB";
            }
            else
            {
                return Math.Round(100.0 * size / mb) / 100.0 + " MB";
            }
        }
    }
}