using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace RemoteScreenInverseClient
{
    static class Program
    {
        [DllImport("GDI32.DLL", CallingConvention = CallingConvention.StdCall)]
        public static extern int BitBlt(IntPtr hdcDest, int x, int y, int cx, int cy, IntPtr hdcSrc, int x1, int y1, UInt32 rop);
        // https://docs.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-bitblt
        [DllImport("GDI32.DLL", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy);
        [DllImport("GDI32.DLL", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("GDI32.DLL", CallingConvention = CallingConvention.StdCall)]
        public static extern void DeleteDC(IntPtr hdc);
        [DllImport("GDI32.DLL", CallingConvention = CallingConvention.StdCall)]
        public static extern void DeleteObject(IntPtr handle);
        [DllImport("USER32.DLL", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("USER32.DLL", CallingConvention = CallingConvention.StdCall)]
        public static extern void ReleaseDC(IntPtr hwnd, IntPtr hdc);
        [DllImport("GDI32.DLL", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hBitmap);

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new DisplayForm());
        }
    }
}
