namespace Vsign4.Properties
{
    // Standalone-build shim. When this module is embedded in the Vsign4 host app the designers
    // resolve images from the host's real Vsign4.Properties.Resources. The standalone
    // VRemoteDesktop.exe only needs the single image referenced by the form designers
    // (key_16px), so provide it here to keep the project self-contained.
    internal class Resources
    {
        internal static System.Drawing.Bitmap key_16px
        {
            get { return new System.Drawing.Bitmap(16, 16); }
        }
    }
}
