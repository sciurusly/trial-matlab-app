using System;
using System.Threading;
using FireSharp.EventStreaming;
using Sciurus.FinancialCanvas.Logging;
using System.Net.Http;

namespace MatlabApp
{
    /// <summary>
    /// Provides a listener to the Studio Firebase database
    /// When this has read an update from studio it notifies the ModelCaller to process the inputs
    /// </summary>
    internal class StudioReceiver : StudioSession
    {
        private bool running;
        private ModelCaller model;
        private Thread studioThread;

        internal StudioReceiver(ModelCaller model, string name, string secret) : base(name, secret)
        {
            Logger.Log.Write(1, "StudioReceiver." + name);
            this.model = model;
        }

        /// <summary>
        /// Handle a data insert
        /// </summary>
        /// <param name="args">even holding data to insert</param>
        private void DataInsert(ValueAddedEventArgs args)
        {
            if (!this.running)
            {
                return;
            }
            Logger.Log.Write(6, "StudioReceiver.DataInsert:" + args.Path + "=" + args.Data);
            this.model.AddMessage(args.Path, args.Data);
        }

        /// <summary>
        /// Handle an update
        /// </summary>
        /// <param name="args">update event</param>
        private void DataUpdate(ValueChangedEventArgs args)
        {
            if (!this.running)
            {
                return;
            }
            Logger.Log.Write(6, "StudioReceiver.DataUpdate:" + args.Path + "=" + args.Data);
            this.model.AddMessage(args.Path, args.Data);
        }

        private async void Listen()
        {
            Logger.Log.Write(4, "StudioReceiver.Listen:" + Studio.STUDIO_CALLBACK);
            // read everything from Firebase as the base state:
            try
            {
                var response = await this.client.OnAsync(Studio.STUDIO_CALLBACK,
                    (sender, args, context) => { this.DataInsert(args); },
                    (sender, args, context) => { this.DataUpdate(args); },
                    (sender, args, context) => { });
            }
            catch(HttpRequestException ex)
            {
                Logger.Log.Error("StudioReceiver.Listen", ex);
            }
        }

        internal void Start()
        {
            Logger.Log.Write(2, "StudioReceiver.Start");
            this.running = true;
            this.studioThread = new Thread(StudioCallback);
            this.studioThread.Start();
        }

        private void StudioCallback()
        {
            Logger.Log.Write(3, "StudioReceiver.StudioCallback");
            this.Listen();
        }

        internal void Stop()
        {
            Logger.Log.Write(2, "StudioReceiver.Stop");
            this.running = false;
            //this.studioThread.Abort();
            this.client = null;
        }
    }
}
