using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.TestHarness
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
    }
}
