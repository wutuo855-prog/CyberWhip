using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace CyberWhip
{
    internal static class SelfTest
    {
        private static int passed, failed;
        private static void Check(string name, Action test)
        {
            try { test(); passed++; Console.WriteLine("PASS  " + name); }
            catch (Exception ex) { failed++; Console.WriteLine("FAIL  " + name + ": " + ex.Message); }
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
        private static void Reject(Action action)
        {
            bool rejected = false;
            try { action(); } catch (ArgumentException) { rejected = true; }
            Require(rejected, "expected rejection");
        }
        [STAThread]
        private static int Main()
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            Native.EnableDpi();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Console.WriteLine("CyberWhip Offline v1.0.1 - built-in checks (no network requests)");
            Console.WriteLine("CLR: " + Environment.Version + " / " + (IntPtr.Size * 8) + " bit");
            Check("SendInput ABI layout", delegate
            {
                Require(Marshal.SizeOf(typeof(Native.INPUT)) == (IntPtr.Size == 8 ? 40 : 28), "INPUT size mismatch");
            });
            Check("OpenAI endpoint normalization", delegate
            {
                Require(IntranetEndpoint.Parse("http://10.1.2.3:8100").AbsolutePath == "/v1/chat/completions", "bare root");
                Require(IntranetEndpoint.Parse("http://10.1.2.3:11434/vl2b/v1/").AbsolutePath == "/vl2b/v1/chat/completions", "nginx prefix");
                Require(IntranetEndpoint.Parse("http://10.1.2.3:8100/v1/chat/completions").AbsolutePath == "/v1/chat/completions", "full path");
                Reject(delegate { IntranetEndpoint.Parse("file:///c:/anything"); });
                Reject(delegate { IntranetEndpoint.Parse("http://u:p@10.1.2.3/v1"); });
                Reject(delegate { IntranetEndpoint.Parse("http://10.1.2.3/v1?key=secret"); });
            });
            Check("Private IP allowlist boundaries", delegate
            {
                foreach (string ip in new string[] { "10.0.0.1", "172.16.0.1", "172.31.255.254", "192.168.1.1", "127.0.0.1", "::1", "fc00::1", "fdff::1", "::ffff:10.1.1.1" })
                    Require(IntranetEndpoint.IsPrivate(IPAddress.Parse(ip)), "private rejected: " + ip);
                foreach (string ip in new string[] { "8.8.8.8", "172.15.255.255", "172.32.0.1", "192.169.0.1", "0.0.0.0", "203.0.113.1", "2001:db8::1", "fe80::1", "::ffff:8.8.8.8" })
                    Require(!IntranetEndpoint.IsPrivate(IPAddress.Parse(ip)), "public accepted: " + ip);
                Reject(delegate { IntranetEndpoint.ValidateNetwork(new Uri("http://203.0.113.1/v1")); });
            });
            Check("Streaming Chinese delta and completion", delegate
            {
                JavaScriptSerializer json = new JavaScriptSerializer();
                StringBuilder output = new StringBuilder(), chunks = new StringBuilder();
                ApiJob.Emit("{\"choices\":[{\"delta\":{\"role\":\"assistant\",\"content\":\"\"}}]}", json, output, delegate(string s) { chunks.Append(s); });
                ApiJob.Emit("{\"choices\":[{\"delta\":{\"content\":\"内网\"}}]}", json, output, delegate(string s) { chunks.Append(s); });
                ApiJob.Emit("{\"choices\":[{\"delta\":{\"content\":\"连接正常\"}}]}", json, output, delegate(string s) { chunks.Append(s); });
                Require(ApiJob.Emit("[DONE]", json, output, delegate { }), "DONE marker");
                Require(output.ToString() == "内网连接正常" && chunks.ToString() == output.ToString(), "Chinese chunks mismatch");
            });
            Check("Non-streaming compatibility", delegate
            {
                StringBuilder output = new StringBuilder();
                ApiJob.Emit("{\"choices\":[{\"message\":{\"content\":\"OK\"}}]}", new JavaScriptSerializer(), output, delegate { });
                Require(output.ToString() == "OK", "message content missing");
            });
            Check("Configuration guards and defaults", delegate
            {
                Config c = new Config();
                c.Sensitivity = -1; c.CooldownMs = -1; c.RopeScale = Double.NaN;
                c.MaxTokens = 999999; c.Phrases = new string[] { null, "", "  加油  " };
                c.Normalize();
                Require(c.Sensitivity == 500 && c.CooldownMs == 800 && c.RopeScale == 1 && c.MaxTokens == 16384, "numeric guards");
                Require(c.Phrases.Length == 1 && c.Phrases[0] == "加油", "phrase guards");
                Require(!c.AutoEnter && !c.InterruptTerminal && !c.UseClipboard, "unexpected key automation default");
            });
            Check("API key is DPAPI protected in serialized settings", delegate
            {
                Config c = new Config();
                const string key = "local-test-key-0x5c";
                c.ApiKey = key;
                Require(c.ApiKey == key, "DPAPI round trip");
                string raw = new JavaScriptSerializer().Serialize(c);
                Require(!raw.Contains(key), "plaintext key stored");
                Config loaded = new JavaScriptSerializer().Deserialize<Config>(raw);
                Require(loaded.ApiKey == key, "serialized encrypted round trip");
            });
            Check("Whip handle stays pinned and physics stays finite", delegate
            {
                WhipPhysics rope = new WhipPhysics();
                Rectangle screen = new Rectangle(-1920, 0, 3840, 2160);
                rope.Spawn(400, 800, 1);
                for (int i = 0; i < 360; i++)
                {
                    double x = 400 + Math.Sin(i * 0.8) * 550, y = 800 + Math.Cos(i * 0.47) * 280;
                    rope.Step(x, y, screen);
                    Require(Math.Abs(rope.Points[0].X - x) < 0.000001 && Math.Abs(rope.Points[0].Y - y) < 0.000001, "handle drift");
                    foreach (RopePoint p in rope.Points)
                        Require(!Double.IsNaN(p.X) && !Double.IsInfinity(p.X) && !Double.IsNaN(p.Y) && !Double.IsInfinity(p.Y), "non-finite point");
                }
                rope.Drop();
                for (int i = 0; i < 500 && !rope.OffScreen(screen); i++) rope.Step(0, 0, screen);
                Require(rope.OffScreen(screen), "drop did not leave screen");
            });
            Check("Overlay restores native topmost order without taking focus", delegate
            {
                using (Form target = new Form())
                using (WhipOverlay glass = new WhipOverlay())
                {
                    target.Text = "CyberWhip CHECK - temporary window";
                    target.ShowInTaskbar = false;
                    target.StartPosition = FormStartPosition.Manual;
                    Rectangle work = Screen.PrimaryScreen.WorkingArea;
                    target.Bounds = new Rectangle(work.Left + 40, work.Top + 40, 280, 160);
                    target.Show();
                    Application.DoEvents();
                    WhipPhysics rope = new WhipPhysics();
                    rope.Spawn(target.Left + 60, target.Top + 90, 1);
                    for (int pass = 0; pass < 2; pass++)
                    {
                        // Put another window at the top, then redraw the whip.
                        // The second pass also checks an already-visible overlay.
                        Require(Native.SetWindowPos(target.Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE | Native.SWP_NOOWNERZORDER), "target placement failed");
                        Native.SetForegroundWindow(target.Handle);
                        Application.DoEvents();
                        IntPtr foreground = Native.GetForegroundWindow();
                        glass.Render(rope, "置顶与焦点自检", 10, SystemInformation.VirtualScreen);
                        Application.DoEvents();
                        Require((Native.GetWindowLong(glass.Handle, -20) & Native.WS_EX_TOPMOST) != 0, "native TOPMOST style missing");
                        bool above = false;
                        IntPtr previous = Native.GetWindow(target.Handle, 3);
                        for (int i = 0; i < 512 && previous != IntPtr.Zero; i++)
                        {
                            if (previous == glass.Handle) { above = true; break; }
                            previous = Native.GetWindow(previous, 3);
                        }
                        Require(above, "whip is below the target window");
                        Require(Native.GetForegroundWindow() == foreground, "overlay stole keyboard focus");
                    }
                    glass.Hide();
                    target.Close();
                }
            });
            Console.WriteLine("RESULT: " + passed + " passed, " + failed + " failed");
            Console.WriteLine("These checks do not replace desktop, sound, browser and model integration testing.");
            return failed == 0 ? 0 : 1;
        }
    }
}
