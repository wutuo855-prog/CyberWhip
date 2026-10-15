using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace CyberWhip
{
    internal sealed class BoundWindow
    {
        internal IntPtr Handle;
        internal uint ProcessId;
        internal string Title = "";
        internal bool Bind(IntPtr window)
        {
            uint pid;
            if (window == IntPtr.Zero || !Native.IsWindow(window)) return false;
            Native.GetWindowThreadProcessId(window, out pid);
            if (pid == Process.GetCurrentProcess().Id) return false;
            string title = Native.Title(window);
            if (String.IsNullOrWhiteSpace(title)) return false;
            try
            {
                string name = Process.GetProcessById((int)pid).ProcessName.ToLowerInvariant();
                if (name == "explorer" || name == "shellexperiencehost" || name == "startmenuexperiencehost" || name == "searchhost") return false;
            }
            catch { return false; }
            Handle = window; ProcessId = pid; Title = title;
            return true;
        }
        internal bool IsValid()
        {
            uint pid;
            if (Handle == IntPtr.Zero || !Native.IsWindow(Handle) || Native.IsIconic(Handle)) return false;
            Native.GetWindowThreadProcessId(Handle, out pid);
            return pid == ProcessId;
        }
        internal bool IsForeground() { return IsValid() && Native.GetForegroundWindow() == Handle; }
    }
    internal sealed class MacroSender : IDisposable
    {
        private readonly Timer timer = new Timer();
        private readonly Timer restoreTimer = new Timer();
        private readonly BoundWindow target;
        private readonly Action<string> report;
        private readonly Stopwatch watch = new Stopwatch();
        private string text;
        private int stage;
        private long nextAt;
        private bool terminalInterrupt, enter, clipboard;
        private IDataObject oldClipboard;
        private string clipboardText;
        private bool pending;
        internal MacroSender(BoundWindow bound, Action<string> status)
        {
            target = bound; report = status;
            timer.Interval = 30;
            timer.Tick += Tick;
            restoreTimer.Interval = 900;
            restoreTimer.Tick += delegate { RestoreClipboard(); };
        }
        internal bool Send(string phrase, Config config)
        {
            if (pending) { report("上一次催促尚未发送完成。"); return false; }
            if (!target.IsForeground()) { report("先绑定并聚焦模型窗口的输入框；本次只播放音效。"); return false; }
            text = (phrase ?? "").Replace("\r", " ").Replace("\n", " ");
            if (text.Length > 500) text = text.Substring(0, 500);
            if (String.IsNullOrWhiteSpace(text)) return false;
            terminalInterrupt = config.Mode == 2 && config.InterruptTerminal;
            enter = config.AutoEnter;
            clipboard = config.UseClipboard;
            stage = 0; nextAt = 0; pending = true;
            watch.Restart(); timer.Start();
            return true;
        }
        private void Tick(object sender, EventArgs args)
        {
            if (!pending) return;
            if (!target.IsForeground()) { Finish("窗口焦点已变化，本次后续按键已取消。"); return; }
            if (watch.ElapsedMilliseconds > 3000) { Finish("按键发送超时，请松开 Ctrl / Alt / Shift 后重试。"); return; }
            if (watch.ElapsedMilliseconds < nextAt || Native.ModifiersDown()) return;
            try
            {
                if (stage == 0)
                {
                    stage = 1;
                    if (terminalInterrupt)
                    {
                        if (!Native.SendKeys(new Native.INPUT[] { Native.Key(0x11, false), Native.Key(0x43, false), Native.Key(0x43, true), Native.Key(0x11, true) }))
                        { Finish("目标窗口没有接受模拟按键；请使用普通用户权限运行目标窗口。"); return; }
                        nextAt = watch.ElapsedMilliseconds + 350;
                        return;
                    }
                }
                if (stage == 1)
                {
                    bool ok;
                    if (clipboard)
                    {
                        RestoreClipboard();
                        oldClipboard = Clipboard.GetDataObject();
                        Clipboard.SetText(text);
                        clipboardText = text;
                        ok = Native.SendKeys(new Native.INPUT[] { Native.Key(0x11, false), Native.Key(0x56, false), Native.Key(0x56, true), Native.Key(0x11, true) });
                        restoreTimer.Start();
                    }
                    else ok = Native.SendText(text);
                    if (!ok) { Finish("目标窗口没有接受文字；可尝试设置中的剪贴板粘贴方式。"); return; }
                    stage = 2; nextAt = watch.ElapsedMilliseconds + (clipboard ? 180 : 90);
                    if (!enter) { Finish("催促语已填入输入框，请按 Enter 发送。"); return; }
                    return;
                }
                if (stage == 2)
                {
                    if (!Native.SendKeys(new Native.INPUT[] { Native.Key(0x0D, false), Native.Key(0x0D, true) }))
                    { Finish("文字已填入，但发送按键未成功。"); return; }
                    Finish("已向绑定窗口发送催促语。");
                }
            }
            catch (Exception ex) { Finish("发送未完成：" + ex.Message); }
        }
        private void Finish(string status)
        {
            pending = false; timer.Stop(); watch.Stop(); report(status);
        }
        private void RestoreClipboard()
        {
            restoreTimer.Stop();
            if (clipboardText == null) return;
            try
            {
                if (Clipboard.ContainsText() && Clipboard.GetText() == clipboardText)
                {
                    if (oldClipboard == null) Clipboard.Clear();
                    else Clipboard.SetDataObject(oldClipboard, true);
                }
            }
            catch { }
            finally { clipboardText = null; oldClipboard = null; }
        }
        internal void CancelPending()
        {
            if (!pending) return;
            pending = false; timer.Stop(); watch.Stop();
        }
        public void Dispose() { CancelPending(); RestoreClipboard(); timer.Dispose(); restoreTimer.Dispose(); }
    }
}
