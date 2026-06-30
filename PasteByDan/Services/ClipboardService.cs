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

        public static void WriteTextSuppressed(string text, IntPtr hwnd)
        {
            try
            {
                SetIgnoreNext(true);
                var data = new System.Windows.DataObject();
                data.SetText(text);
                data.SetData("ExcludeClipboardContentFromMonitorProcessing", new byte[1]);
                System.Windows.Clipboard.SetDataObject(data, copy: true);
            }
            catch
            {
                SetIgnoreNext(false);
            }
        }

        public static void WriteImageSuppressed(BitmapSource bmp, IntPtr hwnd)
        {
            try
            {
                SetIgnoreNext(true);
                var data = new System.Windows.DataObject();
                data.SetImage(bmp);
                data.SetData("ExcludeClipboardContentFromMonitorProcessing", new byte[1]);
                System.Windows.Clipboard.SetDataObject(data, copy: true);
            }
            catch
            {
                SetIgnoreNext(false);
            }
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
