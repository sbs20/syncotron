using System;

namespace Sbs20.Extensions
{
    public static class EnumExtensions
    {
        public static T ToEnum<T>(this string s)  where T : struct
        {
            T val = default(T);
            if (Enum.TryParse<T>(s, out val))
            {
                return val;
            }

            return val;
        }
    }
}
