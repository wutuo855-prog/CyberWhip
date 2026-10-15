using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace CyberWhip
{
    internal sealed class ChatForm : Form
    {
        private readonly WhipContext owner;
        private readonly RichTextBox transcript = new RichTextBox();
        private readonly TextBox input = new TextBox();
        private readonly Label status = new Label();
        private readonly Button send = new Button(), stop = new Button();
        private readonly List<ChatMessage> history = new List<ChatMessage>();
        private readonly StringBuilder partial = new StringBuilder();
        private ApiJob job;
        private int generation;
        private bool busy, shuttingDown;
        internal ChatForm(WhipContext context)
        {
            owner = context;
            Text = "赛博鞭子 · 内网模型聊天";
            Font = new Font("Microsoft YaHei UI", 10);
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            Size = new Size(850, 700); MinimumSize = new Size(640, 530);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Padding = new Padding(12);
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill; root.ColumnCount = 1; root.RowCount = 5;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            Controls.Add(root);
            Label title = new Label();
            title.Dock = DockStyle.Fill;
            title.Text = "直接连接你的内网模型  ·  Enter 发送 / Shift+Enter 换行\r\nAlt+Shift+W 召唤鞭子；聊天记录仅保留在本次运行中。";
            root.Controls.Add(title, 0, 0);
            transcript.ReadOnly = true; transcript.Dock = DockStyle.Fill; transcript.BackColor = Color.White;
            transcript.DetectUrls = false; transcript.BorderStyle = BorderStyle.FixedSingle;
            root.Controls.Add(transcript, 0, 1);
            status.Dock = DockStyle.Fill; status.AutoEllipsis = true; status.TextAlign = ContentAlignment.MiddleLeft;
            root.Controls.Add(status, 0, 2);
            input.Multiline = true; input.ScrollBars = ScrollBars.Vertical; input.Dock = DockStyle.Fill; input.AcceptsReturn = true;
            root.Controls.Add(input, 0, 3);
            input.KeyDown += delegate(object sender, KeyEventArgs args)
            {
                if (args.KeyCode == Keys.Enter && !args.Shift)
                {
                    args.SuppressKeyPress = true; args.Handled = true;
                    if (!busy) SendInputPrompt();
                }
            };
            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill; buttons.FlowDirection = FlowDirection.RightToLeft;
            root.Controls.Add(buttons, 0, 4);
            AddButton(buttons, send, "发送", delegate { SendInputPrompt(); });
            AddButton(buttons, stop, "停止回答", delegate { CancelCurrent(true); owner.SetStatus("已停止当前客户端请求。"); });
            AddButton(buttons, new Button(), "召唤 / 收鞭", delegate
            {
                if (owner.Config.Mode != 3)
                {
                    owner.SetStatus("先在设置中选择“内网 API 聊天”模式。");
                    owner.ShowSettings();
                    return;
                }
                owner.Toggle();
            });
            AddButton(buttons, new Button(), "清空对话", delegate
            {
                CancelCurrent(false); history.Clear(); transcript.Clear();
                owner.SetStatus("当前对话已清空。");
            });
            AddButton(buttons, new Button(), "复制回答", delegate
            {
                for (int i = history.Count - 1; i >= 0; i--)
                    if (history[i].Role == "assistant")
                    {
                        try { Clipboard.SetText(history[i].Content); owner.SetStatus("回答已复制。"); }
                        catch (Exception ex) { owner.SetStatus("复制失败：" + ex.Message); }
                        return;
                    }
            });
            AddButton(buttons, new Button(), "设置", delegate { owner.ShowSettings(); });
            FormClosing += delegate(object sender, FormClosingEventArgs args)
            {
                if (!shuttingDown) { args.Cancel = true; Hide(); }
            };
            stop.Enabled = false;
            RefreshStatus();
            Shown += delegate { input.Focus(); };
        }
        private static void AddButton(FlowLayoutPanel panel, Button button, string text, EventHandler action)
        {
            button.Text = text; button.AutoSize = true; button.Height = 32; button.Padding = new Padding(7, 2, 7, 2);
            button.Click += action; panel.Controls.Add(button);
        }
        private void SendInputPrompt()
        {
            if (SendPrompt(input.Text)) { input.Clear(); input.Focus(); }
        }
        internal bool SendPrompt(string prompt)
        {
            if (busy) { owner.SetStatus("正在回答；请先停止，或抽鞭触发中断重发。"); return false; }
            prompt = (prompt ?? "").Trim();
            if (prompt.Length == 0) { owner.SetStatus("先在聊天框输入一个问题。"); return false; }
            if (String.IsNullOrWhiteSpace(owner.Config.Endpoint) || String.IsNullOrWhiteSpace(owner.Config.Model))
            { owner.SetStatus("先在设置的“内网 API”页填写地址和模型名。"); return false; }
            if (prompt.Length > 60000) { owner.SetStatus("输入过长，请缩短后再发送。"); return false; }
            history.Add(new ChatMessage("user", prompt));
            Append("\r\n你\r\n" + prompt + "\r\n\r\n模型\r\n", Color.FromArgb(40, 66, 102));
            StartRequest();
            return true;
        }
        internal bool Nudge(string phrase)
        {
            if (history.Count == 0)
            {
                owner.SetStatus("先在内网聊天框提出问题，再抽鞭催促。"); input.Focus(); return false;
            }
            if (busy && !owner.Config.RestartOnWhip)
            {
                owner.SetStatus("模型正在回答，本次只播放音效；可在 API 设置中开启中断重发。"); return false;
            }
            if (busy) CancelCurrent(true);
            if (history[history.Count - 1].Role == "user")
            {
                history[history.Count - 1].Content += "\n\n补充指令：" + phrase;
                Append("\r\n鞭子催促\r\n" + phrase + "\r\n\r\n模型\r\n", Color.FromArgb(40, 66, 102));
                StartRequest();
                return true;
            }
            return SendPrompt(phrase);
        }
        private void StartRequest()
        {
            partial.Length = 0;
            busy = true; send.Enabled = false; stop.Enabled = true;
            int current = ++generation;
            owner.SetStatus("正在等待内网模型回答…");
            job = ApiJob.Start(owner.Config, history, delegate(string chunk)
            {
                owner.Ui(delegate
                {
                    if (shuttingDown || current != generation) return;
                    partial.Append(chunk); Append(chunk, Color.FromArgb(28, 31, 38));
                });
            }, delegate(ApiResult result)
            {
                owner.Ui(delegate
                {
                    if (shuttingDown || current != generation) return;
                    busy = false; send.Enabled = true; stop.Enabled = false; job = null;
                    if (result.Content.Length > 0) history.Add(new ChatMessage("assistant", result.Content));
                    partial.Length = 0;
                    if (result.Cancelled) owner.SetStatus("当前客户端请求已停止。");
                    else if (result.Error.Length > 0)
                    {
                        Append("\r\n[" + result.Error + "]\r\n", Color.FromArgb(165, 58, 58));
                        owner.SetStatus(result.Error);
                    }
                    else owner.SetStatus("回答完成 · 可以继续提问或抽鞭。");
                    RefreshStatus();
                });
            });
        }
        private void CancelCurrent(bool keepPartial)
        {
            generation++;
            if (job != null) job.Cancel();
            job = null;
            if (busy)
            {
                if (keepPartial && partial.Length > 0) history.Add(new ChatMessage("assistant", partial.ToString()));
                Append("\r\n[当前客户端请求已停止]\r\n", Color.FromArgb(125, 125, 125));
            }
            partial.Length = 0; busy = false;
            send.Enabled = true; stop.Enabled = false;
        }
        private void Append(string text, Color color)
        {
            transcript.SelectionStart = transcript.TextLength;
            transcript.SelectionLength = 0; transcript.SelectionColor = color;
            transcript.AppendText(text); transcript.SelectionColor = transcript.ForeColor;
            transcript.ScrollToCaret();
        }
        internal void RefreshStatus()
        {
            status.Text = (busy ? "生成中 · " : "") + owner.Status;
        }
        internal void Shutdown() { shuttingDown = true; CancelCurrent(false); }
    }
}
