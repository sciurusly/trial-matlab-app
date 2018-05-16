using System;
using System.Collections.Generic;
using Scirius.FinancialCanvas;

namespace MatlabApp
{
    /// <summary>
    /// Repreents a model
    /// </summary>
    internal class Model
    {
        /// <summary>keep the api instance</summary>
        private API api;
        private Dictionary<String, Block> updateBlocks; // if a field changes, update the tunables in the block
        //private Dictionary<String, Block> postBlocks; // after an update, all these blocks are updated.
        
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

        //internal void PostField(string block, string tunable, string field, string value)
        //{
        //    Block bl;
        //    if (!this.postBlocks.TryGetValue(block, out bl))
        //    {
        //        bl = new Block(block);
        //        this.postBlocks[block] = bl;
        //    }
        //    bl.Set(tunable, field, value);
        //}

        internal String PostBlock { get; set; }
        private void PostUpdate()
        {
            if (String.IsNullOrEmpty(this.PostBlock))
            {
                return;
            }
            Console.WriteLine("post update");
            // refresh the external source
            this.api.Refresh(this.PostBlock, 1);
        }

        public string State { get; private set; }

        internal void UpdateField(string block, string tunable, string field, string value)
        {
            Block bl;
            var newBlock = !this.updateBlocks.TryGetValue(block, out bl);
            if (newBlock)
            {
                bl = new Block(block);
                this.updateBlocks[block] = bl;
            }
            bl.Set(tunable, field, value);
            if (!this.Updating)
            {
                return;
            }
            bl.Update(this.api);

            this.PostUpdate();
            Console.WriteLine("done");
        }

        public bool Updating { get; set; }
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
        internal void Set(string name, string type, string value)
        {
            this.Field(name).Set(type,value);
        }
        internal string Type(string name)
        {
            return this.Field(name).Type;
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
            api.SetStream("off");
            foreach (var field in this.tunables.Keys)
            {
                Console.WriteLine("-" + field + '=' + this.Get(field));
                this.Field(field).Update(api, this.Name);
            }
            api.SetStream("on");
        }
    }

    internal class Field
    {
        internal Field(string name)
        {
            this.Name = name;
            this.Type = "unknown";
        }
        internal String Name { get; }
        internal String Type { get; set; }
        internal String Value { get; set; }

        internal void Set(string name, string value)
        {
            switch (name)
            {
                case "value":
                    {
                        this.Value = value;
                        return;
                    }
                case "type":
                    {
                        this.Type = value;
                        return;
                    }
            }
            throw new InvalidOperationException("Cannot set field " + name);
        }
        internal void Update(API api, String block)
        {
            switch(this.Type)
            {
                case "string":
                    {
                        api.SetProperty(0, block, this.Name, this.Value);
                        return;
                    }
                case "double":
                    {
                        api.SetProperty(0, block, this.Name, Double.Parse(this.Value));
                        return;
                    }
                case "date":
                    {
                        // dates must be YYYYMMDD
                        var date = DateTime.ParseExact(this.Value, "yyyyMMdd",
                            System.Globalization.CultureInfo.InvariantCulture);
                        var span = new TimeSpan(date.Ticks);
                        // C# starts at 01/01/0001, Matlab at 00/00/0000
                        // so we need to add the extra year and 2 days
                        var dateNum = span.TotalDays + 367;
                        api.SetProperty(0, block, this.Name, dateNum);
                        return;
                    }
                case "integer":
                    {
                        api.SetProperty(0, block, this.Name, Int32.Parse(this.Value));
                        return;
                    }
            }
            throw new InvalidOperationException("Cannot set property for type " + this.Type);
        }
    }
}