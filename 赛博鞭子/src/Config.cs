using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace CyberWhip
{
    public sealed class Config
    {
        public int Mode = 1;
        public bool Sound = true;
        public bool AutoEnter = false;
        public bool InterruptTerminal = false;
        public bool UseClipboard = false;
        public bool RestartOnWhip = true;
        public int Sensitivity = 1900;
        public int CooldownMs = 2500;
        public double RopeScale = 1;
        public int TotalCracks = 0;
        public string Endpoint = "";
        public string Model = "";
        public string ApiKeyProtected = "";
        public int MaxTokens = 2048;
        public string SystemPrompt = "你是公司内网技术助手。请使用中文，给出准确、清晰、可执行的回答。";
        public string[] Phrases = new string[]
        {
            "加油，请继续完成当前任务。",
            "快马加鞭，直接给出可执行的结果。",
            "请继续推进，把剩余步骤完成。",
            "请聚焦当前问题，给出关键结论。",
            "检查一下卡住的位置，继续完成工作。"
        };
        [ScriptIgnore] public string ApiKey
        {
            get
            {
                if (String.IsNullOrEmpty(ApiKeyProtected)) return "";
                try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(ApiKeyProtected), null, DataProtectionScope.CurrentUser)); }
                catch { return ""; }
            }
            set
            {
                ApiKeyProtected = String.IsNullOrEmpty(value) ? "" : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser));
            }
        }
        internal void Normalize()
        {
            Mode = Math.Max(0, Math.Min(3, Mode));
            Sensitivity = Math.Max(500, Math.Min(9000, Sensitivity));
            CooldownMs = Math.Max(800, Math.Min(30000, CooldownMs));
            if (Double.IsNaN(RopeScale) || Double.IsInfinity(RopeScale)) RopeScale = 1;
            RopeScale = Math.Max(0.7, Math.Min(2.2, RopeScale));
            MaxTokens = Math.Max(64, Math.Min(16384, MaxTokens));
            Endpoint = Endpoint ?? ""; Model = Model ?? ""; SystemPrompt = SystemPrompt ?? "";
            List<string> valid = new List<string>();
            if (Phrases != null)
                foreach (string phrase in Phrases)
                {
                    if (String.IsNullOrWhiteSpace(phrase)) continue;
                    string text = phrase.Trim().Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
                    valid.Add(text.Length > 500 ? text.Substring(0, 500) : text);
                    if (valid.Count >= 200) break;
                }
            Phrases = valid.Count == 0 ? new string[] { "加油，请继续完成当前任务。" } : valid.ToArray();
            TotalCracks = Math.Max(0, TotalCracks);
        }
    }
    internal static class ConfigStore
    {
        internal static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CyberWhip");
        internal static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");
        internal static bool FirstRun;
        internal static string LoadWarning = "";
        internal static Config Load()
        {
            FirstRun = !File.Exists(FilePath);
            if (!FirstRun)
            {
                try
                {
                    Config value = new JavaScriptSerializer().Deserialize<Config>(File.ReadAllText(FilePath, Encoding.UTF8));
                    if (value == null) throw new InvalidDataException("设置内容为空。");
                    value.Normalize();
                    return value;
                }
                catch (Exception ex) { LoadWarning = "设置读取失败，已暂用默认值：" + ex.Message; }
            }
            return new Config();
        }
        internal static void Save(Config value)
        {
            value.Normalize();
            Directory.CreateDirectory(DirectoryPath);
            string temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(value), new UTF8Encoding(false));
            if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null);
            else File.Move(temporary, FilePath);
        }
        internal static void Log(Exception error)
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                string path = Path.Combine(DirectoryPath, "error.log");
                if (File.Exists(path) && new FileInfo(path).Length > 256 * 1024) File.Delete(path);
                File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + error.GetType().Name + ": " + error.Message + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }
    }
}
