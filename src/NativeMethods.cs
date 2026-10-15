using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;

namespace HainanTurtlePet
{
    internal static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint { public int X, Y; public NativePoint(int x, int y) { X = x; Y = y; } }
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeSize { public int Width, Height; public NativeSize(int width, int height) { Width = width; Height = height; } }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BlendFunction { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

        [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
        [DllImport("user32.dll")] internal static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr window, IntPtr destinationDC,
            ref NativePoint position, ref NativeSize size, IntPtr sourceDC, ref NativePoint sourcePosition,
            int colorKey, ref BlendFunction blend, int flags);

        internal static void Present(IntPtr window, Bitmap bitmap, Point location)
        {
            IntPtr screenDC = GetDC(IntPtr.Zero);
            IntPtr memoryDC = CreateCompatibleDC(screenDC);
            IntPtr image = IntPtr.Zero;
            IntPtr previous = IntPtr.Zero;
            try
            {
                image = bitmap.GetHbitmap(Color.FromArgb(0));
                previous = SelectObject(memoryDC, image);
                NativePoint position = new NativePoint(location.X, location.Y);
                NativePoint source = new NativePoint(0, 0);
                NativeSize size = new NativeSize(bitmap.Width, bitmap.Height);
                BlendFunction blend = new BlendFunction();
                blend.SourceConstantAlpha = 255;
                blend.AlphaFormat = 1;
                if (!UpdateLayeredWindow(window, screenDC, ref position, ref size, memoryDC,
                    ref source, 0, ref blend, 2))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to present the transparent pet window.");
            }
            finally
            {
                if (previous != IntPtr.Zero) SelectObject(memoryDC, previous);
                if (image != IntPtr.Zero) DeleteObject(image);
                DeleteDC(memoryDC);
                ReleaseDC(IntPtr.Zero, screenDC);
            }
        }
    }
}
