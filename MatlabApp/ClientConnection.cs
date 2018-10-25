using System;
using System.Collections.Generic;
using Sciurus.FinancialCanvas.API;
using Sciurus.FinancialCanvas.Logging;
using System.Text;

namespace MatlabApp
{
    internal class ClientConnection
    {
        internal ClientGateway Canvas { get; private set; } // connection into the API
        internal Model Model { get; private set; }
        private List<string> actions = new List<string>();
         
        internal ClientConnection(Model model, int port)
        {
            this.Model = model;
            this.Canvas = new ClientGateway(port);
            this.Canvas.OnRead += this.Canvas_OnRead;
            this.Canvas.OnReadError += this.Canvas_OnReadError;
            this.Canvas.OnWrite += this.Canvas_OnWrite;
            this.Canvas.OnWriteError += this.Canvas_OnWriteError;
        }

        private void Canvas_OnRead(object source, GatewayEventArgs e)
        {
            Logger.Log.Write(9, "ClientConnection.Canvas_OnRead:" + e.Json);
            var json = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Dictionary<string, object>>(e.Json);

            var success = (bool)json["success"];
            var reply = json["reply"];
            string[] message;
            if (reply.GetType().Name == "String")
            {
                message = new string[] { (string)reply };
            }
            else
            {
                var col = (System.Collections.ArrayList)reply;
                message = new string[col.Count];
                col.CopyTo(message);
            }
            this.Model.NotifyReply(success, message);
        }

        private void Canvas_OnReadError(object source, GatewayEventArgs e)
        {
            Logger.Log.Write(9, "ClientConnection.Canvas_OnReadError:" + e.Json);
            this.Model.NotifyReply(false, new string[] { "Unable to read result from canvas" });
        }

        private void Canvas_OnWrite(object source, GatewayEventArgs e)
        {
            Logger.Log.Write(9, "ClientConnection.Canvas_OnWrite:" + e.Json);
        }

        private void Canvas_OnWriteError(object source, GatewayEventArgs e)
        {
            Logger.Log.Write(9, "ClientConnection.Canvas_OnWriteError:" + e.Json);
        }

        /// <summary>
        /// Write to the canvas API
        /// </summary>
        /// <param name="json"></param>
        private void WriteToCanvas(string json)
        {
            try
            {
                this.Canvas.Write(json);
            }
            catch (Exception ex)
            {
                Logger.Log.Error("ClientConnection.WriteToCanvas:" + json, ex);
            }
        }

        internal void ClearActions()
        {
            this.actions.Clear();
        }

        internal void UpdateBlock(Block bl)
        {
            var sb = new StringBuilder();
            sb.Append(@"{ ""action"" : ""update"", ""data"" : { ""block"" : """)
                .Append(bl.Name)
                .Append(@""", ""tunables"" : [ ");
            var addComma = false;
            foreach(KeyValuePair<String, Field> kv in bl.tunables)
            {
                if (addComma)
                {
                    sb.Append(@", ");
                }
                else
                {
                    addComma = true;
                }
                var f = kv.Value;
                sb.Append(@"{ ""name"" : """)
                    .Append(f.Name)
                    .Append(@""", ""value"" : """)
                    .Append(f.Value)
                    .Append(@""" }");
            }
            sb.Append(@" ] } }");
            this.actions.Add(sb.ToString());
        }

        internal void Refresh(string block, bool external)
        {
            this.actions.Add(
                new StringBuilder()
                    .Append(@"{ ""action"" : ""refresh"", ""data"" : { ""block"" : """)
                    .Append(block)
                    .Append(@""", ""external"" : ")
                    .Append(external ? @"1" : @"0")
                    .Append(@" } }")
                    .ToString());
        }
        internal void Refresh(string block, string external)
        {
            this.actions.Add(
                new StringBuilder()
                    .Append(@"{ ""action"" : ""refresh"", ""data"" : { ""block"" : """)
                    .Append(block)
                    .Append(@""", ""external"" : """)
                    .Append(external)
                    .Append(@""" } }")
                    .ToString());
        }

        internal void SendActions()
        {
            var sb = new StringBuilder();
            if (this.actions.Count == 1)
            {
                sb.Append(this.actions[0]);
            }
            else
            {
                sb.Append(@"{ ""action"" : ""steps"", ""data"" : [ ");

                // add them in
                for (int i = 0; i < this.actions.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(@", ");
                    }
                    sb.Append(this.actions[i]);
                }

                sb.Append(@" ] }");
            }
            this.WriteToCanvas(sb.ToString());
        }

        internal void LoadState(string model, string state)
        {
            this.actions.Add(
                new StringBuilder()
                    .Append(@"{ ""action"" : ""loadState"", ""data"" : { ""model"" : """)
                    .Append(model)
                    .Append(@""", ""state"" : """)
                    .Append(state)
                    .Append(@""" } }")
                    .ToString());
        }

        internal void ClearWorkingState()
        {
            this.actions.Add(@"{ ""action"" : ""clearWorkingState"" }");
        }
        internal void LoadWorkingState()
        {
            this.actions.Add(@"{ ""action"" : ""loadWorkingState"" }");
        }
        internal void SaveWorkingState()
        {
            this.actions.Add(@"{ ""action"" : ""saveWorkingState"" }");
        }

        internal void Start()
        {
            this.Canvas.Start();
        }

        internal void Stop()
        {
            this.Canvas.Stop();
        }
    }
}