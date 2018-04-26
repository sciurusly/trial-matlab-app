using Scirius.FinancialCanvas.API;
namespace MatlabApp
{
    class Program
    {
        static void Main(string[] args)
        {
            System.Console.WriteLine("api " + System.DateTime.Now);
            // creating an instance of the API class loads up the environment.
            // this includes the overhead of the MATLAB runtime.
            var api = new API();
            System.Console.WriteLine("load " + System.DateTime.Now);
            // The api handle is passed into Model to handle working with the model:
            var model = new Model(api, "API Test", "State (3)");
            System.Console.WriteLine("set " + System.DateTime.Now);
            model.UpdateNumber("InitialAssets", 110000000);
            System.Console.WriteLine("clear " + System.DateTime.Now);
            // this clears the model from the api, worth calling during shutdown
            // or if changing the required model.
            api.Clear();
            System.Console.WriteLine("done " + System.DateTime.Now);
        }
    }

    internal class Model
    {
        /// <summary>keep the api instance</summary>
        private API api;

        /// <summary>
        /// Create the model
        /// Real world this would need to be a singleton as API supports only a single state open at a time.
        /// </summary>
        /// <param name="api"></param>
        /// <param name="name"></param>
        /// <param name="state"></param>
        internal Model(API api, string name, string state)
        {
            this.api = api;
            // load state loads a named state within a model.
            // once loaded other calls will reference the same model.
            this.api.LoadState(name, state);
        }

        internal void UpdateNumber(string field, double value)
        {
            // SetProperty updates a tunable property on a single block, identified by id.
            // in this case -1 references the global properties block.
            // Call is SetProperty(numReturn, id, tunable name, new value, [force])
            // a return is available [use numReturn==1] that flags 0/1 if the field changed.
            // the optional force flag can be set to 1 to force an update.
            this.api.SetProperty(0, -1, field, value);

            // there is similarly GetProperty(numReturn, id, tunable name) usage would be
            // var x = this.api.GetProperty(0, -1, field) would return as a struture the value of the property.
        }
    }
}
