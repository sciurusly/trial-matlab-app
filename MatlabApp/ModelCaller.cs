using System;
using System.Threading;

namespace MatlabApp
{
    /// <summary>
    /// Manages the Canvas Model object so that it can update.
    /// Results are sent to the StudioSender
    /// </summary>
    internal class ModelCaller
    {
        private StudioSender sender;
        private string name;            // name of model to load
        private string state;           // state for model
        private Model model;            // the model handle
        private string lastRefresh;     // the last refresh
        private int waitCount;
        private bool updateAll;
        private EventWaitHandle modelWait;  // wait to send heartbeat
        private Thread modelThread;         // thread for sending heartbeat
        private bool running;

        private volatile bool _working = false;
        internal bool IsWorking
        {
            get
            {
                return this._working;
            }

            private set
            {
                this._working = value;
                this.sender.AddMessage(Studio.CANVAS_WORKING, this._working);
                Logger.Write(4, "ModelCaller.IsWorking=" + this._working);
            }
        }
        private ModelUpdateType updateType;
        private Message message;
        private object _lock = new object();

        internal ModelCaller(StudioSender sender, bool updateAll)
        {
            Logger.Write(1, "ModelCaller");
            this.sender = sender;
            this.updateAll = updateAll;
            this.model = new Model(this.updateAll);
        }

        internal void AddMessage(string key, string value)
        {
            Logger.Write(6, "ModelCaller.AddMessage." + key + '.' + value);
            var msg = new Message(key, value);
            lock(this._lock)
            {
                if (this.message == null)
                {
                    this.message = msg;
                }
                else
                {
                    this.message.Add(msg);
                }
            }
            // and let's go process it!
            this.modelWait.Set();
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
                return this.model;
            }
            if (this.model.Name != this.name ||
                    this.model.State != this.state)
            {
                this.model.LoadState(this.name, this.state);
            }
            return this.model;
        }

        internal Message PopMessage()
        {
            lock (this._lock)
            {
                if (this.message == null)
                {
                    return null;
                }
                var msg = this.message;
                Logger.Write(6, "ModelCaller.PopMessage." + msg.Key);
                this.message = msg.Next;
                return msg;
            }
        }

        private bool ProcessMessage()
        {
            var msg = this.PopMessage();
            if (msg == null)
            {
                return false;
            }
            Logger.Write(4, "ModelCaller.ProcessMessage.Start");
            while (msg != null)
            {
                this.UpdateField(msg.Key, msg.Value);
                if (this.updateType != ModelUpdateType.None && this.waitCount <= 0)
                {
                    break;
                }
                msg = this.PopMessage();
                while (msg == null && this.waitCount > 0)
                {
                    this.waitCount -= Studio.WAIT_TICKDOWN;
                    this.modelWait.WaitOne(Studio.WAIT_TICKDOWN);
                    msg = this.PopMessage();
                }
            }
            this.waitCount = 0;
            Logger.Write(4, "ModelCaller.ProcessMessage.Done");
            return true;
        }

        private void ProcessUpdate()
        {
            // we need to update the model in some way;
            Logger.Write(3, "ModelCaller.ProcessUpdate." + this.updateType);
            this.IsWorking = true;

            var model = this.GetModel();
            switch (this.updateType)
            {
                case ModelUpdateType.Reset:
                {
                        model.Reset();
                        break;
                }
                case ModelUpdateType.Revert:
                    {
                        model.Revert();
                        break;
                    }
                case ModelUpdateType.Update:
                    {
                        model.Reference = this.lastRefresh;
                        model.Update(false);
                        break;
                    }
            }
            var status = this.model.Status;
            this.sender.AddMessagePending(Studio.STUDIO_CALLBACK, null);
            this.sender.AddMessagePending(Studio.CANVAS_ERRORS, status);
            this.sender.Notify();

            this.updateType = ModelUpdateType.None;
            this.IsWorking = false;
            Logger.Write(3, "ModelCaller.ProcessUpdate.Done");
        }
        internal void Start()
        {
            Logger.Write(2, "ModelCaller.Start");
            this.message = null;
            this.running = true;

            this.modelWait= new AutoResetEvent(false);
            this.modelThread = new Thread(UpdateModel);
            this.modelThread.Start();
        }

        internal void Stop()
        {
            Logger.Write(2, "ModelCaller.Stop");
            this.running = false;
            this.modelWait.Set();
        }

        private void UpdateField(string path, object obj)
        {
            string value = obj == null ? null : obj.ToString();

            Logger.Write(6, "ModelCaller.UpdateField" + path + "=" + value);
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
                            this.lastRefresh = value;
                            this.updateType = ModelUpdateType.Update;
                            this.waitCount = Studio.WAIT_MILLISECONDS;
                            return;
                        }
                    case "/reset":
                        {
                            var isSet = 0;
                            Int32.TryParse(value, out isSet);
                            if (isSet != 0)
                            {
                                this.updateType = ModelUpdateType.Reset;
                            }
                            this.waitCount = 0;
                            return;
                        }
                    case "/revert":
                        {
                            var isSet = 0;
                            Int32.TryParse(value, out isSet);
                            if (isSet != 0)
                            {
                                this.updateType = ModelUpdateType.Revert;
                            }
                            this.waitCount = 0;
                            return;
                        }
                }
                var parts = path.Substring(1).Split('/');
                if (parts.Length != 3 || parts[0] != "update")
                {
                    Logger.Write(0,"*** EXCEPTION Unhandled field " + path);
                    return;
                }
                var block = parts[1];
                var tunable = parts[2];

                var model = this.GetModel();
                model.UpdateField(block, tunable, value);
                this.waitCount = Studio.WAIT_MILLISECONDS;
                Logger.Write(6, "ModelCaller.UpdateField.Waiting");
            }
            catch (Exception ex)
            {
                Logger.Error("ModelCaller.UpdateField", ex);
            }
        }

        private void UpdateModel()
        {
            this.IsWorking = false;
            this.updateType = ModelUpdateType.None;
            this.waitCount = 0;
            Logger.Write(3, "ModelCaller.UpdateModel.Start");
            while (this.running)
            {
                // process any changes
                if (this.ProcessMessage())
                {
                    this.ProcessUpdate();
                }
                else
                {
                    // wait for next notify
                    Logger.Write(3, "ModelCaller.UpdateModel.Wait");
                    this.modelWait.WaitOne();
                }
            }
            Logger.Write(3, "ModelCaller.UpdateModel.Done");
        }

        private enum ModelUpdateType
        {
            None,
            Update,
            Revert,
            Reset
        }
    }
}
