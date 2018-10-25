using System;
using System.Collections.Generic;
using Sciurus.FinancialCanvas.API;
using Sciurus.FinancialCanvas.Logging;

//Scirius.FinancialCanvas.API;

namespace MatlabApp
{
    /// <summary>
    /// Represents a model
    /// </summary>
    internal class Model
    {
        private ModelCaller caller;
        /// <summary>keep the client instance</summary>
        private ClientConnection client;
        private Dictionary<String, Block> updateBlocks; // if a field changes, update the tunables in the block

        // do we need to update the loaded state file?
        private bool updateState;
        private bool updateAll;         // flag to update all dashboards
        /// <summary>
        /// Create the model
        /// Real world this would need to be a singleton as API supports only a single state open at a time.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="name"></param>
        /// <param name="state"></param>
        internal Model(ModelCaller caller, int port, bool updateAll)
        {
            Logger.Log.Write(3, "Model:" + (updateAll ? "All" : "Current"));
            this.caller = caller;
            this.client = new ClientConnection(this, port);
            this.updateBlocks = new Dictionary<string, Block>();
            this.updateState = false;
            this.updateAll = updateAll;
        }

        internal void LoadState(string name, string state)
        {
            Logger.Log.Write(4, "Model.LoadState." + name + '/' + state);
            this.Name = name;
            this.State = state;
            this.updateState = true;
        }

        public string Name { get; private set; }

        internal String PostBlock { get; set; }
        internal String Reference { get; set; }

        private void PostUpdate(bool force)
        {

            if (!this.updateAll && String.IsNullOrEmpty(this.PostBlock))
            {
                return;
            }
            Logger.Log.Write(3, "Model.PostUpdate");

            //refresh the external source
            if (!String.IsNullOrEmpty(this.Reference))
            {
                Logger.Log.Write(6, "Model.PostUpdate.Update." + this.PostBlock + '.' + this.Reference);
                var bl = new Block(this.PostBlock);
                bl.Set("Reference", this.Reference);

                this.client.UpdateBlock(bl);
            }
            if (this.updateAll)
            {
                if (force)
                {
                    Logger.Log.Write(6, "Model.PostUpdate.Refresh.All.Force");
                    this.client.Refresh("#SaveExternal", true);
                }
                else
                {
                    Logger.Log.Write(6, "Model.PostUpdate.Refresh.All.Due");
                    this.client.Refresh("#SaveExternal", "due");
                }
            }
            else
            {
                Logger.Log.Write(6, "Model.PostUpdate.Refresh.Current");
                this.client.Refresh(this.PostBlock, true);
            }

            foreach (var bl in this.updateBlocks.Values)
            {
                // tidy up after the update
                bl.PostUpdate();
            }
        }

        private void PreUpdate()
        {
            Logger.Log.Write(3, "Model.PreUpdate");
            if (this.updateState)
            {
                Logger.Log.Write(3, "Model.PreUpdate.LoadState");
                this.client.LoadState(this.Name, this.State);
            }
            else
            {
                this.client.SaveWorkingState();
            }
        }

        internal void Reset()
        {
                Logger.Log.Write(4, "Model.Reset");
                this.updateState = true;
                this.Update(true);
        }

        internal void Revert()
        {
            try
            {
                Logger.Log.Write(4, "Model.Revert");
                this.client.LoadWorkingState();
                this.client.SendActions();
            }
            catch (Exception ex)
            {
                Logger.Log.Error("Model.Revert", ex);
                this.StatusAdd("Error: " + ex.Message);
            }
            Logger.Log.Write(3, "Model.Revert.Done");
        }

        public string State { get; private set; }


        internal void UpdateField(string block, string tunable, string value)
        {
            Logger.Log.Write(7, "Model.UpdateField " + block + "." + tunable + " = " + value);
            Block bl;
            var newBlock = !this.updateBlocks.TryGetValue(block, out bl);
            if (newBlock)
            {
                bl = new Block(block);
                this.updateBlocks[block] = bl;
            }
            bl.Set(tunable, value);
        }

        public void Update(bool force)
        {
            try
            {
                Logger.Log.Write(3, "Model.Update." + this.Name + '/' + this.State);

                // we don't actually read or write anything we just work out the actions to send to the api.
                this.client.ClearActions();
                this.PreUpdate();

                foreach (var bl in this.updateBlocks.Values)
                {
                    this.client.UpdateBlock(bl);
                }
                this.PostUpdate(force);

                this.client.SendActions();
            }
            catch (Exception ex)
            {
                Logger.Log.Error("Update model", ex);
                this.StatusAdd("Error: " + ex.Message);
            }
            Logger.Log.Write(3, "Model.Update.Done");
        }

        public string[] Status { get; private set; }

        internal void Start()
        {
            this.client.Start();
        }

        private void StatusAdd(string message)
        {
            Logger.Log.Write(7, message);
            if (this.Status == null)
            {
                this.Status = new string[] { message };
            }
            else
            {
                var list = new List<string>(this.Status);
                list.Add(message);
                this.Status = list.ToArray();
            }
        }

        internal void Stop()
        {
            this.client.Stop();
        }

        internal void NotifyReply(bool success, string[] reply)
        {
            Logger.Log.Write(3, "Model.NotifyReply:" + success);
            if (success)
            {
                this.updateState = false;
            }
            this.caller.NotifyReply(success, reply);
        }
    }

    internal class Block
    {
        internal Dictionary<string, Field> tunables = new Dictionary<string, Field>();

        internal Block(string name)
        {
            Logger.Log.Write(7, "Block." + name);
            this.Name = name;
        }

        internal string Name { get; }

        internal string Get(string name)
        {
            return this.Field(name).Value;
        }
        internal void Set(string name, string value)
        {
            this.Field(name).Set(value);
        }

        private Field Field(string name)
        {
            Field val;
            if (!this.tunables.TryGetValue(name, out val))
            {
                val = new Field(name);
                this.tunables[name] = val;
            }
            return val;
        }

        internal void PostUpdate()
        {
            Logger.Log.Write(7, "Block.PostUpdate." + this.Name);
            this.tunables.Clear();
        }
    }

    internal class Field
    {
        internal Field(string name)
        {
            Logger.Log.Write(8, "Field." + name);
            this.Name = name;
        }
        internal String Name { get; }
        internal String Value { get; private set; }

        internal void Set(string value)
        {
            this.Value = value;
        }
    }
}