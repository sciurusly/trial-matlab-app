namespace MatlabApp
{
    /// <summary>
    /// Provide the names to be used for addressing firebase
    /// Rather bad to have shared constants but they're used in various places and this means
    /// that they're defined in a single place.
    /// </summary>
    internal class Studio
    {
        // keys for studio firebas db.
        internal static readonly string CANVAS = "/_canvas";
        internal static readonly string CANVAS_LISTENING = CANVAS + "/listening";
        internal static readonly string CANVAS_TWOWAY = CANVAS + "/twoway";
        internal static readonly string CANVAS_ERRORS = CANVAS + "/errors";
        internal static readonly string CANVAS_WORKING = CANVAS + "/working";
        internal static readonly string STUDIO = "/_studio";
        internal static readonly string STUDIO_CALLBACK = STUDIO + "/callback";

        // time to wait between heartbeats
        internal static readonly int WAIT_HEARTBEAT = 60000;

        // wait between last message and processing:
        internal static readonly int WAIT_MILLISECONDS = 250;
        internal static readonly int WAIT_TICKDOWN = 50;
    }
}
