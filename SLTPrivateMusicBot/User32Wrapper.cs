namespace SLTPrivateMusicBot
{
    using System.Runtime.InteropServices;

    internal static class User32Wrapper
    {

        [DllImport("user32.dll")]
        public static extern uint GetDoubleClickTime();
    }
}
