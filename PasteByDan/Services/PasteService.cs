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

        private static void Log(string msg)
        {
            try
            {
                var path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "paste_debug.txt");
                System.IO.File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\r\n");
            }
            catch { }
        }

        // Send WM_PASTE directly to the focused child of target window (no focus needed)
        public static void WmPasteTo(IntPtr targetWindow)
        {
            try
            {
                uint tid = Win32.GetWindowThreadProcessId(targetWindow, out _);
                var gui = new Win32.GUITHREADINFO { cbSize = (uint)Marshal.SizeOf(typeof(Win32.GUITHREADINFO)) };
                Win32.GetGUIThreadInfo(tid, ref gui);

                IntPtr dest = (gui.hwndFocus != IntPtr.Zero) ? gui.hwndFocus : targetWindow;
                Log($"WM_PASTE → dest={dest.ToInt64():X} (focus={gui.hwndFocus.ToInt64():X})");
                Win32.PostMessage(dest, Win32.WM_PASTE, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception ex) { Log($"WmPasteTo error: {ex.Message}"); }
        }

        // Fallback: inject Ctrl+V via SendInput
        public static void SendCtrlV()
        {
            try
            {
                var inputs = new Win32.INPUT[4];

                // Use Right Ctrl (0xA3) to avoid PowerToys remapping Left Ctrl → Win
                inputs[0].Type = Win32.INPUT_KEYBOARD;
                inputs[0].U.ki.wVk = 0xA3; // VK_RCONTROL
                inputs[0].U.ki.dwFlags = Win32.KEYEVENTF_EXTENDEDKEY;

                inputs[1].Type = Win32.INPUT_KEYBOARD;
                inputs[1].U.ki.wVk = 0x56; // V
                inputs[1].U.ki.dwFlags = 0;

                inputs[2].Type = Win32.INPUT_KEYBOARD;
                inputs[2].U.ki.wVk = 0x56;
                inputs[2].U.ki.dwFlags = Win32.KEYEVENTF_KEYUP;

                inputs[3].Type = Win32.INPUT_KEYBOARD;
                inputs[3].U.ki.wVk = 0xA3; // VK_RCONTROL
                inputs[3].U.ki.dwFlags = Win32.KEYEVENTF_EXTENDEDKEY | Win32.KEYEVENTF_KEYUP;

                int inputSize = Marshal.SizeOf(typeof(Win32.INPUT));
                uint sent = Win32.SendInput(4, inputs, inputSize);
                uint err = Win32.GetLastError();
                Log($"SendInput: size={inputSize}, sent={sent}, lastErr={err}");
            }
            catch (Exception ex) { Log($"SendCtrlV error: {ex.Message}"); }
        }
    }
}
