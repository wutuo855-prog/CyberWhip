using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Media;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace CyberWhip
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool first;
            using (Mutex mutex = new Mutex(true, "Local\\CyberWhipOffline_" + Environment.UserName, out first))
            {
                if (!first)
                {
                    MessageBox.Show("赛博鞭子已经在运行，请查看任务栏右下角的托盘图标（也可能在隐藏图标中）。", "赛博鞭子", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                try
                {
                    Native.EnableDpi();
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                    Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                    {
                        ConfigStore.Log(e.Exception);
                        MessageBox.Show("运行遇到问题：" + e.Exception.Message + "\r\n日志位置：" + ConfigStore.DirectoryPath, "赛博鞭子", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    };
                    Application.Run(new WhipContext());
                }
                catch (Exception ex)
                {
                    ConfigStore.Log(ex);
                    MessageBox.Show("启动失败：" + ex.Message + "\r\n日志位置：" + ConfigStore.DirectoryPath, "赛博鞭子", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally { mutex.ReleaseMutex(); }
            }
        }
    }
    internal sealed class SoundBank : IDisposable
    {
        private readonly List<SoundPlayer> players = new List<SoundPlayer>();
        private readonly List<Stream> streams = new List<Stream>();
        internal SoundBank()
        {
            foreach (char name in "ABCDE")
            {
                Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("CyberWhip.Sounds." + name + ".wav");
                if (s == null) continue;
                try
                {
                    SoundPlayer player = new SoundPlayer(s);
                    player.Load();
                    streams.Add(s); players.Add(player);
                }
                catch { s.Dispose(); }
            }
        }
        internal void Play(Random random)
        {
            if (players.Count == 0) return;
            try { players[random.Next(players.Count)].Play(); }
            catch { }
        }
        public void Dispose()
        {
            foreach (SoundPlayer p in players) p.Dispose();
            foreach (Stream s in streams) s.Dispose();
        }
    }
    internal sealed class WhipContext : ApplicationContext
    {
        internal Config Config;
        internal readonly BoundWindow Target = new BoundWindow();
        internal string Status = "先聚焦模型输入框，按 Alt+Shift+B 绑定。";
        internal static readonly string[] Modes = { "仅动画和音效", "网页聊天", "终端助手", "内网 API 聊天" };
        private readonly NotifyIcon tray;
        private readonly HotKeyWindow hotkeys;
        private readonly WhipOverlay overlay;
        private readonly WhipPhysics physics = new WhipPhysics();
        private readonly MacroSender macro;
        private readonly SoundBank sounds;
        private readonly Timer frameTimer = new Timer(), bindTimer = new Timer();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Random random = new Random();
        private readonly Native.HookProc mouseCallback;
        private IntPtr mouseHook;
        private int swallowedButtons, whipSession;
        private bool active, quitting, preciseTimer;
        private long lastFrame, lastCrack = -10000, lastAction = -30000, dropAt;
        private double accumulator, mouseX, mouseY;
        private SettingsForm settings;
        private ChatForm chat;
        internal WhipContext()
        {
            Config = ConfigStore.Load();
            overlay = new WhipOverlay();
            // Create the non-activating UI handle once for cross-thread delivery.
            IntPtr handle = overlay.Handle;
            macro = new MacroSender(Target, SetStatus);
            sounds = new SoundBank();
            mouseCallback = MouseEvent;
            tray = new NotifyIcon();
            tray.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            tray.Text = "赛博鞭子 · 点击召唤 / 右键设置";
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("召唤 / 收鞭    Alt+Shift+W", null, delegate { Toggle(); });
            menu.Items.Add("抽一下            Alt+Shift+Q", null, delegate { Crack(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("3 秒后绑定模型窗口", null, delegate { BindAfterDelay(); });
            menu.Items.Add("设置", null, delegate { ShowSettings(); });
            menu.Items.Add("打开内网 API 聊天", null, delegate { ShowChat(); });
            menu.Items.Add("切换静音", null, delegate { Config.Sound = !Config.Sound; Save(); SetStatus(Config.Sound ? "音效已开启。" : "已静音。"); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { Quit(); });
            tray.ContextMenuStrip = menu;
            tray.MouseClick += delegate(object sender, MouseEventArgs args) { if (args.Button == MouseButtons.Left) Toggle(); };
            tray.Visible = true;
            hotkeys = new HotKeyWindow(delegate(int id)
            {
                if (id == 1) Toggle();
                else if (id == 2) Crack();
                else if (id == 3) BindNow();
            });
            frameTimer.Interval = 16;
            frameTimer.Tick += Frame;
            bindTimer.Interval = 3000;
            bindTimer.Tick += delegate { bindTimer.Stop(); BindNow(); };
            if (ConfigStore.FirstRun || !String.IsNullOrEmpty(ConfigStore.LoadWarning)) ShowSettings();
            if (!String.IsNullOrEmpty(ConfigStore.LoadWarning)) SetStatus(ConfigStore.LoadWarning);
            if (hotkeys.Failures.Count > 0)
            {
                SetStatus("快捷键已被占用：" + String.Join("、", hotkeys.Failures.ToArray()) + "；可使用托盘菜单。 ");
                Balloon(Status);
            }
        }
        internal void Ui(Action action)
        {
            if (quitting || overlay.IsDisposed) return;
            try { overlay.BeginInvoke((MethodInvoker)delegate { if (!quitting) action(); }); }
            catch (InvalidOperationException) { }
        }
        internal void SetStatus(string value)
        {
            Status = value ?? "";
            if (settings != null && !settings.IsDisposed) settings.RefreshStatus();
            if (chat != null && !chat.IsDisposed) chat.RefreshStatus();
        }
        private void Balloon(string text)
        {
            tray.ShowBalloonTip(3500, "赛博鞭子", text, ToolTipIcon.Info);
        }
        internal void Save()
        {
            try { ConfigStore.Save(Config); }
            catch (Exception ex) { SetStatus("设置未能保存：" + ex.Message); }
        }
        internal bool Apply(Config newConfig)
        {
            try
            {
                newConfig.Normalize();
                newConfig.TotalCracks = Config.TotalCracks;
                ConfigStore.Save(newConfig);
                macro.CancelPending();
                Config = newConfig;
                SetStatus("设置已保存 · " + Modes[Config.Mode]);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("设置未能保存：" + ex.Message, "赛博鞭子", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
        internal void ShowSettings()
        {
            StopWhip();
            if (settings == null || settings.IsDisposed) settings = new SettingsForm(this);
            settings.Show(); settings.Activate(); settings.RefreshStatus();
        }
        internal ChatForm ShowChat()
        {
            if (chat == null || chat.IsDisposed) chat = new ChatForm(this);
            chat.Show(); chat.Activate();
            return chat;
        }
        internal void BindAfterDelay()
        {
            StopWhip();
            if (settings != null && !settings.IsDisposed) settings.Hide();
            if (chat != null && !chat.IsDisposed) chat.Hide();
            bindTimer.Stop(); bindTimer.Start();
            SetStatus("3 秒内切换到模型窗口，并点进输入框。");
            Balloon(Status);
        }
        private void BindNow()
        {
            if (Target.Bind(Native.GetForegroundWindow()))
            {
                SetStatus("已绑定：" + Target.Title);
                Balloon("已绑定模型窗口。保持输入框焦点，按 Alt+Shift+W 召唤鞭子。");
            }
            else
            {
                SetStatus("未绑定：请切换到模型网页或终端，再按 Alt+Shift+B。");
                Balloon(Status);
            }
        }
        internal void Toggle()
        {
            if (active) { BeginDrop(); return; }
            if (Config.Mode == 3) ShowChat();
            else if (Target.IsValid()) Native.SetForegroundWindow(Target.Handle);
            Native.POINT p;
            Native.GetCursorPos(out p);
            mouseX = p.X; mouseY = p.Y;
            physics.Spawn(p.X, p.Y, Config.RopeScale);
            active = true; accumulator = 0;
            whipSession++;
            if (!preciseTimer) preciseTimer = Native.timeBeginPeriod(1) == 0;
            lastFrame = clock.ElapsedMilliseconds;
            if (mouseHook == IntPtr.Zero)
                mouseHook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, mouseCallback, Native.GetModuleHandle(null), 0);
            if (mouseHook == IntPtr.Zero)
                SetStatus("鼠标收鞭未启用；请用 Alt+Shift+W 或 Esc 收鞭。");
            else if ((Config.Mode == 1 || Config.Mode == 2) && !Target.IsValid())
                SetStatus("未绑定窗口 · 本次先体验动画和音效。Alt+Shift+B 可绑定。");
            else SetStatus("移动鼠标挥鞭 · 左键抽一下 · 右键 / Esc 收起");
            frameTimer.Start();
            Draw();
        }
        private IntPtr MouseEvent(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0)
            {
                int msg = wParam.ToInt32();
                int bit = (msg == Native.WM_LBUTTONDOWN || msg == Native.WM_LBUTTONUP) ? 1 : 2;
                if ((msg == Native.WM_LBUTTONUP || msg == Native.WM_RBUTTONUP) && (swallowedButtons & bit) != 0)
                {
                    swallowedButtons &= ~bit;
                    if (!active && swallowedButtons == 0) Ui(ReleaseHook);
                    return new IntPtr(1);
                }
                if (active && msg == Native.WM_LBUTTONDOWN)
                {
                    swallowedButtons |= 1;
                    int session = whipSession;
                    // Keep the low-level hook fast: sound, UI and macros run later
                    // on the UI queue. Recheck the session so a canceled click cannot
                    // fire after another whip has been summoned.
                    Ui(delegate { if (active && !physics.Dropping && session == whipSession) Crack(); });
                    return new IntPtr(1);
                }
                if (active && msg == Native.WM_RBUTTONDOWN)
                {
                    swallowedButtons |= 2;
                    BeginDrop();
                    int session = whipSession;
                    Ui(delegate
                    {
                        if (active && session == whipSession)
                        {
                            StopWhip();
                            SetStatus("已收起鞭子 · Alt+Shift+W 可再次召唤。");
                        }
                    });
                    return new IntPtr(1);
                }
            }
            return Native.CallNextHookEx(mouseHook, code, wParam, lParam);
        }
        private void BeginDrop()
        {
            if (!active || physics.Dropping) return;
            macro.CancelPending();
            physics.Drop(); dropAt = clock.ElapsedMilliseconds;
        }
        private void ReleaseHook()
        {
            if (mouseHook != IntPtr.Zero) Native.UnhookWindowsHookEx(mouseHook);
            mouseHook = IntPtr.Zero;
        }
        private void StopWhip()
        {
            active = false;
            frameTimer.Stop();
            if (preciseTimer) { Native.timeEndPeriod(1); preciseTimer = false; }
            physics.Points = null;
            overlay.Hide();
            macro.CancelPending();
            if (swallowedButtons == 0) ReleaseHook();
        }
        private void Frame(object sender, EventArgs args)
        {
            if (!active) return;
            try
            {
                long now = clock.ElapsedMilliseconds;
                if (Native.IsDown(0x1B)) { StopWhip(); return; }
                Native.POINT p;
                Native.GetCursorPos(out p);
                double elapsed = Math.Max(1, Math.Min(100, now - lastFrame));
                mouseX = p.X; mouseY = p.Y;
                accumulator += elapsed / 1000;
                lastFrame = now;
                int steps = 0;
                while (accumulator >= 1.0 / 60 && steps < 4)
                {
                    physics.Step(mouseX, mouseY, SystemInformation.VirtualScreen);
                    accumulator -= 1.0 / 60; steps++;
                }
                if (steps == 4) accumulator = Math.Min(accumulator, 1.0 / 60);
                if (physics.Dropping && (physics.OffScreen(SystemInformation.VirtualScreen) || now - dropAt > 2000)) { StopWhip(); return; }
                Draw();
            }
            catch (Exception ex)
            {
                StopWhip(); ConfigStore.Log(ex);
                SetStatus("绘制已停止：" + ex.Message);
                Balloon(Status);
            }
        }
        private void Draw()
        {
            string label = "啪 × " + Config.TotalCracks + "  ·  " + Status;
            overlay.Render(physics, label, (clock.ElapsedMilliseconds - lastCrack) / 1000.0, SystemInformation.VirtualScreen);
        }
        private void Crack()
        {
            long now = clock.ElapsedMilliseconds;
            lastCrack = now;
            if (Config.TotalCracks < Int32.MaxValue) Config.TotalCracks++;
            if (Config.Sound) sounds.Play(random);
            if (Config.Mode == 0) { SetStatus("啪！左键抽一下 · 右键 / Esc 收鞭"); return; }
            if (now - lastAction < Config.CooldownMs) { SetStatus("啪！催促语冷却中，本次只播放音效。"); return; }
            string phrase = Config.Phrases[random.Next(Config.Phrases.Length)];
            bool sent;
            if (Config.Mode == 3)
            {
                ChatForm target = ShowChat();
                sent = target.Nudge(phrase);
            }
            else sent = macro.Send(phrase, Config);
            if (sent) lastAction = now;
        }
        private void Quit()
        {
            if (quitting) return;
            quitting = true;
            bindTimer.Stop(); StopWhip(); ReleaseHook();
            if (chat != null && !chat.IsDisposed) chat.Shutdown();
            Save();
            tray.Visible = false;
            tray.Dispose(); hotkeys.Dispose(); sounds.Dispose(); macro.Dispose();
            frameTimer.Dispose(); bindTimer.Dispose(); overlay.Dispose();
            if (settings != null && !settings.IsDisposed) settings.Dispose();
            if (chat != null && !chat.IsDisposed) chat.Dispose();
            ExitThread();
        }
    }
}
