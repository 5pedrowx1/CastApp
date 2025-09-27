using System.Runtime.InteropServices;

namespace CastApp
{
    public class Natives
    {
        [DllImport("user32.dll")]
        public static extern int GetAsyncKeyState(Int32 i);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FreeConsole();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool OpenClipboard(nint hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern nint GetClipboardData(uint uFormat);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern nint GlobalLock(nint hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GlobalUnlock(nint hMem);
    }
}
