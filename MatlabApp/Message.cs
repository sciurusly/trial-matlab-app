using System;
using FireSharp;
using Sciurus.FinancialCanvas.Logging;

namespace MatlabApp
{
    /// <summary>
    /// Represents a message item
    /// A message has a name and value and an optional next message
    /// </summary>
    internal class Message
    {
        internal string Key { get; private set; }
        internal object Value { get; private set; }
        internal Message Next { get; private set; }

        internal Message(string key, object value)
        {
            Logger.Log.Write(8, "Message." + key);
            this.Key = key;
            this.Value = value;
            this.Next = null;
        }

        internal void Add(Message add)
        {
            if (this.Next == null)
            {
                Logger.Log.Write(9, "Message.Add." + this.Key + '.' + add.Key);
                this.Next = add;
            }
            else
            {
                this.Next.Add(add);
            }
        }

        internal void Send(FirebaseClient client)
        {
            try
            {
                Logger.Log.Write(9, "Message.Send:" + this.Key);
                var result = client.Set(this.Key,
                    this.Value == null ? "{}" : this.Value);
                
                Logger.Log.Write(9, "Message.Send.Done:" + result.StatusCode);
            }
            catch (Exception ex)
            {
                // this should not ever happen ??
                Logger.Log.Error("Message.Send:" + this.Key, ex);
            }
        }
    }
}
