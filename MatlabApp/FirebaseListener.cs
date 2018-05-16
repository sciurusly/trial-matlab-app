using System;
using FireSharp;
using FireSharp.Config;
using FireSharp.EventStreaming;

namespace MatlabApp
{
    internal class FirebaseListener
    {
        private FirebaseClient client;  // firebase client db
        private string root;            // the root key within the db
        private string name;            // name of model to load
        private string state;           // state for model
        private Model modelHandle;      // reference to the model
        private bool updating;          // are we inserting [from a new input] or updating [requires a round trip]

        /// <summary>
        /// Create a firebase listener
        /// </summary>
        /// <param name="path"></param>
        /// <param name="secret"></param>
        /// <param name="root"></param>
        internal FirebaseListener(string name, string secret, string root)
        {
            System.Console.WriteLine("create");
            var path = "https://" + name + ".firebaseio.com/";
            this.root = root;
            var config = new FirebaseConfig
            {
                BasePath = path,
                AuthSecret = secret
            };
            this.client = new FirebaseClient(config);
            this.modelHandle = new Model();
        }

        internal async void Listen()
        {
            this.updating = false;
            var fullPath = root + "/_callback";
            Console.WriteLine("listen " + fullPath);
            // read everything from Firebase as the base state:
            var response = await this.client.OnAsync(fullPath,
                (sender, args, context) => { this.DataInsert(args); },
                (sender, args, context) => { this.DataUpdate(args); },
                (sender, args, context) => { });
        }

        /// <summary>
        /// Handle a datat insert
        /// </summary>
        /// <param name="args">even holding data to insert</param>
        private void DataInsert(ValueAddedEventArgs args)
        {
            this.updating = false;
            Console.Out.WriteLine("INS:" + args.Path + "=" + args.Data);
            this.ProcessField(args.Path, args.Data);
        }

        /// <summary>
        /// Handle an update
        /// </summary>
        /// <param name="args">update event</param>
        private void DataUpdate(ValueChangedEventArgs args)
        {
            this.updating = true;
            Console.Out.WriteLine("UPD:" + args.Path + "=" + args.Data);
            this.ProcessField(args.Path, args.Data);
        }

        /// <summary>
        /// Process the date from an insert or update
        /// </summary>
        /// <param name="path">The path to the data</param>
        /// <param name="value">the string value of the data</param>
        private void ProcessField(String path, string value)
        {
            try
            {

                switch (path)
                {
                    case "/model":
                        {
                            this.name = value;
                            this.GetModel(); // make sure it's loaded
                            return;
                        }
                    case "/state":
                        {
                            this.state = value;
                            this.GetModel(); // make sure it's loaded
                            return;
                        }
                    case "/post":
                        {
                            this.GetModel().PostBlock = value;
                            return;
                        }
                }
                var parts = path.Substring(1).Split('/');
                if (parts.Length != 4 || parts[0] != "update")
                {
                    Console.Error.WriteLine("Unhandled field " + path);
                    return;
                }
                var block = parts[1];
                var tunable = parts[2];
                var field = parts[3];

                var model = this.GetModel();
                model.Updating = this.updating;
                model.UpdateField(block, tunable, field, value);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Exception " + ex.Message + '\n' + ex.Source);
            }
        }

        /// <summary>
        /// Get the handle to the current model
        /// </summary>
        /// If the name or state have changed then the new state is loaded
        /// <returns>the currently referenced model</returns>
        private Model GetModel()
        {
            if (this.name == null || this.state == null)
            {
                return this.modelHandle;
            }
            if (this.modelHandle.Name != this.name ||
                    this.modelHandle.State != this.state)
            {
                Console.WriteLine("GetModel.LoadState");
                this.modelHandle.LoadState(this.name, this.state);
            }
            return this.modelHandle;
        }
    }
}
