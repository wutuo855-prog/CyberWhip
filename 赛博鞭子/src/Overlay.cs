using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.ComponentModel;
using System.Windows.Forms;

namespace CyberWhip
{
    internal sealed class WhipOverlay : Form
    {
        private IntPtr memoryDC, dib, oldBitmap;
        private Bitmap surface;
        private Graphics painter;
        private int capacityW, capacityH;
        private readonly Font hudFont = new Font("Microsoft YaHei UI", 10, FontStyle.Regular);
        internal WhipOverlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Size = new Size(1, 1);
            Text = "CyberWhip overlay";
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams p = base.CreateParams;
                p.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW | Native.WS_EX_TOPMOST;
                return p;
            }
        }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == Native.WM_MOUSEACTIVATE) { message.Result = new IntPtr(3); return; }
            if (message.Msg == Native.WM_NCHITTEST) { message.Result = new IntPtr(-1); return; }
            base.WndProc(ref message);
        }
        private void ReleaseSurface()
        {
            if (painter != null) { painter.Dispose(); painter = null; }
            if (surface != null) { surface.Dispose(); surface = null; }
            if (memoryDC != IntPtr.Zero && oldBitmap != IntPtr.Zero) Native.SelectObject(memoryDC, oldBitmap);
            if (dib != IntPtr.Zero) Native.DeleteObject(dib);
            if (memoryDC != IntPtr.Zero) Native.DeleteDC(memoryDC);
            dib = memoryDC = oldBitmap = IntPtr.Zero;
            capacityW = capacityH = 0;
        }
        private void EnsureSurface(int width, int height)
        {
            if (surface != null && width <= capacityW && height <= capacityH) return;
            ReleaseSurface();
            capacityW = ((width + 127) / 128) * 128;
            capacityH = ((height + 127) / 128) * 128;
            memoryDC = Native.CreateCompatibleDC(IntPtr.Zero);
            if (memoryDC == IntPtr.Zero) throw new Win32Exception("无法创建桌面绘图环境。");
            Native.BITMAPINFO info = new Native.BITMAPINFO();
            info.Header.Size = 40;
            info.Header.Width = capacityW;
            info.Header.Height = -capacityH;
            info.Header.Planes = 1;
            info.Header.BitCount = 32;
            IntPtr bits;
            dib = Native.CreateDIBSection(memoryDC, ref info, 0, out bits, IntPtr.Zero, 0);
            if (dib == IntPtr.Zero || bits == IntPtr.Zero) throw new Win32Exception("无法创建透明鞭子画布。");
            oldBitmap = Native.SelectObject(memoryDC, dib);
            surface = new Bitmap(capacityW, capacityH, capacityW * 4, PixelFormat.Format32bppPArgb, bits);
            painter = Graphics.FromImage(surface);
            painter.SmoothingMode = SmoothingMode.AntiAlias;
            painter.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            painter.CompositingQuality = CompositingQuality.HighSpeed;
        }
        private static PointF Point(RopePoint[] points, int index, int ox, int oy)
        {
            if (index < 0) return new PointF((float)(2 * points[0].X - points[1].X - ox), (float)(2 * points[0].Y - points[1].Y - oy));
            if (index >= points.Length)
            {
                int n = points.Length;
                return new PointF((float)(2 * points[n - 1].X - points[n - 2].X - ox), (float)(2 * points[n - 1].Y - points[n - 2].Y - oy));
            }
            return new PointF((float)(points[index].X - ox), (float)(points[index].Y - oy));
        }
        private static void Segment(Graphics g, Pen pen, RopePoint[] points, int index, int ox, int oy)
        {
            PointF p0 = Point(points, index - 1, ox, oy), p1 = Point(points, index, ox, oy);
            PointF p2 = Point(points, index + 1, ox, oy), p3 = Point(points, index + 2, ox, oy);
            PointF c1 = new PointF(p1.X + (p2.X - p0.X) / 6, p1.Y + (p2.Y - p0.Y) / 6);
            PointF c2 = new PointF(p2.X - (p3.X - p1.X) / 6, p2.Y - (p3.Y - p1.Y) / 6);
            g.DrawBezier(pen, p1, c1, c2, p2);
        }
        private static GraphicsPath Rounded(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
        internal void Render(WhipPhysics physics, string caption, double flashAge, Rectangle screen)
        {
            if (physics.Points == null) return;
            RopePoint[] points = physics.Points;
            double minX = points[0].X, maxX = minX, minY = points[0].Y, maxY = minY;
            foreach (RopePoint p in points)
            {
                minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y);
                maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y);
            }
            bool showCaption = !physics.Dropping && !String.IsNullOrEmpty(caption);
            float bubbleX = (float)Math.Max(screen.Left + 8, Math.Min(screen.Right - 370, points[0].X + 22));
            float bubbleY = (float)Math.Max(screen.Top + 8, Math.Min(screen.Bottom - 48, points[0].Y + 28));
            if (showCaption)
            {
                minX = Math.Min(minX, bubbleX); minY = Math.Min(minY, bubbleY);
                maxX = Math.Max(maxX, bubbleX + 364); maxY = Math.Max(maxY, bubbleY + 42);
            }
            Rectangle area = Rectangle.FromLTRB((int)Math.Floor(minX - 32), (int)Math.Floor(minY - 32), (int)Math.Ceiling(maxX + 32), (int)Math.Ceiling(maxY + 32));
            area = Rectangle.Intersect(area, screen);
            if (area.Width <= 0 || area.Height <= 0) { Hide(); return; }
            EnsureSurface(area.Width, area.Height);
            painter.ResetTransform();
            painter.Clear(Color.Transparent);
            double scale = physics.Scale;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < points.Length - 1; i++)
                {
                    float thickness = (float)((5.5 - i / 26.0 * 2.5 + (i < 2 ? 6 : 0)) * scale);
                    using (Pen pen = new Pen(pass == 0 ? Color.FromArgb(245, 255, 255, 255) : Color.FromArgb(255, 24, 27, 37), thickness + (pass == 0 ? (float)(4 * scale) : 0)))
                    {
                        pen.StartCap = pen.EndCap = LineCap.Round;
                        pen.LineJoin = LineJoin.Round;
                        Segment(painter, pen, points, i, area.X, area.Y);
                    }
                }
            }
            PointF handle = Point(points, 0, area.X, area.Y);
            using (Brush b = new SolidBrush(Color.FromArgb(255, 93, 230, 220))) painter.FillEllipse(b, handle.X - (float)(4 * scale), handle.Y - (float)(4 * scale), (float)(8 * scale), (float)(8 * scale));
            if (!physics.Dropping && flashAge >= 0 && flashAge < 0.23)
            {
                PointF tip = Point(points, points.Length - 1, area.X, area.Y);
                float radius = (float)((10 + flashAge * 90) * scale);
                using (Pen pen = new Pen(Color.FromArgb((int)(255 * (1 - flashAge / 0.23)), 255, 196, 74), (float)(2 * scale)))
                {
                    for (int i = 0; i < 10; i++)
                    {
                        double a = i * Math.PI / 5;
                        painter.DrawLine(pen, tip.X + (float)Math.Cos(a) * radius * 0.6f, tip.Y + (float)Math.Sin(a) * radius * 0.6f, tip.X + (float)Math.Cos(a) * radius, tip.Y + (float)Math.Sin(a) * radius);
                    }
                }
            }
            if (showCaption)
            {
                RectangleF bubble = new RectangleF(bubbleX - area.X, bubbleY - area.Y, 364, 38);
                using (GraphicsPath path = Rounded(bubble, 9))
                using (Brush b = new SolidBrush(Color.FromArgb(230, 29, 33, 47))) painter.FillPath(b, path);
                using (Brush b = new SolidBrush(Color.FromArgb(255, 228, 239, 251)))
                using (StringFormat format = new StringFormat())
                {
                    format.Trimming = StringTrimming.EllipsisCharacter;
                    format.FormatFlags = StringFormatFlags.NoWrap;
                    format.LineAlignment = StringAlignment.Center;
                    painter.DrawString(caption, hudFont, b, new RectangleF(bubble.X + 12, bubble.Y, bubble.Width - 24, bubble.Height), format);
                }
            }
            painter.Flush();
            Native.POINT destination = new Native.POINT(area.X, area.Y), source = new Native.POINT(0, 0);
            Native.SIZE size = new Native.SIZE(area.Width, area.Height);
            Native.BLENDFUNCTION blend = new Native.BLENDFUNCTION();
            blend.SourceConstantAlpha = 255; blend.AlphaFormat = 1;
            if (!Native.UpdateLayeredWindow(Handle, IntPtr.Zero, ref destination, ref size, memoryDC, ref source, 0, ref blend, 2))
                throw new Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error(), "透明窗口绘制失败。");
            if (!Visible) Show();
            // Showing a form or refocusing Chrome can change native Z order.
            // Reassert the actual HWND order after Show and on every drawn frame,
            // while keeping the model input window as the foreground window.
            if (!Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE | Native.SWP_NOOWNERZORDER))
                throw new Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error(), "无法将鞭子窗口置顶。");
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { ReleaseSurface(); hudFont.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
