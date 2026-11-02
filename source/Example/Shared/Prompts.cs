namespace Example.Shared
{
    /// <summary>
    /// Console prompts shared by the examples.  Every prompt re-asks until it gets a valid answer, and treats the end of
    /// the input (e.g. redirected input) as "back", so menus can never loop forever.
    /// </summary>
    internal class Prompts
    {
        /// <summary>
        /// Writes a section header.
        /// </summary>
        public static void WriteHeader(string title, string? subtitle = null)
        {
            string line = new string('=', Math.Max(title.Length, subtitle?.Length ?? 0) + 4);
            Console.WriteLine();
            ConsoleWriter.WriteLine(line, ConsoleColor.DarkCyan);
            ConsoleWriter.WriteLine($"  {title}", ConsoleColor.Cyan);
            if (subtitle != null)
            {
                ConsoleWriter.WriteLine($"  {subtitle}", ConsoleColor.Gray);
            }
            ConsoleWriter.WriteLine(line, ConsoleColor.DarkCyan);
        }

        /// <summary>
        /// Shows a numbered menu and returns the key of the chosen option.  Returns <c>backKey</c> at the end of the input.
        /// </summary>
        public static string Choose(string question, string backKey, params (string Key, string Label)[] options)
        {
            Console.WriteLine();
            ConsoleWriter.WriteLine(question, ConsoleColor.Yellow);
            foreach (var option in options)
            {
                ConsoleWriter.Write($"  [{option.Key}] ", ConsoleColor.Yellow);
                Console.WriteLine(option.Label);
            }

            while (true)
            {
                ConsoleWriter.Write("> ", ConsoleColor.Yellow);
                string? input = Console.ReadLine();
                if (input == null)
                {
                    return backKey;
                }
                input = input.Trim();
                if (options.Any(x => x.Key.Equals(input, StringComparison.OrdinalIgnoreCase)))
                {
                    return options.First(x => x.Key.Equals(input, StringComparison.OrdinalIgnoreCase)).Key;
                }
                ConsoleWriter.WriteLine($"Please enter one of: {String.Join(", ", options.Select(x => x.Key))}", ConsoleColor.Red);
            }
        }

        /// <summary>
        /// Asks for a value, returning the default if nothing is entered.
        /// </summary>
        public static string Ask(string question, string defaultValue)
        {
            ConsoleWriter.Write($"{question} [{defaultValue}]: ", ConsoleColor.Yellow);
            string? input = Console.ReadLine();
            return String.IsNullOrWhiteSpace(input) ? defaultValue : input.Trim();
        }

        /// <summary>
        /// Asks for a whole number of at least <c>minimum</c>, returning the default if nothing is entered.
        /// </summary>
        public static int AskInt(string question, int defaultValue, int minimum = 1)
        {
            while (true)
            {
                string input = Ask(question, defaultValue.ToString());
                if (int.TryParse(input, out int value) && value >= minimum)
                {
                    return value;
                }
                ConsoleWriter.WriteLine($"Please enter a whole number of at least {minimum}", ConsoleColor.Red);
            }
        }

        /// <summary>
        /// Asks a yes/no question, returning the default if nothing is entered.
        /// </summary>
        public static bool AskYesNo(string question, bool defaultValue)
        {
            while (true)
            {
                string input = Ask($"{question} (y/n)", defaultValue ? "y" : "n").ToLowerInvariant();
                if (input == "y" || input == "yes")
                {
                    return true;
                }
                if (input == "n" || input == "no")
                {
                    return false;
                }
                ConsoleWriter.WriteLine("Please enter y or n", ConsoleColor.Red);
            }
        }

        /// <summary>
        /// Waits for the user to press Enter.
        /// </summary>
        public static void Pause()
        {
            ConsoleWriter.Write("Press Enter to continue...", ConsoleColor.DarkGray);
            Console.ReadLine();
        }

        /// <summary>
        /// Asks which store to use, returning <c>Constants.StoreTypeMongoDb</c>, <c>Constants.StoreTypeRavenDb</c>, or
        /// an empty string to go back.
        /// </summary>
        public static string GetInputStoreType()
        {
            string choice = Choose("Which store would you like to use?", "0",
                ("1", "MongoDb"),
                ("2", "RavenDb"),
                ("0", "Back"));
            return choice switch
            {
                "1" => Constants.StoreTypeMongoDb,
                "2" => Constants.StoreTypeRavenDb,
                _ => String.Empty
            };
        }
    }
}
