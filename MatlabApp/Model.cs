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

        /// <summary>
        /// Create the model
        /// Real world this would need to be a singleton as API supports only a single state open at a time.
        /// </summary>
        /// <param name="api"></param>
        /// <param name="name"></param>
        /// <param name="state"></param>
        internal Model()
        {
            this.api = new API();
            this.updateBlocks = new Dictionary<string, Block>();
            this.updateState = false;
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

        private void PostUpdate()
        {
            if (String.IsNullOrEmpty(this.PostBlock))
            {
                return;
            }
            Console.WriteLine("post update");
            //refresh the external source
            if (!String.IsNullOrEmpty(this.Reference))
            {
                this.api.UpdateParameter("set", this.PostBlock, "Reference", this.Reference);
                this.api.UpdateParameter("update");
            }
            this.api.Refresh(this.PostBlock, 1);
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
        public void Update()
        {
            try
            {
                Console.WriteLine("Update");
                if (this.updateState)
                {
                    Console.WriteLine("LoadState");
                    this.api.LoadState(this.Name, this.State);
                    this.updateState = false;
                }
                this.api.UpdateParameter("clear");
                foreach (var bl in this.updateBlocks.Values)
                {
                    bl.Update(this.api);
                }
                this.api.UpdateParameter("update");
                this.PostUpdate();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Exception " + ex.Message + '\n' + ex.Source);
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
            Console.WriteLine("update block " + this.Name);
            foreach (var field in this.tunables.Keys)
            {
                Console.WriteLine("-" + field + '=' + this.Get(field));
                this.Field(field).Update(api, this.Name);
            }
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
            api.UpdateParameter("set", block, this.Name, this.Value);
        }
    }
}