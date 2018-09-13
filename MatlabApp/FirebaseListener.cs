using System;
using System.Threading;
using FireSharp;
using FireSharp.Config;
using FireSharp.EventStreaming;

namespace MatlabApp
{
    internal class FirebaseListener
    {
        private static readonly string CANVAS= "/_canvas";
        private static readonly string CANVAS_TWOWAY = CANVAS + "/twoway";
        private static readonly string CANVAS_ERRORS = CANVAS + "/errors";
        private static readonly string STUDIO = "/_studio";
        private static readonly string STUDIO_CALLBACK = STUDIO + "/callback";

        private static readonly int WAIT_MILLISECONDS = 250;
        private static readonly int WAIT_TICKDOWN = 50;

        private FirebaseClient client;  // firebase client db
        private bool running;           // flag that the process is running
        private EventWaitHandle updateWait; // wait notify
        private bool updateAll;         // flag to update all dashboards
        private string name;            // name of model to load
        private string state;           // state for model
        private Model modelHandle;      // reference to the model
        private string lastRefresh;     // the last refresh
        private Thread updateThread;    // thread for updating the model;

        private int waitCount = 0;      // tick down when an update arrives
        private bool updateActive;      // is the model being updated? if so store any changes from firebase
        private bool resetActive;       // is the model being reset? if so store any changes from firebase
        private Pending pending;        // simple linked list of pending changes.
        private Object _lock = new Object();
        /// <summary>
        /// Create a firebase listener
        /// </summary>
        /// <param name="path"></param>
        /// <param name="secret"></param>
        internal FirebaseListener(string name, string secret, bool updateAll)
        {
            System.Console.WriteLine("create listener for " + name);
            var path = "https://" + name + ".firebaseio.com/";
            var config = new FirebaseConfig
            {
                BasePath = path,
                AuthSecret = secret
            };
            this.updateAll = updateAll;

            System.Console.WriteLine("... model");
            this.modelHandle = new Model(this.updateAll);
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
            Console.WriteLine("listen on " +  STUDIO_CALLBACK);
            // read everything from Firebase as the base state:
            var response = await this.client.OnAsync(STUDIO_CALLBACK,
                (sender, args, context) => { this.DataInsert(args); },
                (sender, args, context) => { this.DataUpdate(args); },
                (sender, args, context) => { });
            this.NotifyFirebase(CANVAS_TWOWAY, true);
        }


        private void SetFlag(bool update, bool reset)
        {
            lock (this._lock)
            {
                this.updateActive = update;
                if (!this.updateActive)
                {
                    this.pending = null;
                }
                this.resetActive = reset;

                if (update || reset)
                {
                    this.waitCount = WAIT_MILLISECONDS;
                }
            }
        }

        internal void Stop()
        {
            this.running = false;
            this.NotifyFirebase(CANVAS_TWOWAY, false);
            this.client = null;
            this.updateWait.Set();
        }

        // thread handler to update the model.
        internal void StartUpdateThread()
        {
            Console.WriteLine("Start update thread");
            while (this.running)
            {
                Console.WriteLine("...update waiting");
                this.updateWait.WaitOne();

                var ticks = 0;
                while (this.running && this.waitCount > 0)
                {
                    if (ticks % 5 == 0)
                    {
                        Console.WriteLine("  waiting...");
                    }
                    ticks++;

                    this.waitCount-=WAIT_TICKDOWN;
                    this.updateWait.WaitOne(WAIT_TICKDOWN);
                }
                if (this.running)
                {
                    Console.WriteLine("  updating...");
                    try
                    {
                        if (this.resetActive)
                        {
                            this.RunReset();
                        }
                        if (this.updateActive)
                        {
                            this.RunUpdate();
                        }
                    }
                    catch (Exception ex)
                    {
                        this.NotifyException("Update Model", ex);
                    }
                    finally
                    {
                        this.SetFlag(false, false);
                    }
                }
            }
        }

        private void RunNotify()
        {
            this.NotifyFirebase(STUDIO_CALLBACK, null);
            this.NotifyFirebase(CANVAS_ERRORS, this.modelHandle.Status);
        }

        private void RunReset()
        {
            Console.WriteLine("...reset running");
            this.GetModel().Reset();
            this.RunNotify();
            this.NotifyFirebase("_studio", null);
            Console.WriteLine("...reset complete");
        }

        private void RunUpdate()
        {
            Console.WriteLine("...update running");
            this.modelHandle.Reference = this.lastRefresh;
            this.modelHandle.Update(false);
            this.NotifyDone();
            while (this.pending != null)
            {
                // any updates received
                if (this.pending.Key != "/reference")
                {
                    this.lastRefresh = this.pending.Value;
                    this.modelHandle.Reference = this.lastRefresh;
                    this.modelHandle.Update(false);
                }
                else
                {
                    this.UpdateField(this.pending.Key, this.pending.Value);
                }
                this.NotifyDone();
                this.pending = this.pending.Next;
            }
            this.RunNotify();
            Console.WriteLine("...update ended");
        }

        private void NotifyDone()
        {
            // nothing to do here
        }

        /// <summary>
        /// Handle a data insert
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
                if (this.IsUpdateActive)
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

            Console.WriteLine("  UpdateField " + path + " = " + value);
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
                                this.SetFlag(true, false);
                                this.updateWait.Set();
                                this.lastRefresh = value;
                            }
                            return;
                        }
                    case "/reset":
                        {
                            var isSet = 0;
                            Int32.TryParse(value, out isSet);
                            if (isSet != 0)
                            {
                                this.SetFlag(true, true);
                                this.updateWait.Set();
                                this.lastRefresh = value;
                            }
                            return;
                        }
                }
                var parts = path.Substring(1).Split('/');
                if (parts.Length != 3 || parts[0] != "update")
                {
                    Console.Error.WriteLine("*** EXCEPTION Unhandled field " + path);
                    return;
                }
                var block = parts[1];
                var tunable = parts[2];

                var model = this.GetModel();
                model.UpdateField(block, tunable, value);
                this.waitCount = WAIT_MILLISECONDS;
                this.updateWait.Set();
            }
            catch (Exception ex)
            {
                this.NotifyException("UpdateField", ex);
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

        private bool IsResetActive
        {
            get
            {
                lock (this._lock)
                {
                    return this.resetActive;
                }
            }
        }

        private bool IsUpdateActive
        {
            get
            {
                lock (this._lock)
                {
                    return this.updateActive && this.waitCount == 0;
                }
            }
        }

        private void NotifyFirebase(string path, Object value)
        {
            try
            {
                if (value == null)
                {
                    this.client.Set(path, "{}");
                }
                else
                {
                    this.client.Set(path, value);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("*** EXCEPTION " + ex.Message + '\n' + ex.Source);
            }
        }

        /// <summary>
        /// Notify an exception in the listener
        /// </summary>
        /// Write a message to the consol and, if available, back to firebase
        /// <param name="source"></param>
        /// <param name="ex"></param>
        private void NotifyException(string source, Exception ex)
        {
            var msg = source + '\n' + 
                ex.Message + '\n' + 
                ex.Source;

            Console.Error.WriteLine("*** EXCEPTION " + msg);
            //if (this.client == null)
            //{
            //    return;
            //}
            //this.NotifyFirebase("_error", msg);
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
