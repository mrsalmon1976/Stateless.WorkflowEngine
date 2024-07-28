using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NugetTestApp
{
    internal class ConsoleWriter
    {
        private static object _messageLock = new object();

        public static void WriteLine(string prefix, string message, ConsoleColor? color = null)
        {
            lock (_messageLock)
            {
                if (color != null)
                {
                    Console.ForegroundColor = color.Value;
                }
                Console.WriteLine($"{prefix}{message}");
                Console.ResetColor();
            }
        }

        public static void WriteLine(string message, ConsoleColor? color = null)
        {
            lock (_messageLock)
            {
                if (color != null)
                {
                    Console.ForegroundColor = color.Value;
                }
                Console.WriteLine(message);
                Console.ResetColor();
            }
        }

        public static void Write(string prefix, string message, ConsoleColor? color = null)
        {
            lock (_messageLock)
            {
                if (color != null)
                {
                    Console.ForegroundColor = color.Value;
                }
                Console.Write($"{prefix}{message}");
                Console.ResetColor();
            }
        }

        public static void Write(string message, ConsoleColor? color = null)
        {
            lock (_messageLock)
            {
                if (color != null)
                {
                    Console.ForegroundColor = color.Value;
                }
                Console.Write(message);
                Console.ResetColor();
            }
        }

    }
}
