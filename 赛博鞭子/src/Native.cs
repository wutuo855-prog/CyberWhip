using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace CyberWhip
{
    internal static class Native
    {
        internal const int WM_HOTKEY = 0x0312;
        internal const int WM_NCHITTEST = 0x0084;
        internal const int WM_MOUSEACTIVATE = 0x0021;
        internal const int WH_MOUSE_LL = 14;
        internal const int WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202;
        internal const int WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205;
        internal const uint KEYEVENTF_KEYUP = 2, KEYEVENTF_UNICODE = 4;
        internal const int WS_EX_LAYERED = 0x80000, WS_EX_TRANSPARENT = 0x20;
        internal const int WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
        internal const int WS_EX_TOPMOST = 0x00000008;
        internal static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        internal const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002;
        internal const uint SWP_NOACTIVATE = 0x0010, SWP_NOOWNERZORDER = 0x0200;

        [StructLayout(LayoutKind.Sequential)] internal struct POINT
        {
            internal int X, Y;
            internal POINT(int x, int y) { X = x; Y = y; }
        }
        [StructLayout(LayoutKind.Sequential)] internal struct SIZE
        {
            internal int CX, CY;
            internal SIZE(int x, int y) { CX = x; CY = y; }
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] internal struct BLENDFUNCTION
        {
            internal byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct BITMAPINFOHEADER
        {
            internal uint Size;
            internal int Width, Height;
            internal ushort Planes, BitCount;
            internal uint Compression, SizeImage;
            internal int XPelsPerMeter, YPelsPerMeter;
            internal uint ClrUsed, ClrImportant;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct BITMAPINFO
        {
            internal BITMAPINFOHEADER Header;
            internal uint Colors;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct MOUSEINPUT
        {
            internal int Dx, Dy;
            internal uint MouseData, Flags, Time;
            internal UIntPtr ExtraInfo;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct KEYBDINPUT
        {
            internal ushort Vk, Scan;
            internal uint Flags, Time;
            internal UIntPtr ExtraInfo;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct HARDWAREINPUT
        {
            internal uint Msg;
            internal ushort ParamL, ParamH;
        }
        [StructLayout(LayoutKind.Explicit)] internal struct INPUTUNION
        {
            [FieldOffset(0)] internal MOUSEINPUT Mouse;
            [FieldOffset(0)] internal KEYBDINPUT Key;
            [FieldOffset(0)] internal HARDWAREINPUT Hardware;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct INPUT
        {
            internal uint Type;
            internal INPUTUNION Data;
        }

        internal delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] internal static extern int GetWindowLong(IntPtr window, int index);
        [DllImport("user32.dll")] internal static extern IntPtr GetWindow(IntPtr window, uint command);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetWindowText(IntPtr window, StringBuilder text, int maxCount);
        [DllImport("user32.dll")] internal static extern bool GetCursorPos(out POINT point);
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("user32.dll", SetLastError = true)] internal static extern uint SendInput(uint count, INPUT[] inputs, int size);
        [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint threadId);
        [DllImport("user32.dll")] internal static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr GetModuleHandle(string moduleName);
        [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] internal static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("winmm.dll")] internal static extern uint timeBeginPeriod(uint period);
        [DllImport("winmm.dll")] internal static extern uint timeEndPeriod(uint period);
        [DllImport("user32.dll")] internal static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] internal static extern int ReleaseDC(IntPtr window, IntPtr dc);
        [DllImport("gdi32.dll")] internal static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] internal static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] internal static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] internal static extern IntPtr CreateDIBSection(IntPtr dc, ref BITMAPINFO info, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool UpdateLayeredWindow(IntPtr window, IntPtr screenDC, ref POINT position, ref SIZE size, IntPtr sourceDC, ref POINT source, uint colorKey, ref BLENDFUNCTION blend, uint flags);

        internal static string Title(IntPtr window)
        {
            StringBuilder value = new StringBuilder(512);
            GetWindowText(window, value, value.Capacity);
            return value.ToString();
        }
        internal static bool IsDown(int key) { return (GetAsyncKeyState(key) & 0x8000) != 0; }
        internal static bool ModifiersDown()
        {
            return IsDown(0x10) || IsDown(0x11) || IsDown(0x12) || IsDown(0x5B) || IsDown(0x5C);
        }
        internal static void EnableDpi()
        {
            try { if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return; }
            catch (EntryPointNotFoundException) { }
            SetProcessDPIAware();
        }
        internal static INPUT Key(ushort key, bool up)
        {
            INPUT input = new INPUT();
            input.Type = 1;
            input.Data.Key.Vk = key;
            input.Data.Key.Flags = up ? KEYEVENTF_KEYUP : 0;
            return input;
        }
        internal static bool SendKeys(INPUT[] inputs)
        {
            return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT))) == inputs.Length;
        }
        internal static bool SendText(string text)
        {
            INPUT[] values = new INPUT[text.Length * 2];
            for (int i = 0; i < text.Length; i++)
            {
                values[2 * i].Type = values[2 * i + 1].Type = 1;
                values[2 * i].Data.Key.Scan = values[2 * i + 1].Data.Key.Scan = text[i];
                values[2 * i].Data.Key.Flags = KEYEVENTF_UNICODE;
                values[2 * i + 1].Data.Key.Flags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;
            }
            return SendKeys(values);
        }
    }

    internal sealed class HotKeyWindow : NativeWindow, IDisposable
    {
        private readonly Action<int> handler;
        internal readonly System.Collections.Generic.List<string> Failures = new System.Collections.Generic.List<string>();
        internal HotKeyWindow(Action<int> callback)
        {
            handler = callback;
            CreateParams p = new CreateParams();
            p.Caption = "CyberWhip hotkeys";
            p.Parent = new IntPtr(-3);
            CreateHandle(p);
            Register(1, 'W', "Alt+Shift+W");
            Register(2, 'Q', "Alt+Shift+Q");
            Register(3, 'B', "Alt+Shift+B");
        }
        private void Register(int id, char key, string label)
        {
            if (!Native.RegisterHotKey(Handle, id, 0x4005, key)) Failures.Add(label);
        }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == Native.WM_HOTKEY) handler(message.WParam.ToInt32());
            base.WndProc(ref message);
        }
        public void Dispose()
        {
            for (int i = 1; i <= 3; i++) Native.UnregisterHotKey(Handle, i);
            DestroyHandle();
        }
    }
}
