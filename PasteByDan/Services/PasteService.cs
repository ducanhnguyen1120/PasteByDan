using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace PasteByDan.Services
{
    public static class PasteService
    {
        // Call from UI thread (has message loop — AttachThreadInput is reliable)
        public static void FocusWindow(IntPtr target)
        {
            if (target == IntPtr.Zero) return;
            try
            {
                uint dummy;
                uint myThread = Win32.GetCurrentThreadId();
                uint tgThread = Win32.GetWindowThreadProcessId(target, out dummy);

                Win32.AttachThreadInput(myThread, tgThread, true);
                Win32.ShowWindow(target, 9); // SW_RESTORE if minimized
                Win32.BringWindowToTop(target);
                Win32.SetForegroundWindow(target);
                Win32.AttachThreadInput(myThread, tgThread, false);
            }
            catch { }
        }

        // Call after delay — sends Ctrl+V to whatever is in foreground
        public static void SendCtrlV()
        {
            try
            {
                var inputs = new Win32.INPUT[4];

                inputs[0].Type = Win32.INPUT_KEYBOARD;
                inputs[0].U.ki.wVk = 0x11; // VK_CONTROL down
                inputs[0].U.ki.dwFlags = 0;

                inputs[1].Type = Win32.INPUT_KEYBOARD;
                inputs[1].U.ki.wVk = 0x56; // V down
                inputs[1].U.ki.dwFlags = 0;

                inputs[2].Type = Win32.INPUT_KEYBOARD;
                inputs[2].U.ki.wVk = 0x56; // V up
                inputs[2].U.ki.dwFlags = Win32.KEYEVENTF_KEYUP;

                inputs[3].Type = Win32.INPUT_KEYBOARD;
                inputs[3].U.ki.wVk = 0x11; // VK_CONTROL up
                inputs[3].U.ki.dwFlags = Win32.KEYEVENTF_KEYUP;

                Win32.SendInput(4, inputs, Marshal.SizeOf(typeof(Win32.INPUT)));
            }
            catch { }
        }
    }
}
