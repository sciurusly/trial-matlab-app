using FireSharp;
using FireSharp.Config;

namespace MatlabApp
{
    /// <summary>
    /// Provide a client connection for the Studio Firebase database
    /// </summary>
    internal abstract class StudioSession
    {
        protected FirebaseClient client;

        protected StudioSession(string name, string secret)
        {
            var path = "https://" + name + ".firebaseio.com/";
            Logger.Write(6, "StudioSession." + path);
            var config = new FirebaseConfig
            {
                BasePath = path,
                AuthSecret = secret
            };
            this.client = new FirebaseClient(config);
        }
    }
}
