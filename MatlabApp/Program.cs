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
            if (args.Length < 2 || args.Length > 3)
            {
                Console.WriteLine("Invalid command arguments: path secret [all]");
                Console.WriteLine("  path - the name of the firebase database; eg 'dev-financial-canvas-studio'");
                Console.WriteLine("  secret - the authentication secret for the firebase database");
                Console.WriteLine("  all - 'all'/'current' if the api should update all dashboards or the current one; default is 'all'");
                return;
            }
            var path = args[0];
            var secret = args[1];
            var updateAll = (args.Length <= 3 || args[2] == "all");

            var listener = new FirebaseListener(path, secret, updateAll);
            try
            {
                listener.Listen();
                Console.WriteLine("Listening for changes");
                var waiting = true;
                Console.WriteLine("[Press Q to quit]");
                while (waiting)
                {
                    var key = Console.ReadKey();
                    Console.WriteLine();
                    waiting = !(key.Key == ConsoleKey.Q);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error in listener");
                Console.Error.WriteLine(ex.Message);
            }
            finally
            {
                Console.WriteLine("Exit");
                listener.Stop();
                listener = null;
            }
        }
    }
}
