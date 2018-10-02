using System;
using System.Collections.Generic;
using Scirius.FinancialCanvas;

namespace MatlabApp
{
    /// <summary>
    /// Represents a model
    /// </summary>
    internal class Model
    {
        /// <summary>keep the api instance</summary>
        private API api;
        private Dictionary<String, Block> updateBlocks; // if a field changes, update the tunables in the block

        // do we need to update the loaded state file?
        private bool updateState;
        private bool updateAll;         // flag to update all dashboards
        /// <summary>
        /// Create the model
        /// Real world this would need to be a singleton as API supports only a single state open at a time.
        /// </summary>
        /// <param name="api"></param>
        /// <param name="name"></param>
        /// <param name="state"></param>
        internal Model(bool updateAll)
        {
            Logger.Write(3, "Model." + (updateAll ? "All" : "Current"));
            this.api = new API();
            this.updateBlocks = new Dictionary<string, Block>();
            this.updateState = false;
            this.updateAll = updateAll;
        }

        internal void LoadState(string name, string state)
        {
            Logger.Write(4, "Model.LoadState." + name + '/' + state);
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
            Logger.Write(3, "Model.PostUpdate");

            var status = this.api.Status();
            if (!status.IsEmpty)
            {
                this.StatusAdd("something went wrong");
            }
            else
            {
                //refresh the external source
                if (!String.IsNullOrEmpty(this.Reference))
                {
                    Logger.Write(6, "Model.PostUpdate.Update." + this.PostBlock + '.' + this.Reference);
                    this.api.Update("set", this.PostBlock, "Reference", this.Reference);
                    this.api.Update("update");
                }
                if (this.updateAll)
                {
                    if (force)
                    {
                        Logger.Write(6, "Model.PostUpdate.Refresh.All.Force");
                        this.api.Refresh("#SaveExternal", 1);
                    }
                    else
                    {
                        Logger.Write(6, "Model.PostUpdate.Refresh.All.Due");
                        this.api.Refresh("#SaveExternal", "due");
                    }
                }
                else
                {
                    Logger.Write(6, "Model.PostUpdate.Refresh.Current");
                    this.api.Refresh(this.PostBlock, 1);
                }
            }

            foreach (var bl in this.updateBlocks.Values)
            {
                bl.PostUpdate();
            }
        }

        private void PreUpdate()
        {
            // clear status;
            this.Status = null;
            Logger.Write(3, "Model.PreUpdate");
            if (this.updateState)
            {
                Logger.Write(3, "Model.PreUpdate.LoadState");
                this.api.LoadState(this.Name, this.State);
                this.updateState = false;
            }
            Logger.Write(4, "Model.PreUpdate.Clear");
            this.api.Update("clear");
            Logger.Write(4, "Model.PreUpdate.WorkingState.Save");
            this.api.WorkingState("save");
        }

        internal void Reset()
        {
            Logger.Write(4, "Model.Reset");
            this.updateState = true;
            this.Update(true);
        }

        internal void Revert()
        {
            Logger.Write(4, "Model.Revert");
            if (this.updateState)
            {
                return; // nothing to do
            }
            Logger.Write(5, "Model.Revert.WorkingState.Load");
            this.api.WorkingState("load");
            Logger.Write(5, "Model.Revert.WorkingState.Ready");
        }

        public string State { get; private set; }


        internal void UpdateField(string block, string tunable, string value)
        {
            Logger.Write(7, "Model.UpdateField " + block + "." + tunable + " = " + value);
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
                Logger.Write(3, "Model.Update." + this.Name + '/' + this.State);

                this.PreUpdate();
                foreach (var bl in this.updateBlocks.Values)
                {
                    bl.Update(this.api);
                }
                var ret = this.api.Update("update");
                
                this.PostUpdate(force);
            }
            catch (Exception ex)
            {
                Logger.Error("Update model", ex);
                this.StatusAdd("Error: " + ex.Message);
            }
            Logger.Write(3, "Model.Update.Done");
        }

        public string[] Status { get; private set; }

        private void StatusAdd(string message)
        {
            Logger.Write(7, message);
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
    }

    internal class Block
    {
        private Dictionary<string, Field> tunables = new Dictionary<string, Field>();

        internal Block(string name)
        {
            Logger.Write(7, "Block." + name);
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
        internal void Update(API api)
        {
            if (this.tunables.Count == 0)
            {
                return;
            }
            Logger.Write(7, "Block.Update." + this.Name);
            foreach (var field in this.tunables.Keys)
            {
                this.Field(field).Update(api, this.Name);
            }
        }

        internal void PostUpdate()
        {
            Logger.Write(7, "Block.PostUpdate." + this.Name);
            this.tunables.Clear();
        }
    }

    internal class Field
    {
        internal Field(string name)
        {
            Logger.Write(8, "Field." + name);
            this.Name = name;
        }
        internal String Name { get; }
        internal String Value { get; private set; }

        internal void Set(string value)
        {
            this.Value = value;
        }
        internal void Update(API api, String block)
        {
            Logger.Write(9, "Field.Update." + this.Name + '=' + this.Value);
            api.Update("set", block, this.Name, this.Value);
        }
    }
}