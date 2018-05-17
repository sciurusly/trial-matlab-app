using System;
using System.Threading;
using FireSharp;
using FireSharp.Config;
using FireSharp.EventStreaming;

namespace MatlabApp
{
    internal class FirebaseListener
    {
        private FirebaseClient client;  // firebase client db
        private bool running;           // flag that the process is running
        private EventWaitHandle updateWait; // wait notify
        private string root;            // the root key within the db
        private string name;            // name of model to load
        private string state;           // state for model
        private Model modelHandle;      // reference to the model
        private string lastRefresh;     // the last refresh
        private Thread updateThread;    // thread for updating the model;
        private bool updateActive;   // is the model being updated? if so store any changes from firebase
        private Pending pending;        // simple linked list of pending changes.
        private Object _lock = new Object();
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
            System.Console.WriteLine("... model");
            this.modelHandle = new Model();
            System.Console.WriteLine("... firebase");
            this.client = new FirebaseClient(config);
            System.Console.WriteLine("... handler");
            this.updateWait = new AutoResetEvent(false);
            this.updateThread = new Thread(StartUpdateThread);
            this.running = true;
            this.updateThread.Start();
        }

        internal async void Listen()
        {
            var fullPath = this.root + "/_callback";
            Console.WriteLine("listen " + fullPath);
            // read everything from Firebase as the base state:
            var response = await this.client.OnAsync(fullPath,
                (sender, args, context) => { this.DataInsert(args); },
                (sender, args, context) => { this.DataUpdate(args); },
                (sender, args, context) => { });
        }

        internal void Stop()
        {
            this.running = false;
            this.updateWait.Set();
        }

        // thread hadnler to update the model.
        internal void StartUpdateThread()
        {
            Console.WriteLine("Start update thread");
            while (this.running)
            {
                Console.WriteLine("...update waiting");
                this.updateWait.WaitOne();
                if (this.running)
                {
                    try
                    {
                        Console.WriteLine("...update running");
                        this.modelHandle.Reference = this.lastRefresh;
                        this.modelHandle.Update();
                        this.NotifyDone();
                        while (this.pending != null)
                        {
                            // any updates received
                            if (this.pending.Key != "/reference")
                            {
                                this.lastRefresh = this.pending.Value;
                                this.modelHandle.Reference = this.lastRefresh;
                                this.modelHandle.Update();
                                this.NotifyDone();
                            }
                            else
                            {
                                this.UpdateField(this.pending.Key, this.pending.Value);
                            }
                            this.pending = this.pending.Next;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("Exception " + ex.Message + '\n' + ex.Source);
                    }
                    finally
                    {
                        lock (this._lock)
                        {
                            this.updateActive = false;
                            this.pending = null;
                        }
                    }
                }
            }
            Console.WriteLine("...update ended");
        }

        private void NotifyDone()
        {
            // nothing to do here
        }

        /// <summary>
        /// Handle a datat insert
        /// </summary>
        /// <param name="args">even holding data to insert</param>
        private void DataInsert(ValueAddedEventArgs args)
        {
            Console.Out.WriteLine("INS:" + args.Path + "=" + args.Data);
            this.ProcessField(args.Path, args.Data);
        }

        /// <summary>
        /// Handle an update
        /// </summary>
        /// <param name="args">update event</param>
        private void DataUpdate(ValueChangedEventArgs args)
        {
            Console.Out.WriteLine("UPD:" + args.Path + "=" + args.Data);
            this.ProcessField(args.Path, args.Data);
        }

        /// <summary>
        /// Process the date from an insert or update
        /// </summary>
        /// <param name="path">The path to the data</param>
        /// <param name="value">the string value of the data</param>
        private void ProcessField(string path, string value)
        {
            lock (this._lock)
            {
                if (this.updateActive)
                {
                    var p = new Pending(path, value);
                    if (this.pending == null)
                    {
                        this.pending = p;
                    }
                    else
                    {
                        this.pending.Add(p);
                    }
                    return;
                }
                this.UpdateField(path, value);
            }
        }

        private void UpdateField(string path, string value)
        {

            Console.WriteLine("UpdateField " + path + " = " + value);
            try
            {

                switch (path)
                {
                    case "/FolderName":
                        {
                            this.name = value;
                            this.GetModel(); // make sure it's loaded
                            return;
                        }
                    case "/StateFile":
                        {
                            this.state = value;
                            this.GetModel(); // make sure it's loaded
                            return;
                        }
                    case "/SourceBlock":
                        {
                            this.GetModel().PostBlock = value;
                            return;
                        }
                    case "/reference":
                        {
                            if (this.lastRefresh != value)
                            {
                                // notify we're ready
                                this.updateWait.Set();
                                this.lastRefresh = value;
                            }
                            return;
                        }
                }
                var parts = path.Substring(1).Split('/');
                if (parts.Length != 3 || parts[0] != "update")
                {
                    Console.Error.WriteLine("Unhandled field " + path);
                    return;
                }
                var block = parts[1];
                var tunable = parts[2];

                var model = this.GetModel();
                model.UpdateField(block, tunable, value);
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

    internal class Pending
    {
        internal Pending(string key, string value)
        {
            this.Key = key;
            this.Value = value;
        }

        internal void Add(Pending add)
        {
            if (this.Next == null)
            {
                this.Next = add;
            }
            else
            {
                this.Next.Add(add);
            }
        }
        internal string Key { get; set; }
        internal string Value { get; set; }
        internal Pending Next { get; set; }
    }

}
