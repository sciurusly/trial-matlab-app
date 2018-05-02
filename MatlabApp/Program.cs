using System;
using FireSharp;
using FireSharp.Config;
using FireSharp.EventStreaming;

namespace MatlabApp
{
    class Program
    {
        static void Main(string[] args)
        {
            //System.Console.WriteLine("api " + System.DateTime.Now);
            //// creating an instance of the API class loads up the environment.
            //// this includes the overhead of the MATLAB runtime.
            //var api = new API();
            //System.Console.WriteLine("load " + System.DateTime.Now);
            //// The api handle is passed into Model to handle working with the model:
            //var model = new Model(api, "API Test", "State (4)");
            //System.Console.WriteLine("set " + System.DateTime.Now);
            //for (int i = 11; i < 15; i++)
            //{
            //    System.Console.WriteLine("set to " + i + " " + System.DateTime.Now);
            //    model.UpdateNumber("InitialAssets", i * 10000000);
            //}
            //System.Console.WriteLine("clear " + System.DateTime.Now);
            //// this clears the model from the api, worth calling during shutdown
            //// or if changing the required model.
            //api.Clear();
            //System.Console.WriteLine("done " + System.DateTime.Now);
            var listener = new FirebaseListener();
            listener.Listen();
            Console.WriteLine("Listening for changes");
            Console.WriteLine("Press Q to quit");
            var waiting = true;
            while (waiting)
            {
                var key = Console.ReadKey();
                waiting = !(key.Key == ConsoleKey.Q);
            }
            Console.WriteLine("Exit");
            listener = null;
        }
    }

    internal class FirebaseListener
    {
        protected const string FIREBASE_PATH = "https://fc-dashboard.firebaseio.com/";
        protected const string FIREBASE_SECRET = "ZMbxpDIqZSVfCoTLVss1jafW0JhYQF2ejHw65wT7";
        private FirebaseClient client;
        private string name;
        private string state;
        private Model modelHandle;
        private bool updating;
        internal FirebaseListener()
        {
            System.Console.WriteLine("create");
            var config = new FirebaseConfig
            {
                BasePath = FirebaseListener.FIREBASE_PATH,
                AuthSecret = FirebaseListener.FIREBASE_SECRET
            };
            this.modelHandle = new Model();
            this.client = new FirebaseClient(config);
        }

        internal void Listen()
        {
            this.updating = false;
            this.ListenOnPath(null);
        }

        private async void ListenOnPath(string path)
        {
            var fullPath = "investible_benchmark/model_params";
            if (path != null)
            {
                fullPath = fullPath + path;
            }
            Console.WriteLine("listen " + fullPath);
            // read everything from Firebase as the base state:
            var response = await this.client.OnAsync(fullPath,
                (sender, args, context) => { this.DataInsert(path, args); },
                (sender, args, context) => { this.DataUpdate(path, args); },
                (sender, args, context) => { });
            //response = null;
        }

        private void DataInsert(string path, ValueAddedEventArgs args)
        {
            if (path != null)
            {
                return; // already got from earlier call
            }
            this.updating = false;
            Console.Out.WriteLine("INS:" + args.Path + "=" + args.Data);
            this.ProcessField(args.Path, args.Data);
        }

        private void DataUpdate(string path, ValueChangedEventArgs args)
        {
            this.updating = true;
            var argPath = path == null ? args.Path : path + args.Path;
            Console.Out.WriteLine("UPD:" + argPath + "=" +args.Data);
            this.ProcessField(argPath, args.Data);
         //   this.GetModel().UpdateNumber("InitialAssets", 1.1e9);
        }

        private void ProcessField(String path, object value)
        {
            System.Console.WriteLine("process");
            try
            {
                path = path.Substring(1); // strip the '/'
                switch (path)
                {
                    case "model":
                        {
                            this.name = (string)value;
                            return;
                        }
                    case "state":
                        {
                            this.state = (string)value;
                            return;
                        }
                }
                System.Console.WriteLine("process.model");
                var model = this.GetModel();
                model.Updating = this.updating;
                var parts = path.Split('/');
                if (parts.Length != 3)
                {
                    Console.Error.WriteLine("Ignored field " + path);
                    return;
                }
                var type = parts[0];
                var block = parts[1];
                var tunable = parts[2];

                switch (type)
                {
                    case "update":
                        {
                            Console.WriteLine("process.update");
                            model.UpdateField(block, tunable, value);
                            return;
                        }
                    case "post":
                        {
                            Console.WriteLine("process.post");
                            model.PostField(block, tunable, value);
                            return;
                        }
                    default:
                        {
                            Console.Error.WriteLine("Unhandled field " + path);
                            return;
                        }
                }
            }
            catch(Exception ex)
            {
                Console.Error.WriteLine("Exception " + ex.Message + '\n' + ex.Source);
            }
        }
        private Model GetModel()
        {
            if (this.name == null || this.state == null)
            {
                return this.modelHandle;
            }
            return this.GetModel(this.name, this.state);
        }

        private Model GetModel(string model, string state)
        {
            if (this.updating && (this.modelHandle.Name != model || 
                    this.modelHandle.State != state))
            {
                Console.WriteLine("GetModel.LoadState");
                this.modelHandle.LoadState(model, state);
            }
            return this.modelHandle;
        }
    }
}
