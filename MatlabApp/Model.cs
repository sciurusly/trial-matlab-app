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
            this.api = new API();
            this.updateBlocks = new Dictionary<string, Block>();
            this.updateState = false;
            this.updateAll = updateAll;
        }

        internal void LoadState(string name, string state)
        {
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
            Console.WriteLine("      post update");
            //refresh the external source
            if (!String.IsNullOrEmpty(this.Reference))
            {
                this.api.Update("set", this.PostBlock, "Reference", this.Reference);
                this.api.Update("update");
            }
            if (this.updateAll)
            {
                if (force)
                {
                    this.api.Refresh("#SaveExternal", 1);
                }
                else
                {
                    this.api.Refresh("#SaveExternal", "due");
                }
            }
            else
            {
                this.api.Refresh(this.PostBlock, 1);
            }
            var status = this.api.Status();

            if (!status.IsEmpty)
            {
                this.StatusAdd("something went wrong");
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
            Console.WriteLine("      pre update");
            if (this.updateState)
            {
                Console.WriteLine("        LoadState");
                this.api.LoadState(this.Name, this.State);
                this.updateState = false;
            }
            this.api.Update("clear");
        }

        internal void Reset()
        {
            Console.WriteLine("  State reload");
            this.updateState = true;
            this.Update(true);
        }

        public string State { get; private set; }

        internal void UpdateField(string block, string tunable, string value)
        {
            Console.WriteLine("UpdateField " + block + "." + tunable + " = " + value);
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
                Console.WriteLine("    Update for " + this.Name + '@' + this.State);

                this.PreUpdate();
                foreach (var bl in this.updateBlocks.Values)
                {
                    bl.Update(this.api);
                }
                this.api.Update("update");

                this.PostUpdate(force);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("*** EXCEPTION " + ex.Message + '\n' + ex.Source);
                this.StatusAdd("Error updating model");
            }
            //this.StatusAdd("Failed!");
        }

        public string[] Status { get; private set; }

        private void StatusAdd(string message)
        {
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
                val=new Field(name);
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
            Console.WriteLine("      update block " + this.Name);
            foreach (var field in this.tunables.Keys)
            {
                Console.WriteLine("        " + field + '=' + this.Get(field));
                this.Field(field).Update(api, this.Name);
            }
        }

        internal void PostUpdate()
        {
            this.tunables.Clear();
        }
    }

    internal class Field
    {
        internal Field(string name)
        {
            this.Name = name;
        }
        internal String Name { get; }
        internal String Value { get; set; }

        internal void Set(string value)
        {
            this.Value = value;
        }
        internal void Update(API api, String block)
        {
            api.Update("set", block, this.Name, this.Value);
        }
    }
}