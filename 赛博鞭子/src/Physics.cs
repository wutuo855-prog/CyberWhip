using System;
using System.Drawing;

namespace CyberWhip
{
    internal sealed class RopePoint
    {
        internal double X, Y, PX, PY;
        internal RopePoint(double x, double y) { X = PX = x; Y = PY = y; }
    }
    internal sealed class WhipPhysics
    {
        internal const int Count = 28;
        internal RopePoint[] Points;
        internal bool Dropping;
        internal double Scale = 1;
        internal double TipSpeed;
        private double handleAngle, angularSpeed, lastMouseX, lastMouseY;
        private double tipBeforeX, tipBeforeY;
        internal void Spawn(double x, double y, double scale)
        {
            Scale = Math.Max(0.7, Math.Min(2.2, scale));
            Dropping = false;
            Points = new RopePoint[Count];
            for (int i = 0; i < Count; i++)
            {
                double t = i / (double)(Count - 1);
                Points[i] = new RopePoint(x + t * 230 * Scale, y - Math.Sin(t * Math.PI * 0.75) * 170 * Scale);
            }
            handleAngle = -1.12;
            angularSpeed = 0;
            lastMouseX = x; lastMouseY = y;
            TipSpeed = 0;
        }
        internal void Drop() { Dropping = true; }
        private double Length(int index) { return 22 * Scale * (1 - index / 27.0 * 0.4); }
        private static double Clamp(double value, double lo, double hi) { return Math.Max(lo, Math.Min(hi, value)); }
        private static double Wrap(double value)
        {
            while (value > Math.PI) value -= Math.PI * 2;
            while (value < -Math.PI) value += Math.PI * 2;
            return value;
        }
        private void BasePose()
        {
            if (Dropping) return;
            double x = Math.Cos(handleAngle), y = Math.Sin(handleAngle);
            for (int i = 1; i <= 2; i++)
            {
                RopePoint a = Points[i - 1], b = Points[i];
                double stiffness = i == 1 ? 0.9 : 0.8;
                b.X += (a.X + x * Length(i - 1) - b.X) * stiffness;
                b.Y += (a.Y + y * Length(i - 1) - b.Y) * stiffness;
            }
        }
        private void CapStretch()
        {
            for (int i = 0; i < Count - 1; i++)
            {
                RopePoint a = Points[i], b = Points[i + 1];
                double dx = b.X - a.X, dy = b.Y - a.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                double max = Length(i) * 1.2;
                if (distance > max)
                {
                    double k = max / distance;
                    b.X = a.X + dx * k; b.Y = a.Y + dy * k;
                }
            }
        }
        private void BendLimits()
        {
            for (int i = 1; i < Count - 1; i++)
            {
                RopePoint a = Points[i - 1], b = Points[i], c = Points[i + 1];
                double x1 = a.X - b.X, y1 = a.Y - b.Y, x2 = c.X - b.X, y2 = c.Y - b.Y;
                double length1 = Math.Max(0.0001, Math.Sqrt(x1 * x1 + y1 * y1));
                double length2 = Math.Max(0.0001, Math.Sqrt(x2 * x2 + y2 * y2));
                x1 /= length1; y1 /= length1; x2 /= length2; y2 /= length2;
                double t = i / 26.0;
                double max = (16 + 114 * t) * Math.PI / 180;
                double bend = Math.PI - Math.Acos(Clamp(x1 * x2 + y1 * y2, -1, 1));
                if (bend <= max) continue;
                double sign = x1 * y2 - y1 * x2 >= 0 ? 1 : -1;
                double angle = Math.Atan2(y1, x1) + sign * (Math.PI - max);
                double stiffness = 0.8 - 0.68 * t;
                c.X += (b.X + Math.Cos(angle) * length2 - c.X) * stiffness;
                c.Y += (b.Y + Math.Sin(angle) * length2 - c.Y) * stiffness;
            }
        }
        private void Walls(Rectangle bounds)
        {
            if (Dropping) return;
            for (int i = 1; i < Count; i++)
            {
                RopePoint p = Points[i];
                double vx = p.X - p.PX, vy = p.Y - p.PY;
                bool hit = false;
                if (p.X < bounds.Left) { p.X = bounds.Left; if (vx < 0) vx *= -0.42; vy *= 0.86; hit = true; }
                if (p.X > bounds.Right - 1) { p.X = bounds.Right - 1; if (vx > 0) vx *= -0.42; vy *= 0.86; hit = true; }
                if (p.Y < bounds.Top) { p.Y = bounds.Top; if (vy < 0) vy *= -0.42; vx *= 0.86; hit = true; }
                if (p.Y > bounds.Bottom - 1) { p.Y = bounds.Bottom - 1; if (vy > 0) vy *= -0.42; vx *= 0.86; hit = true; }
                if (hit) { p.PX = p.X - vx; p.PY = p.Y - vy; }
            }
        }
        internal void Step(double mouseX, double mouseY, Rectangle bounds)
        {
            if (Points == null) return;
            RopePoint tip = Points[Count - 1];
            tipBeforeX = tip.X; tipBeforeY = tip.Y;
            if (!Dropping)
            {
                double delta = Clamp(((mouseX - lastMouseX) * 0.006 + (mouseY - lastMouseY) * 0.003) / Scale, -1.8, 1.8);
                angularSpeed += Wrap(-1.12 + delta - handleAngle) * 0.7;
                angularSpeed *= 0.13;
                handleAngle = Wrap(handleAngle + angularSpeed);
            }
            for (int i = Dropping ? 0 : 1; i < Count; i++)
            {
                RopePoint p = Points[i];
                double vx = (p.X - p.PX) * 0.96, vy = (p.Y - p.PY) * 0.96;
                p.PX = p.X; p.PY = p.Y;
                p.X += vx; p.Y += vy + (Dropping ? 0.95 : 1.15) * Scale;
            }
            if (!Dropping)
            {
                Points[0].X = Points[0].PX = mouseX;
                Points[0].Y = Points[0].PY = mouseY;
            }
            CapStretch(); Walls(bounds); BasePose();
            for (int iter = 0; iter < 16; iter++)
            {
                for (int i = 0; i < Count - 1; i++)
                {
                    RopePoint a = Points[i], b = Points[i + 1];
                    double dx = b.X - a.X, dy = b.Y - a.Y;
                    double distance = Math.Max(0.0001, Math.Sqrt(dx * dx + dy * dy));
                    double k = (distance - Length(i)) / distance * 0.5;
                    if (i == 0 && !Dropping) { b.X -= dx * k * 2; b.Y -= dy * k * 2; }
                    else { a.X += dx * k; a.Y += dy * k; b.X -= dx * k; b.Y -= dy * k; }
                }
                BendLimits(); BasePose(); CapStretch(); Walls(bounds);
            }
            double tx = tip.X - tipBeforeX, ty = tip.Y - tipBeforeY;
            TipSpeed = Math.Sqrt(tx * tx + ty * ty) * 60 / Scale;
            lastMouseX = mouseX; lastMouseY = mouseY;
        }
        internal bool OffScreen(Rectangle bounds)
        {
            if (Points == null) return true;
            for (int i = 0; i < Count; i++) if (Points[i].Y <= bounds.Bottom + 80 * Scale) return false;
            return true;
        }
    }
}
