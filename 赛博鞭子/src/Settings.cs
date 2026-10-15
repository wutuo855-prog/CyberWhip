using System;
using System.Collections.Generic;
using System.Drawing;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace CyberWhip
{
    internal sealed class SettingsForm : Form
    {
        private readonly WhipContext owner;
        private readonly ComboBox mode = new ComboBox(), inputMethod = new ComboBox();
        private readonly NumericUpDown cooldown = new NumericUpDown(), scale = new NumericUpDown(), maxTokens = new NumericUpDown();
        private readonly CheckBox sound = new CheckBox(), enter = new CheckBox(), interrupt = new CheckBox(), restart = new CheckBox();
        private readonly TextBox phrases = new TextBox(), endpoint = new TextBox(), model = new TextBox(), apiKey = new TextBox(), system = new TextBox();
        private readonly Label status = new Label(), bound = new Label();
        private readonly TabControl tabs = new TabControl();
        internal SettingsForm(WhipContext context)
        {
            owner = context;
            Text = "赛博鞭子 · 内网便携版 v1.0.1";
            Font = new Font("Microsoft YaHei UI", 10);
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            Size = new Size(780, 650);
            MinimumSize = new Size(700, 590);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Padding = new Padding(14);
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1; root.RowCount = 4;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            Controls.Add(root);
            Label heading = new Label();
            heading.Text = "赛博鞭子  /  CYBER WHIP\r\n左键抽鞭 · 右键收起 · 桌面置顶 · 中文催促";
            heading.Dock = DockStyle.Fill;
            heading.Font = new Font(Font.FontFamily, 11, FontStyle.Bold);
            root.Controls.Add(heading, 0, 0);
            tabs.Dock = DockStyle.Fill;
            root.Controls.Add(tabs, 0, 1);
            BuildGeneral(); BuildPhrases(); BuildApi();
            status.Dock = DockStyle.Fill; status.Padding = new Padding(4, 5, 4, 0); status.AutoEllipsis = true;
            root.Controls.Add(status, 0, 2);
            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill; buttons.FlowDirection = FlowDirection.RightToLeft;
            root.Controls.Add(buttons, 0, 3);
            Button close = CreateButton("关闭", delegate { Hide(); });
            Button save = CreateButton("保存并关闭", delegate { if (TrySave()) Hide(); });
            Button summon = CreateButton("保存并召唤", delegate { if (TrySave()) { Hide(); owner.Toggle(); } });
            buttons.Controls.Add(close); buttons.Controls.Add(save); buttons.Controls.Add(summon);
            LoadValues();
            mode.SelectedIndexChanged += delegate { UpdateMode(); };
            UpdateMode(); RefreshStatus();
        }
        private static Button CreateButton(string title, EventHandler handler)
        {
            Button button = new Button();
            button.Text = title; button.AutoSize = true; button.Height = 32; button.Padding = new Padding(10, 2, 10, 2);
            button.Click += handler;
            return button;
        }
        private TableLayoutPanel Table(string title)
        {
            TabPage page = new TabPage(title);
            page.Padding = new Padding(12); page.AutoScroll = true;
            tabs.TabPages.Add(page);
            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.Top; table.AutoSize = true;
            table.ColumnCount = 2;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 202));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            page.Controls.Add(table);
            return table;
        }
        private static void Row(TableLayoutPanel table, string label, Control control, int height)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            Label text = new Label(); text.Text = label; text.Dock = DockStyle.Fill;
            text.TextAlign = ContentAlignment.MiddleLeft;
            table.Controls.Add(text, 0, row);
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(3, 4, 3, 4);
            table.Controls.Add(control, 1, row);
        }
        private static void Wide(TableLayoutPanel table, Control control, int height)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            control.Dock = DockStyle.Fill;
            table.Controls.Add(control, 0, row); table.SetColumnSpan(control, 2);
        }
        private static Label Note(string text)
        {
            Label label = new Label(); label.Text = text; label.AutoEllipsis = true; label.Padding = new Padding(0, 4, 0, 4);
            return label;
        }
        private static void Number(NumericUpDown control, decimal min, decimal max, decimal step, int decimals)
        {
            control.Minimum = min; control.Maximum = max; control.Increment = step; control.DecimalPlaces = decimals;
        }
        private void BuildGeneral()
        {
            TableLayoutPanel table = Table("鞭子与发送");
            mode.DropDownStyle = ComboBoxStyle.DropDownList;
            mode.Items.AddRange(WhipContext.Modes);
            Row(table, "工作模式", mode, 38);
            Wide(table, Note("移动鼠标只控制鞭子动画；左键点击响鞭并催促，右键立即收起。"), 46);
            Number(cooldown, 0.8m, 30, 0.5m, 1);
            Row(table, "模型催促冷却（秒）", cooldown, 38);
            Number(scale, 70, 220, 10, 0);
            Row(table, "鞭子大小（%）", scale, 38);
            sound.Text = "播放随机鞭子音效";
            Wide(table, sound, 31);
            enter.Text = "文字填入后自动按 Enter（关闭时由你手动发送）";
            Wide(table, enter, 31);
            interrupt.Text = "终端模式：先发送 Ctrl+C，再填入催促语";
            Wide(table, interrupt, 31);
            inputMethod.DropDownStyle = ComboBoxStyle.DropDownList;
            inputMethod.Items.AddRange(new string[] { "Unicode 按键（不占用剪贴板）", "剪贴板粘贴（适合部分终端）" });
            Row(table, "文字输入方式", inputMethod, 38);
            bound.AutoEllipsis = true;
            Wide(table, bound, 42);
            Wide(table, CreateButton("3 秒后绑定模型窗口", delegate { if (TrySave()) owner.BindAfterDelay(); }), 38);
            Wide(table, Note("先点进模型输入框，Alt+Shift+B 绑定；Alt+Shift+W 召唤。鞭子显示时点击不会传给下方网页。"), 48);
        }
        private void BuildPhrases()
        {
            TableLayoutPanel table = Table("催促语");
            Wide(table, Note("每行一条，抽鞭时随机选择。网页 / 终端每条最多 500 个字符；不会自动清空原有输入。"), 48);
            phrases.Multiline = true; phrases.ScrollBars = ScrollBars.Vertical; phrases.AcceptsReturn = true;
            Wide(table, phrases, 282);
            Wide(table, Note("甩鞭是发送指令的交互方式。模型实际生成速度取决于推理服务与 GPU。"), 48);
        }
        private void BuildApi()
        {
            TableLayoutPanel table = Table("内网 API");
            Wide(table, Note("支持 vLLM / Ollama 的 OpenAI 兼容接口。Dify 普通聊天网页请选“网页聊天”。"), 46);
            Row(table, "API 地址", endpoint, 38);
            Row(table, "模型名", model, 38);
            apiKey.UseSystemPasswordChar = true;
            Row(table, "API 密钥（可留空）", apiKey, 38);
            Number(maxTokens, 64, 16384, 256, 0);
            Row(table, "最大输出 token", maxTokens, 38);
            system.Multiline = true; system.ScrollBars = ScrollBars.Vertical;
            Row(table, "系统提示词", system, 84);
            restart.Text = "API 回答中抽鞭：取消当前请求，追加催促语后重新生成";
            Wide(table, restart, 38);
            Wide(table, CreateButton("保存并测试连接", delegate
            {
                mode.SelectedIndex = 3;
                if (TrySave()) owner.ShowChat().SendPrompt("只回复“内网连接正常”，不要输出其他文字。");
            }), 38);
            Wide(table, Note("例：http://10.0.0.10:8100/v1 或 http://10.0.0.10:11434/vl2b/v1\r\n只接受内网 / localhost；不使用代理，不跟随跳转，不跳过 HTTPS 证书校验。"), 67);
        }
        private void LoadValues()
        {
            Config c = owner.Config;
            mode.SelectedIndex = c.Mode;
            cooldown.Value = c.CooldownMs / 1000m; scale.Value = (decimal)c.RopeScale * 100;
            sound.Checked = c.Sound; enter.Checked = c.AutoEnter; interrupt.Checked = c.InterruptTerminal;
            inputMethod.SelectedIndex = c.UseClipboard ? 1 : 0;
            phrases.Lines = c.Phrases;
            endpoint.Text = c.Endpoint; model.Text = c.Model; apiKey.Text = c.ApiKey;
            maxTokens.Value = c.MaxTokens; system.Text = c.SystemPrompt; restart.Checked = c.RestartOnWhip;
        }
        private void UpdateMode()
        {
            bool keys = mode.SelectedIndex == 1 || mode.SelectedIndex == 2;
            enter.Enabled = inputMethod.Enabled = keys;
            interrupt.Enabled = mode.SelectedIndex == 2;
        }
        private bool TrySave()
        {
            try
            {
                if (!String.IsNullOrWhiteSpace(endpoint.Text)) IntranetEndpoint.Parse(endpoint.Text);
                if (mode.SelectedIndex == 3 && (String.IsNullOrWhiteSpace(endpoint.Text) || String.IsNullOrWhiteSpace(model.Text)))
                {
                    tabs.SelectedIndex = 2;
                    throw new ArgumentException("内网 API 模式需要填写 API 地址和模型名。网页模式无需填写。 ");
                }
                List<string> lines = new List<string>();
                foreach (string raw in phrases.Lines)
                {
                    string line = raw.Trim();
                    if (line.Length == 0) continue;
                    if (line.Length > 500) throw new ArgumentException("每条催促语请控制在 500 个字符以内。");
                    lines.Add(line);
                }
                if (lines.Count == 0) throw new ArgumentException("至少填写一条催促语。");
                Config c = new Config();
                c.Mode = mode.SelectedIndex;
                c.Sound = sound.Checked; c.AutoEnter = enter.Checked; c.InterruptTerminal = interrupt.Checked;
                c.UseClipboard = inputMethod.SelectedIndex == 1;
                c.Sensitivity = owner.Config.Sensitivity; c.CooldownMs = (int)(cooldown.Value * 1000);
                c.RopeScale = (double)scale.Value / 100;
                c.Phrases = lines.ToArray();
                c.Endpoint = endpoint.Text.Trim(); c.Model = model.Text.Trim(); c.ApiKey = apiKey.Text.Trim();
                c.MaxTokens = (int)maxTokens.Value; c.SystemPrompt = system.Text;
                c.RestartOnWhip = restart.Checked;
                return owner.Apply(c);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "请检查设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
        }
        internal void RefreshStatus()
        {
            status.Text = owner.Status;
            bound.Text = owner.Target.IsValid() ? "绑定窗口：" + owner.Target.Title : "绑定窗口：尚未绑定 / 原窗口已关闭";
        }
    }
}
