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
            string storeType = "";
            const string Exit = "X";
            const string msg = @"What stype type would you like to use?
            * MongoDb [" + Constants.StoreTypeMongoDb + @"]
            * RavenDb [" + Constants.StoreTypeRavenDb + @"]
            * Exit [" + Exit + "]";
            Console.WriteLine(msg);
            while (String.IsNullOrEmpty(storeType))
            {
                string? input = Console.ReadLine()?.ToUpper();
                switch (input)
                {
                    case Constants.StoreTypeMongoDb:
                    case Constants.StoreTypeRavenDb:
                    case Exit:
                        storeType = input;
                        break;
                    default:
                        Console.WriteLine("Invalid input - please try again");
                        continue;
                }
            }

            if (storeType == Exit)
            {
                Console.WriteLine("Exit selected.  Hit enter to continue.");
                Console.ReadLine();
                Environment.Exit(0);

            }

            return storeType;
        }
    }
}
