using System;
using FireSharp;
using System.Threading;

namespace MatlabApp
{
    /// <summary>
    /// Sends updates to the sender.
    /// All the messages are queued and sent in the order they are recieved.
    /// </summary>
    internal class StudioSender : StudioSession
    {
        private Message message;
        private bool running;

        private EventWaitHandle updateWait;   // wait to update studio
        private Thread updateThread;          // thread for updating studio
        private object _lock = new object();  // lock for adding and removing messages

        internal StudioSender(string name, string secret) : base(name, secret)
        {
            Logger.Write(1, "StudioSender." + name);
        }

        internal void AddMessage(string key, object value)
        {
            Logger.Write(5, "StudioSender.AddMessage." + key);
            this.AddMessagePending(key, value);
            this.Notify();
        }

        internal void AddMessagePending(string key, object value)
        {
            Logger.Write(6, "StudioSender.AddMessagePending." + key);
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
        }

        /// <summary>
        /// Notify the sender that it needs to check it's messages.
        /// The calling process can submit messages, but must then call notify to start sending then
        /// </summary>
        internal void Notify()
        {
            Logger.Write(4, "StudioSender.Notify");
            this.updateWait.Set();
        }

        internal bool ProcessMessage()
        {
            Message msg = null;
            lock (this._lock)
            {
                if (this.message == null)
                {
                    return false;
                }
                // take the first one
                msg = this.message;
                this.message = msg.Next;
                msg.Send(this.client);
                return true;
            }
        }

        /// <summary>
        /// Is the sender running.
        /// Is it still alive or are there messages to dispose of
        /// at shut down.
        /// </summary>
        private bool Running
        {
            get
            {
                if (!this.running)
                {
                    lock (this._lock)
                    {
                        // empty the queue first
                        return (this.message != null);
                    }
                }
                return true;
            }
        }

        internal void Start()
        {
            Logger.Write(2, "StudioSender.Start");
            this.message = null;
            this.running = true;

            this.updateWait = new AutoResetEvent(false);
            this.updateThread = new Thread(UpdateStudio);
            this.updateThread.Start();
        }

        private void UpdateStudio()
        {
            Logger.Write(3, "StudioSender.UpdateStudio.Start");
            while (this.Running)
            {
                if (!this.ProcessMessage())
                {
                    // the queue is now empty so wait
                    // should not come in here when running flag is false
                    if (this.running)
                    {
                        this.updateWait.WaitOne();
                    }
                }
            }
            Logger.Write(3, "StudioSender.UpdateStudio.Done");
            this.client = null;
        }

        internal void Stop()
        {
            Logger.Write(2, "StudioSender.Stop");
            this.running = false;
            this.Notify();
        }
    }
}
