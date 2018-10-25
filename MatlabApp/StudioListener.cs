using System;
using System.Threading;
using Sciurus.FinancialCanvas.Logging;

namespace MatlabApp
{
    /// <summary>
    /// Manage the components of the studio listener
    /// Controls the console input and listens for a 'Q' to quit!
    /// </summary>
    internal class StudioListener
    {
        private ModelCaller model;
        private string name;
        private StudioReceiver receiver;
        private string secret;
        private StudioSender sender;
        private bool updateAll;
        private EventWaitHandle heartbeatWait;  // wait to send heartbeat
        private Thread heartbeatThread;         // thread for sending heartbeat
        private bool running;
        private int shortHeartbeat = 1; // first heartbeat is quick to sync it all up
        private int port;

        internal StudioListener(string name, string secret, bool updateAll) : 
            this (name, secret, 13500, updateAll) { }

        internal StudioListener(string name, string secret, int port, bool updateAll)
        {
            Logger.Log.Write(1,"StudioListener." + name);
            this.name = name;
            this.secret = secret;
            this.updateAll = updateAll;
            this.port = port;
        }

        internal void Listen()
        {
            try {
                this.StartProcessing();
                var waiting = true;
                // this does just go out to the console:
                Console.Out.WriteLineAsync("        Studio Listener Running");
                Console.Out.WriteLineAsync("        on " + this.name);
                Console.Out.WriteLineAsync("        [Press Q to quit or a number to change logging]");
                while (waiting)
                {
                    var key = Console.ReadKey();
                    Console.Out.WriteLine();
                    var log = "0123456789".IndexOf(key.KeyChar);
                    if (log != -1)
                    {
                        Logger.Log.Level = log;
                    }
                    else
                    {
                        waiting = !(key.Key == ConsoleKey.Q);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error in listener");
                Console.Error.WriteLine(ex.Message);
            }
            finally
            {
                Console.Out.WriteLine("Exit");
                this.StopProcessing();
            }
        }

        /// <summary>
        /// Thread to send a heartbeat message;
        /// The update to studio lets it know that the is still
        /// </summary>
        private void SendHeartbeat()
        {
            Logger.Log.Write(3, "StudioListener.SendHeartbeat.Start");
            this.sender.AddMessage(Studio.CANVAS_TWOWAY, true);
            while (this.running)
            {
                var heartbeat = DateTime.Now;
                Logger.Log.Write(3, "StudioListener.SendHeartbeat.Send." + heartbeat);
                bool working = this.model.IsWorking;

                this.sender.AddMessage(Studio.CANVAS_LISTENING, heartbeat);
                this.sender.AddMessage(Studio.CANVAS_WORKING, working);
                if (working)
                {
                    this.shortHeartbeat = 5;
                }

                if (this.shortHeartbeat > 0)
                {
                    this.heartbeatWait.WaitOne(Studio.WAIT_SHORT_HEARTBEAT);
                    this.shortHeartbeat--;
                }
                else
                {
                    this.heartbeatWait.WaitOne(Studio.WAIT_HEARTBEAT);
                }
            }
            Logger.Log.Write(3, "StiudioListener.SendHeartbeat.Stopping");
            this.sender.AddMessage(Studio.CANVAS_WORKING, false);
            this.sender.AddMessage(Studio.CANVAS_TWOWAY, false);
            this.sender.AddMessage(Studio.CANVAS_LISTENING, null);
            this.sender.Stop();
            Logger.Log.Write(3, "StiudioListener.SendHeartbeat.Stopped");
        }

        internal void NotifyHeartBeat()
        {
            if (this.heartbeatWait == null)
            {
                return;
            }
            this.heartbeatWait.Set();
        }

        private void StartProcessing()
        {
            Logger.Log.Write(2, "StiudioListener.StartProcessing");
            // create the instances
            this.sender = new StudioSender(this.name, this.secret);
            // before we start the other two, clear callback
            this.sender.Start();
            this.sender.AddMessage(Studio.STUDIO_CALLBACK, null);

            this.model = new ModelCaller(this, this.sender, this.port, this.updateAll);
            this.receiver = new StudioReceiver(this.model, this.name, this.secret);
            this.model.Start();
            this.receiver.Start();

            // finally set up the heartbeat thread:
            this.heartbeatWait = new AutoResetEvent(false);
            this.running = true;
            this.heartbeatThread = new Thread(SendHeartbeat);
            this.heartbeatThread.Start();
    }

    private void StopProcessing()
        {
            Logger.Log.Write(2, "StiudioListener.StopProcessing");
            if (!this.running)
            {
                return;
            }
            this.running = false;
            this.heartbeatWait.Set();
            
            this.receiver.Stop();
            this.model.Stop();
            this.sender.Stop();
        }
    }
}
