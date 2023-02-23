using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace RemoteScreenInverseClient
{
    public partial class DisplayForm : Form
    {
        private Socket ss;
        private Thread connector;
        public Socket socket;
        public bool mousecaptured = false;
        public Point mouseposition = Point.Empty;
        public bool autorequest = false;

        public IntPtr hdcScreen, hdc1, hdc2, hbmprev;

        public delegate void UpdateConnectedStatusCallback();
        public UpdateConnectedStatusCallback ucscallback;

        public DisplayForm()
        {
            InitializeComponent();

            IntPtr hdcScreen = Program.GetDC(IntPtr.Zero);
            hdc1 = Program.CreateCompatibleDC(hdcScreen);
            hdc2 = Program.CreateCompatibleDC(hdcScreen);
            hbmprev = IntPtr.Zero;

            ucscallback = new UpdateConnectedStatusCallback(UpdateConnectedStatus);

            connector = new Thread(StayConnected);
            connector.Start();
        }

        public void StayConnected()
        {
            ss = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            ss.Bind(new IPEndPoint(IPAddress.Any, 8765));
            ss.Listen(1);
            while (true)
            {
                socket = ss.Accept();
                Invoke(ucscallback);
                while (socket.Connected) Thread.Sleep(100);
                Invoke(ucscallback);
            }
        }

        public void UpdateConnectedStatus()
        {
            this.Text = "DisplayWindow - F1 for help, connected: ";
            if (socket != null && socket.Connected)
            {
                this.Text += "YES";
                heartbeatTimer.Start();
            }
            else
            {
                this.Text += "NO";
                heartbeatTimer.Enabled = false;
                if (mousecaptured) FreeMouse();
            }
            this.Text += ", mousecaptured: " + (mousecaptured ? "YES" : "NO");
            // udělat lépe?
        }

        private void DisplayForm_Load(object sender, EventArgs e)
        {
        }

        private void DisplayForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (DialogResult.Yes == MessageBox.Show("Do you really want to exit RemoteScreenInverseClient?", "Really exit?", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation))
            {
                FreeMouse();
                if (connector != null) connector.Abort();

                if (socket != null) socket.Close();
                if (ss != null) ss.Close();

                if (hbmprev != IntPtr.Zero) Program.DeleteObject(hbmprev);
                Program.DeleteDC(hdc1);
                Program.DeleteDC(hdc2);
                Program.ReleaseDC(IntPtr.Zero, hdcScreen);

                Environment.Exit(0);
                //Application.Exit();
            }
            else e.Cancel = true;
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (mousecaptured)
            {
                Point p;
                if (IsValidHostCoordinate(e.X, e.Y, out p))
                {
                    if (p.X == mouseposition.X && p.Y == mouseposition.Y) return;
                    //int dx = p.X - mouseposition.X;
                    //int dy = p.Y - mouseposition.Y;
                    //SendMove((short)dx, (short)dy);
                    SendSet((short)p.X, (short)p.Y);

                    mouseposition.X = p.X;
                    mouseposition.Y = p.Y;
                }
            }
        }

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (mousecaptured)
            {
                byte[] b = new byte[1];
                if (e.Button == MouseButtons.Left) b[0] = 1;
                if (e.Button == MouseButtons.Right) b[0] = 3;
                if (e.Button == MouseButtons.Middle) b[0] = 5;
                try { socket.Send(b); }
                catch { UpdateConnectedStatus(); }
            }
            // else nic, capture až na mouseup
        }

        private void pictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            if (mousecaptured)
            {
                byte[] b = new byte[1];
                if (e.Button == MouseButtons.Left) b[0] = 2;
                if (e.Button == MouseButtons.Right) b[0] = 4;
                if (e.Button == MouseButtons.Middle) b[0] = 6;
                try { socket.Send(b); }
                catch { UpdateConnectedStatus(); }
            }
            else
            {
                Point p;
                if (IsValidHostCoordinate(e.X, e.Y, out p) && socket != null && socket.Connected)
                {
                    mousecaptured = true;
                    //Cursor.Hide();
                    Cursor.Clip = pictureBox1.RectangleToScreen(pictureBox1.ClientRectangle);

                    SendSet((short)p.X, (short)p.Y);
                    mouseposition.X = p.X;
                    mouseposition.Y = p.Y;
                }
            }
        }

        public void SendSet(short dx, short dy)
        {
            // send input Set(x,y);
            byte[] b = new byte[5];
            b[0] = 7;
            b[2] = (byte)dx;
            dx = (short)(dx >> 8);
            b[1] = (byte)dx;

            b[4] = (byte)dy;
            dy = (short)(dy >> 8);
            b[3] = (byte)dy;

            try
            { socket.Send(b); }
            catch { UpdateConnectedStatus(); }
        }

        private bool IsValidHostCoordinate(int x, int y, out Point result)
        {
            if (pictureBox1.Image == null)
            {
                result = Point.Empty;
                return false;
            }
            // x a y jsou client (picturebox) coordinates

            // započítat prázdné pruhy nahoře a dole nebo po stranách když má picturebox jiné ratio než image
            double ratiox = (double)pictureBox1.Width / (double)pictureBox1.Image.Width;
            double ratioy = (double)pictureBox1.Height / (double)pictureBox1.Image.Height;
            // iw * x = w -> x = w / iw; ih * x = expected_h; if (eh < h) => pruhy nahoře a dole; else po stranách
            double eh = (double)pictureBox1.Image.Height * ratiox;
            if (eh < pictureBox1.Height)
            {
                x -= (int)((pictureBox1.Height - eh) / 2.0);
            }
            else
            {
                y -= (int)((pictureBox1.Width - (pictureBox1.Image.Width * ratioy)) / 2.0);
            }

            double xd = (double)x / ratiox;
            double yd = (double)y / ratioy;

            result = new Point((int)xd, (int)yd);

            return (result.X >= 0 && result.X < pictureBox1.Image.Width && result.Y >= 0 && result.Y < pictureBox1.Image.Height);
        }

        public void FreeMouse()
        {
            mousecaptured = false;
            Cursor.Show();
            Cursor.Clip = Screen.PrimaryScreen.Bounds;
        }

        private void DisplayForm_KeyDown(object sender, KeyEventArgs e)
        {
            // odfiltrovat: minimálně F1-F12, win key, alt
            // pokud bysme chtěli posílat, museli bysme si zaregistrovat keyboardhook
            // bez toho sice dostanu event, ale zároveň na to zareaguje muj windows a vmíchá se mi do toho
            if (mousecaptured)
            {
                if (e.KeyCode >= Keys.F1 && e.KeyCode <= Keys.F12) return;
                if (e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin) return;
                if (e.KeyCode == Keys.Alt) return;

                byte[] b = new byte[2];
                b[0] = 11;
                b[1] = (byte)e.KeyValue;
                try { socket.Send(b); }
                catch { UpdateConnectedStatus(); }
            }
        }

        private void DisplayForm_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin) return;
            if (e.KeyCode == Keys.Alt) return;

            switch (e.KeyCode)
            {
                case Keys.F1:
                    {
                        bool b = heartbeatTimer.Enabled;
                        heartbeatTimer.Enabled = false;
                        MessageBox.Show("Click inside the DisplayWindow to capture mouse and control the cursor on the remote computer.\r\n\r\nPress CTRL+F10 to release mouse.\r\n\r\nF1 - this help\r\nF2 - set the size of DisplayWindow to the resolution of the remote computer (or closest possible)\r\nF3 - request picture of remote screen\r\nF4 - OnScreenKeyboard\r\nF5 - decrease image quality\r\nF6 - increase image quality\r\nF7 - toggle requesting screens automatically every 0.5s");
                        heartbeatTimer.Enabled = b;
                        break;
                    }
                case Keys.F2:
                    {
                        this.ClientSize = new Size(pictureBox1.Image.Width + this.Padding.Left + this.Padding.Right, pictureBox1.Image.Height + this.Padding.Top + this.Padding.Bottom);
                        if (mousecaptured) Cursor.Clip = new Rectangle(pictureBox1.PointToScreen(Point.Empty), pictureBox1.Size);
                        break;
                    }
                case Keys.F3:
                    {
                        if (!autorequest)
                        {
                            try
                            {
                                byte[] b = new byte[1];
                                b[0] = 0;
                                socket.Send(b);

                                ReceivePicture();
                            }
                            catch { UpdateConnectedStatus(); }
                        }
                        break;
                    }
                case Keys.F4:
                    {
                        byte[] b = new byte[1];
                        b[0] = 8;
                        try
                        { socket.Send(b); }
                        catch { UpdateConnectedStatus(); }
                        break;
                    }
                case Keys.F5:
                    {
                        byte[] b = new byte[1];
                        b[0] = 9;
                        try
                        { socket.Send(b); }
                        catch { UpdateConnectedStatus(); }
                        break;
                    }
                case Keys.F6:
                    {
                        byte[] b = new byte[1];
                        b[0] = 10;
                        try
                        { socket.Send(b); }
                        catch { UpdateConnectedStatus(); }
                        break;
                    }
                case Keys.F7:
                    {
                        autorequest = !autorequest;
                        break;
                    }
                case Keys.F10:
                    {
                        if (mousecaptured && e.Control)
                        {
                            FreeMouse();

                            byte[] b = new byte[2];
                            b[0] = 12;  // key up
                            b[1] = 0x11;  // VK_CONTROL
                            try
                            { socket.Send(b); }
                            catch { UpdateConnectedStatus(); }
                        }
                        break;
                    }
                default:
                    {
                        if (e.KeyCode >= Keys.F1 && e.KeyCode <= Keys.F12) break;
                        if (mousecaptured)
                        {
                            byte[] b = new byte[2];
                            b[0] = 12;
                            b[1] = (byte)e.KeyValue;
                            try
                            { socket.Send(b); }
                            catch { UpdateConnectedStatus(); }
                        }
                        break;
                    }
            }
        }

        public void ReceivePicture()
        {
            try
            {
                byte[] lb = new byte[4];
                socket.Receive(lb);
                int l = lb[1];
                l <<= 8;
                l |= lb[2];
                l <<= 8;
                l |= lb[3];

                byte[] buff = new byte[l];
                int received = 0;
                while (received < l)
                {
                    int r = socket.Receive(buff, received, l - received, SocketFlags.None);
                    received += r;
                }

                MemoryStream ms = new MemoryStream(buff);
                Bitmap b = new Bitmap(ms);
                ms.Dispose();
                if (lb[0] == 1)
                {
                    IntPtr hbm = b.GetHbitmap();
                    Program.SelectObject(hdc1, hbmprev);
                    Program.SelectObject(hdc2, hbm);
                    Program.BitBlt(hdc1, 0, 0, b.Width, b.Height, hdc2, 0, 0, /*SRCINVERT*/0x00660046);
                    b.Dispose();
                    Program.DeleteObject(hbm);
                    b = Bitmap.FromHbitmap(hbmprev);
                    Image im = pictureBox1.Image;
                    pictureBox1.Image = b;
                    if (im != null) im.Dispose();
                }
                else
                {
                    Image im = pictureBox1.Image;
                    pictureBox1.Image = b;
                    if (hbmprev != IntPtr.Zero) Program.DeleteObject(hbmprev);
                    hbmprev = b.GetHbitmap();
                    if (im != null) im.Dispose();
                }
            }
            catch { UpdateConnectedStatus(); }
        }

        private void DisplayForm_MouseLeave(object sender, EventArgs e)
        {
            if (mousecaptured) FreeMouse();  // to by se nemělo stát ale ok
        }

        private void heartbeatTimer_Tick(object sender, EventArgs e)
        {
            if (socket == null) return;

            if (autorequest)
            {
                try
                {
                    byte[] b = new byte[1];
                    b[0] = 0;
                    socket.Send(b);
                    ReceivePicture();
                }
                catch { UpdateConnectedStatus(); }
            }
            else
            {
                try
                {
                    byte[] b = new byte[1];
                    b[0] = 255;
                    socket.Send(b);
                }
                catch { UpdateConnectedStatus(); }
            }
        }
    }
}
