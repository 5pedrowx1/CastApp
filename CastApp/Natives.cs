using System.Runtime.InteropServices;

namespace CastApp
{
    public class Natives
    {
        [DllImport("user32.dll")]
        public static extern int GetAsyncKeyState(Int32 i);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FreeConsole();
    }
}
