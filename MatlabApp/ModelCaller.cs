using System;
using System.Threading;
using Sciurus.FinancialCanvas.Logging;

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
        private EventWaitHandle modelWait;  // wait to update the model
        private Thread modelThread;         // thread for updating the model
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
                Logger.Log.Write(3, "ModelCaller.IsWorking:" + value);
                this._working = value;
                this.process.NotifyHeartBeat();
            }
        }
        private ModelUpdateType updateType;
        private Message message;
        private object _lock = new object();
        private bool success;
        private string[] reply;
        private bool replyReceived;
        private StudioListener process;

        internal ModelCaller(StudioListener process, StudioSender sender, int port, bool updateAll)
        {
            Logger.Log.Write(1, "ModelCaller");
            this.process = process;
            this.sender = sender;
            this.updateAll = updateAll;
            this.model = new Model(this, port, this.updateAll);
        }

        internal void AddMessage(string key, string value)
        {
            Logger.Log.Write(6, "ModelCaller.AddMessage:" + key + '.' + value);
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
                    Logger.Log.Write(6, "ModelCaller.PopMessage:[null]");
                    return null;
                }
                var msg = this.message;
                Logger.Log.Write(6, "ModelCaller.PopMessage:" + msg.Key);
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
            Logger.Log.Write(4, "ModelCaller.ProcessMessage.Start");
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
            Logger.Log.Write(4, "ModelCaller.ProcessMessage.Done");
            return this.updateType != ModelUpdateType.None;
        }

        private void ProcessUpdate()
        {
            // we need to update the model in some way;
            Logger.Log.Write(3, "ModelCaller.ProcessUpdate:" + this.updateType);
            this.IsWorking = true;
            this.replyReceived = false;
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
            // this is an assync call, we need to wait for the callback:
            var status = this.model.Status;
            Logger.Log.Write(3, "ModelCaller.ProcessUpdate.Done");
        }

        internal void NotifyReply(bool success, string[] reply)
        {
            this.success = success;
            this.reply = reply;
            this.replyReceived = true;
            this.modelWait.Set();
        }
        /// <summary>
        /// Process the reply from canvas
        /// </summary>
        /// <returns></returns>
        private void ProcessReply()
        {
            if (!this.replyReceived)
            {
                return;
            }
            this.sender.AddMessage(Studio.STUDIO_CALLBACK, null);
            if (this.success)
            {
                this.sender.AddMessage(Studio.CANVAS_ERRORS, null);
            }
            else
            {
                this.sender.AddMessage(Studio.CANVAS_ERRORS, this.reply);
            }
            this.IsWorking = false;
            this.replyReceived = false;
            this.updateType = ModelUpdateType.None;
        }

        internal void Start()
        {
            Logger.Log.Write(2, "ModelCaller.Start");
            this.message = null;
            this.running = true;
            this.model.Start();
            this.modelWait= new AutoResetEvent(false);
            this.modelThread = new Thread(UpdateModel);
            this.modelThread.Start();
        }

        internal void Stop()
        {
            Logger.Log.Write(2, "ModelCaller.Stop");
            this.running = false;
            this.model.Stop();
            this.modelWait.Set();
        }

        private void UpdateField(string path, object obj)
        {
            string value = obj == null ? null : obj.ToString();

            Logger.Log.Write(6, "ModelCaller.UpdateField:" + path + "=" + value);
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
                    Logger.Log.Write(0,"*** EXCEPTION Unhandled field " + path);
                    return;
                }
                var block = parts[1];
                var tunable = parts[2];

                var model = this.GetModel();
                model.UpdateField(block, tunable, value);
                this.waitCount = Studio.WAIT_MILLISECONDS;
                Logger.Log.Write(6, "ModelCaller.UpdateField.Waiting");
            }
            catch (Exception ex)
            {
                Logger.Log.Error("ModelCaller.UpdateField", ex);
            }
        }

        private void UpdateModel()
        {
            this.IsWorking = false;
            this.updateType = ModelUpdateType.None;
            this.waitCount = 0;
            Logger.Log.Write(3, "ModelCaller.UpdateModel.Start");
            while (this.running)
            {
                var wait = true;
                if (this.IsWorking)
                {
                    Logger.Log.Write(9, "ModelCaller.UpdateModel.Reply");
                    this.ProcessReply();
                }
                else
                {
                    // process any changes
                    Logger.Log.Write(9, "ModelCaller.UpdateModel.Message");
                    if (this.ProcessMessage())
                    {
                        Logger.Log.Write(9, "ModelCaller.UpdateModel.Update");
                        this.ProcessUpdate();
                        wait = false;
                    }
                }
                if (wait)
                {
                    // wait for next notify
                    Logger.Log.Write(9, "ModelCaller.UpdateModel.Wait");
                    this.modelWait.WaitOne();
                    Logger.Log.Write(9, "ModelCaller.UpdateModel.Wake");
                }
            }
            Logger.Log.Write(3, "ModelCaller.UpdateModel.Done");
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
