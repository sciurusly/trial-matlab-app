using System;

namespace MatlabApp
{
    /// <summary>
    /// Trial matlab api app
    /// </summary>
    /// Takes data from firebase and loadds the related canvas model to allow round trip updates
    class Program
    {
        /// <summary>
        /// Entry point for app
        /// </summary>
        /// <param name="args">Requires three arguments for db, secret and root</param>
        static void Main(string[] args)
        {
            Logger.Write(1, "Financial Canvas Studio Listener");
            string message = null;
            var path = "path-name";
            var secret = "api-secret";
            var updateAll = true;
            if (args.Length < 2 || args.Length > 4)
            {
                message = "Incorect number of parameters";
            }
            else
            {
                // check all the arguments in order
                path = args[0];
                secret = args[1];
                if (args.Length > 2)
                {
                    switch (args[2])
                    {
                        case "all":
                            {
                                break;
                            }
                        case "current":
                            {
                                updateAll = false;
                                break;
                            }
                        default:
                            {
                                message = "Invalid update flag; must be 'all' or 'current'";
                                break;
                            }
                    }
                }
                if (args.Length > 3 && message == null)
                {
                    int log;
                    if (Int32.TryParse(args[3], out log))
                    {
                        if (log < 0 || log > 9)
                        {
                            message = "Invalid log level; must be 0 - 9";
                        }
                        else
                        {
                            Logger.Level = log;
                        }
                    }
                    else
                    {
                        message = "Cannot convert log level to a number; must be 0 - 9";
                    }
                }
            }
            if (message != null)
            {
                Console.Error.WriteLine(message);
                Console.Error.WriteLine("Valid arguments are: path secret [all] [log]\nwhere:");
                Console.Error.WriteLine("\tpath - the name of the firebase database; eg 'dev-financial-canvas-studio'");
                Console.Error.WriteLine("\tsecret - the authentication secret for the firebase database");
                Console.Error.WriteLine("\tall - 'all'/'current' if the api should update all dashboards or the current one; default is 'all'");
                Console.Error.WriteLine("\tlog - a number from 0 to 9 to set the logging level, 0 is errors, 9 is full debug; default is 3");
                return;
            }
 
            // create the listener and run it.
            var listener = new StudioListener(path, secret, updateAll);
            listener.Listen();
        }
    }
}
