using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using System.IO;
using PasteByDan.Models;

namespace PasteByDan.Services
{
    public static class ClipboardService
    {
        private static readonly uint CF_EXCLUDE = Win32.RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
        private static bool _ignoreNext = false;

        public static void SetIgnoreNext(bool ignore) => _ignoreNext = ignore;
        public static bool ConsumeIgnoreNext()
        {
            if (_ignoreNext) { _ignoreNext = false; return true; }
            return false;
        }

        public static ClipboardItem GetCurrentClipboardItem()
        {
            try
            {
                if (System.Windows.Clipboard.ContainsImage())
                {
                    var bmp = System.Windows.Clipboard.GetImage();
                    if (bmp != null)
                    {
                        var base64 = BitmapSourceToBase64(bmp);
                        return new ClipboardItem { Type = ClipType.Image, ImageBase64 = base64 };
                    }
                }
                if (System.Windows.Clipboard.ContainsText())
                {
                    var text = System.Windows.Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return new ClipboardItem
                        {
                            Type = ClipboardItem.DetectType(text),
                            TextContent = text
                        };
                    }
                }
            }
            catch { }
            return null;
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

        public static void WriteTextSuppressed(string text, IntPtr hwnd)
        {
            Log($"WriteText: CF_EXCLUDE={CF_EXCLUDE}");
            SetIgnoreNext(true);
            bool opened = false;
            for (int i = 0; i < 5; i++)
            {
                if (Win32.OpenClipboard(hwnd)) { opened = true; break; }
                System.Threading.Thread.Sleep(10);
            }
            if (!opened) { SetIgnoreNext(false); Log("WriteText: OpenClipboard failed"); return; }
            try
            {
                Win32.EmptyClipboard();
                var bytes = System.Text.Encoding.Unicode.GetBytes(text + "\0");
                IntPtr hText = Win32.GlobalAlloc(Win32.GMEM_MOVEABLE, (uint)bytes.Length);
                if (hText != IntPtr.Zero)
                {
                    IntPtr ptr = Win32.GlobalLock(hText);
                    if (ptr != IntPtr.Zero) { Marshal.Copy(bytes, 0, ptr, bytes.Length); Win32.GlobalUnlock(hText); }
                    Win32.SetClipboardData(Win32.CF_UNICODETEXT, hText);
                }
                if (CF_EXCLUDE != 0)
                {
                    IntPtr hEx = Win32.GlobalAlloc(Win32.GMEM_MOVEABLE | Win32.GMEM_ZEROINIT, 1);
                    if (hEx != IntPtr.Zero) { Win32.SetClipboardData(CF_EXCLUDE, hEx); Log($"CF_EXCLUDE set, hEx={hEx.ToInt64():X}"); }
                }
            }
            catch (Exception ex) { Log($"WriteText error: {ex.Message}"); SetIgnoreNext(false); }
            finally { Win32.CloseClipboard(); Log("WriteText: CloseClipboard done"); }
        }

        public static void WriteImageSuppressed(BitmapSource bmp, IntPtr hwnd)
        {
            SetIgnoreNext(true);
            bool opened = false;
            for (int i = 0; i < 5; i++)
            {
                if (Win32.OpenClipboard(hwnd)) { opened = true; break; }
                System.Threading.Thread.Sleep(10);
            }
            if (!opened) { SetIgnoreNext(false); return; }
            try
            {
                Win32.EmptyClipboard();
                var dibData = BitmapSourceToDib(bmp);
                if (dibData != null)
                {
                    IntPtr hMem = Win32.GlobalAlloc(Win32.GMEM_MOVEABLE, (uint)dibData.Length);
                    if (hMem != IntPtr.Zero)
                    {
                        IntPtr ptr = Win32.GlobalLock(hMem);
                        if (ptr != IntPtr.Zero) { Marshal.Copy(dibData, 0, ptr, dibData.Length); Win32.GlobalUnlock(hMem); }
                        Win32.SetClipboardData(Win32.CF_DIB, hMem);
                    }
                }
                if (CF_EXCLUDE != 0)
                {
                    IntPtr hEx = Win32.GlobalAlloc(Win32.GMEM_MOVEABLE | Win32.GMEM_ZEROINIT, 1);
                    if (hEx != IntPtr.Zero) Win32.SetClipboardData(CF_EXCLUDE, hEx);
                }
            }
            catch { SetIgnoreNext(false); }
            finally { Win32.CloseClipboard(); }
        }

        private static string BitmapSourceToBase64(BitmapSource bmp)
        {
            try
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));
                using (var ms = new MemoryStream())
                {
                    encoder.Save(ms);
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
            catch { return null; }
        }

        public static BitmapSource Base64ToBitmapSource(string base64)
        {
            try
            {
                var bytes = Convert.FromBase64String(base64);
                using (var ms = new MemoryStream(bytes))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch { return null; }
        }

        private static byte[] BitmapSourceToDib(BitmapSource bmp)
        {
            try
            {
                // Convert to Bgr32
                var converted = new FormatConvertedBitmap(bmp, System.Windows.Media.PixelFormats.Bgr32, null, 0);
                int w = converted.PixelWidth;
                int h = converted.PixelHeight;
                int stride = w * 4;
                byte[] pixels = new byte[stride * h];
                converted.CopyPixels(pixels, stride, 0);

                int headerSize = 40;
                var result = new byte[headerSize + pixels.Length];
                // BITMAPINFOHEADER
                BitConverter.GetBytes((uint)40).CopyTo(result, 0);
                BitConverter.GetBytes((int)w).CopyTo(result, 4);
                BitConverter.GetBytes((int)-h).CopyTo(result, 8); // negative = top-down
                BitConverter.GetBytes((ushort)1).CopyTo(result, 12);
                BitConverter.GetBytes((ushort)32).CopyTo(result, 14);
                // rest zeros
                pixels.CopyTo(result, headerSize);
                return result;
            }
            catch { return null; }
        }
    }
}
