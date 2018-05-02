using System;
using System.Collections.Generic;
using Scirius.FinancialCanvas;

namespace MatlabApp
{

    internal class Model
    {
        /// <summary>keep the api instance</summary>
        private API api;
        private Dictionary<String, Block> updateBlocks; // if a field changes, update the tunables in the block
        private Dictionary<String, Block> postBlocks; // after an update, all these blocks are updated.

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
            this.postBlocks = new Dictionary<string, Block>();
        }

        internal void LoadState(string name, string state)
        {
            this.Name = name;
            this.State = state;
            this.api.LoadState(name, state);
        }

        internal void UpdateNumber(string field, double value)
        {
            // SetProperty updates a tunable property on a single block, identified by id.
            // in this case -1 references the global properties block.
            // Call is SetProperty(numReturn, id, tunable name, new value, [force])
            // a return is available [use numReturn==1] that flags 0/1 if the field changed.
            // the optional force flag can be set to 1 to force an update.
            //this.api.SetProperty("Global Properties", field, value);
            //this.api.SetProperty("SaveExternal", "SaveWhen", "now");
            // there is similarly GetProperty(numReturn, id, tunable name) usage would be
            // var x = this.api.GetProperty(0, -1, field) would return as a struture the value of the property.
        }

        public string Name { get; private set; }

        internal void PostField(string block, string field, object value)
        {
            Block bl;
            if (!this.postBlocks.TryGetValue(block, out bl))
            {
                bl = new Block(block);
                this.postBlocks[block] = bl;
            }
            bl.Set(field, value);
        }

        private void PostUpdate()
        {
            // streaming off
            foreach (var block in this.postBlocks.Values)
            {
            }
        }

        public string State { get; private set; }

        internal void UpdateField(string block, string field, object value)
        {
            Block bl;
            var newBlock = !this.updateBlocks.TryGetValue(block, out bl);
            if (newBlock)
            {
                bl = new Block(block);
                this.updateBlocks[block] = bl;
            }
            bl.Set(field, value);
            if (!this.Updating)
            {
                return;
            }
            // set streaming off
            bl.Update(this.api);
            // set streaming on

            this.PostUpdate();
        }

        public bool Updating { get; set; }
    }

    internal class Block
    {
        private Dictionary<String, object> tunables = new Dictionary<string, object>();

        internal Block(string name)
        {
            this.Name = name;
        }
        internal string Name { get; private set; }

        internal object Get(string name)
        {
            object val;
            if (!this.tunables.TryGetValue(name, out val))
            {
                return null;
            }
            return val;
        }
        internal void Set(string name, object value)
        {
            this.tunables[name] = value;
        }

        internal void Update(API api)
        {
            foreach(var field in this.tunables.Keys)
            {
                var value = this.Get(field);
                Console.WriteLine("set prop " + this.Name + '.' + field + '=' + value);
                if (value is string)
                {
                    api.SetProperty(0, this.Name, field, (string)value);
                }
                else if (value is Int16 || value is Int32)
                {
                    api.SetProperty(0, this.Name, field, (Int32)value);
                }
                else
                {
                    api.SetProperty(0, this.Name, field, (double)value);
                }
            }
        }
    }
}