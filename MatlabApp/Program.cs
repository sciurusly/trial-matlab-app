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
            if (args.Length != 3)
            {
                Console.WriteLine("Invalid command arguments: path secret root");
                return;
            }
            var path = args[0];
            var secret = args[1];
            var root = args[2];
            var listener = new FirebaseListener(path, secret, root);
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
            catch (Exception EX)
            {
                Console.WriteLine("Error in listener");
                Console.WriteLine(EX.Message);
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
