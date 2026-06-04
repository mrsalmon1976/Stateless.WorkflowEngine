using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Example.Shared
{
    internal class Prompts
    {
        public static string GetInputStoreType()
        {
            const string msg = @"What store type would you like to use?
            * MongoDb [" + Constants.StoreTypeMongoDb + @"]
            * RavenDb [" + Constants.StoreTypeRavenDb + @"]
            * Back [Enter]";
            Console.WriteLine(msg);

            while (true)
            {
                string? input = Console.ReadLine()?.ToUpper();

                if (string.IsNullOrEmpty(input))
                    return string.Empty;

                switch (input)
                {
                    case Constants.StoreTypeMongoDb:
                    case Constants.StoreTypeRavenDb:
                        return input;
                    default:
                        Console.WriteLine("Invalid input - please try again");
                        break;
                }
            }
        }
    }
}
