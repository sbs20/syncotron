using System;

namespace Sbs20.Syncotron
{
    class Logger
    {
        private static void Write(string s)
        {
            //Console.WriteLine(s);
        }

        public static void debug(string a, string b)
        {
            Write(a + ": " + b);
        }

        public static void verbose(string a, string b)
        {
            Write(a + ": " + b);
        }

        public static void info(string a, string b)
        {
            Write(a + ": " + b);
        }

        public static void debug(object a, string b)
        {
            debug(a.GetType().Name, b);
        }

        public static void verbose(object a, string b)
        {
            verbose(a.GetType().Name, b);
        }

        public static void info(object a, string b)
        {
            info(a.GetType().Name, b);
        }
    }
}
