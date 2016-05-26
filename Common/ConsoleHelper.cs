using System;
using System.Collections.Generic;

namespace Sbs20.Common
{
    public class ConsoleHelper
    {
        public static string ConsoleReadVariable(string promptText)
        {
            Console.Write(promptText);
            return Console.ReadLine();
        }

        public static void ConsoleGetUsernamePassword(string promptText, out string username, out string password)
        {
            Console.WriteLine(promptText);
            username = ConsoleReadVariable("Username: ");
            password = ConsoleReadVariable("Password: ");
        }

        public static IDictionary<string, string> ReadArguments(string[] args)
        {
            var arguments = new Dictionary<string, string>();
            string key = null;
            string val = null;

            for (int i = 0; i < args.Length; i++)
            {
                string element = args[i];

                if (element.StartsWith("-"))
                {
                    key = element.Substring(1);
                    val = null;
                }
                else
                {
                    val = val == null ? element : val + " " + element;
                }

                if (!string.IsNullOrEmpty(key))
                {
                    if (i == args.Length - 1)
                    {
                        arguments.Add(key, val);
                    }
                    else if (args[i + 1].StartsWith("-"))
                    {
                        arguments.Add(key, val);
                    }
                }
                else
                {
                    // This is bad. Key should never be empty by here
                }
            }

            return arguments;
        }
    }
}
