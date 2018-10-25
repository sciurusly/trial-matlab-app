using System;
using System.Dynamic;
using Sciurus.FinancialCanvas.Logging;

namespace MatlabApp
{
    /// <summary>
    /// Trial matlab api app
    /// </summary>
    /// Takes data from firebase and loadds the related canvas model to allow round trip updates
    /// dev: dev-financial-canvas-studio 4475E20GW2jgWLZtagpCVo8C39VZlrOmUJ9mUitd
    /// william: fc-dashboard ZMbxpDIqZSVfCoTLVss1jafW0JhYQF2ejHw65wT7
    /// 
    class Program
    {
        /// <summary>
        /// Entry point for app
        /// </summary>
        /// <param name="args">Requires three arguments for db, secret and root</param>
        static void Main(string[] args)
        {
            Logger.Log.Write(1, "Financial Canvas Studio Listener");
            string message = null;
            var path = "path-name";
            var secret = "api-secret";
            var port = 13500;
            var updateAll = true;
            if (args.Length < 2 || args.Length > 5)
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
                    if (!Int32.TryParse(args[3], out port))
                    {
                        message = "Invalid port number; must be numeric";
                    }
                }
                if (args.Length > 4 && message == null)
                {
                    int log;
                    if (Int32.TryParse(args[4], out log))
                    {
                        if (log < 0 || log > 9)
                        {
                            message = "Invalid log level; must be 0 - 9";
                        }
                        else
                        {
                            Logger.Log.Level = log;
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
                Console.Error.WriteLine("Valid arguments are: path secret [all] [port] [log]\nwhere:");
                Console.Error.WriteLine("\tpath - the name of the firebase database; eg 'dev-financial-canvas-studio'");
                Console.Error.WriteLine("\tsecret - the authentication secret for the firebase database");
                Console.Error.WriteLine("\tall - 'all'/'current' if the api should update all dashboards or the current one; default is 'all'");
                Console.Error.WriteLine("\tport - the number of the port to connect to the canvas server; default is 13500");
                Console.Error.WriteLine("\tlog - a number from 0 to 9 to set the logging level, 0 is errors, 9 is full debug; default is 3");
                Console.Error.WriteLine("\n\t\t[press any key to exit]");
                Console.ReadKey();
                return;
            }
 
            // create the listener and run it.
            var listener = new StudioListener(path, secret, port, updateAll);
            listener.Listen();
        }
    }
}
