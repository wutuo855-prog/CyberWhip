using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace CyberWhip
{
    internal sealed class ChatMessage
    {
        internal string Role, Content;
        internal ChatMessage(string role, string content) { Role = role; Content = content; }
    }
    internal sealed class ApiResult
    {
        internal string Content = "", Error = "";
        internal bool Cancelled;
    }
    internal static class IntranetEndpoint
    {
        internal static Uri Parse(string input)
        {
            Uri uri;
            if (!Uri.TryCreate((input ?? "").Trim(), UriKind.Absolute, out uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
                throw new ArgumentException("填写 http:// 或 https:// 开头的内网 API 地址。");
            if (!String.IsNullOrEmpty(uri.UserInfo) || !String.IsNullOrEmpty(uri.Query) || !String.IsNullOrEmpty(uri.Fragment))
                throw new ArgumentException("API 地址不要带账号、查询参数或 # 片段；密钥请填在单独的输入框。");
            string path = uri.AbsolutePath.TrimEnd('/');
            if (!path.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                if (!path.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) path += "/v1";
                path += "/chat/completions";
            }
            UriBuilder builder = new UriBuilder(uri);
            builder.Path = path;
            return builder.Uri;
        }
        internal static bool IsPrivate(IPAddress address)
        {
            if (IPAddress.IsLoopback(address)) return true;
            if (address.IsIPv4MappedToIPv6) return IsPrivate(address.MapToIPv4());
            byte[] bytes = address.GetAddressBytes();
            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                return bytes[0] == 10 || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) || (bytes[0] == 192 && bytes[1] == 168);
            }
            return address.AddressFamily == AddressFamily.InterNetworkV6 && (bytes[0] & 0xFE) == 0xFC;
        }
        internal static void ValidateNetwork(Uri uri)
        {
            IPAddress address;
            IPAddress[] addresses = IPAddress.TryParse(uri.DnsSafeHost.Trim('[', ']'), out address) ? new IPAddress[] { address } : Dns.GetHostAddresses(uri.DnsSafeHost);
            if (addresses.Length == 0) throw new ArgumentException("内网主机名没有解析到地址。");
            foreach (IPAddress ip in addresses)
                if (!IsPrivate(ip)) throw new ArgumentException("纯内网模式拒绝公网地址。请填写 10.x、172.16–31.x、192.168.x、localhost，或解析到内网的主机名。");
        }
    }
    internal sealed class ApiJob
    {
        private readonly CancellationTokenSource cancel = new CancellationTokenSource();
        internal void Cancel()
        {
            try { cancel.Cancel(); }
            catch (ObjectDisposedException) { }
        }
        internal static ApiJob Start(Config config, List<ChatMessage> history, Action<string> onChunk, Action<ApiResult> onDone)
        {
            ApiJob job = new ApiJob();
            string endpoint = config.Endpoint, model = config.Model.Trim(), key = config.ApiKey, system = config.SystemPrompt;
            int maxTokens = config.MaxTokens;
            List<ChatMessage> snapshot = new List<ChatMessage>();
            foreach (ChatMessage m in history) snapshot.Add(new ChatMessage(m.Role, m.Content));
            Task.Factory.StartNew(delegate
            {
                ApiResult result = new ApiResult();
                StringBuilder output = new StringBuilder();
                try
                {
                    CancellationToken token = job.cancel.Token;
                    token.ThrowIfCancellationRequested();
                    Uri uri = IntranetEndpoint.Parse(endpoint);
                    IntranetEndpoint.ValidateNetwork(uri);
                    token.ThrowIfCancellationRequested();
                    if (String.IsNullOrWhiteSpace(model)) throw new ArgumentException("先在设置中填写模型名（served-model-name）。");
                    if (key.IndexOf('\r') >= 0 || key.IndexOf('\n') >= 0) throw new ArgumentException("API 密钥不能包含换行。");
                    JavaScriptSerializer json = new JavaScriptSerializer();
                    json.MaxJsonLength = 4 * 1024 * 1024;
                    List<Dictionary<string, object>> messages = new List<Dictionary<string, object>>();
                    if (!String.IsNullOrWhiteSpace(system)) messages.Add(Message("system", system));
                    int first = Math.Max(0, snapshot.Count - 24);
                    if (first > 0 && snapshot[0].Role == "user") messages.Add(Message("user", snapshot[0].Content));
                    for (int i = first; i < snapshot.Count; i++) messages.Add(Message(snapshot[i].Role, snapshot[i].Content));
                    Dictionary<string, object> payload = new Dictionary<string, object>();
                    payload["model"] = model;
                    payload["messages"] = messages;
                    payload["stream"] = true;
                    payload["max_tokens"] = maxTokens;
                    byte[] body = Encoding.UTF8.GetBytes(json.Serialize(payload));
                    // TLS 1.2 is available on the Windows versions targeted by this package.
                    ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
                    request.Method = "POST";
                    request.Proxy = null;
                    request.AllowAutoRedirect = false;
                    request.KeepAlive = false;
                    request.Timeout = 60000;
                    request.ReadWriteTimeout = 60000;
                    request.ContentType = "application/json; charset=utf-8";
                    request.Accept = "text/event-stream, application/json";
                    request.ServicePoint.Expect100Continue = false;
                    request.ContentLength = body.Length;
                    if (!String.IsNullOrEmpty(key)) request.Headers[HttpRequestHeader.Authorization] = "Bearer " + key;
                    using (CancellationTokenRegistration registration = token.Register(delegate { try { request.Abort(); } catch { } }))
                    {
                        using (Stream requestStream = request.GetRequestStream()) requestStream.Write(body, 0, body.Length);
                        using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                        {
                            int status = (int)response.StatusCode;
                            if (status >= 300 && status < 400) throw new InvalidOperationException("内网 API 返回跳转（" + status + "）；本程序不跟随跳转，请填写最终地址。");
                            using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                            {
                                if ((response.ContentType ?? "").IndexOf("text/event-stream", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    StringBuilder eventData = new StringBuilder();
                                    string line;
                                    bool finished = false;
                                    while (!finished && (line = reader.ReadLine()) != null)
                                    {
                                        token.ThrowIfCancellationRequested();
                                        if (line.Length == 0)
                                        {
                                            if (eventData.Length > 0)
                                            {
                                                finished = Emit(eventData.ToString(), json, output, onChunk);
                                                eventData.Length = 0;
                                            }
                                        }
                                        else if (line.StartsWith("data:", StringComparison.Ordinal))
                                        {
                                            if (eventData.Length > 0) eventData.Append('\n');
                                            eventData.Append(line.Substring(5).TrimStart());
                                            if (eventData.Length > 1024 * 1024) throw new InvalidDataException("API 单条事件过大。");
                                        }
                                    }
                                    if (!finished && eventData.Length > 0) Emit(eventData.ToString(), json, output, onChunk);
                                }
                                else
                                {
                                    StringBuilder raw = new StringBuilder();
                                    char[] buffer = new char[4096];
                                    int count;
                                    while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
                                    {
                                        token.ThrowIfCancellationRequested();
                                        raw.Append(buffer, 0, count);
                                        if (raw.Length > 4 * 1024 * 1024) throw new InvalidDataException("API 响应超过大小限制。");
                                    }
                                    Emit(raw.ToString(), json, output, onChunk);
                                }
                            }
                        }
                    }
                    token.ThrowIfCancellationRequested();
                    if (output.Length == 0) result.Error = "接口已返回，但没有可显示的文字。请检查是否为 OpenAI 兼容 chat/completions 接口。";
                }
                catch (OperationCanceledException) { result.Cancelled = true; }
                catch (WebException ex)
                {
                    if (job.cancel.IsCancellationRequested) result.Cancelled = true;
                    else if (ex.Response != null)
                    {
                        using (HttpWebResponse response = ex.Response as HttpWebResponse)
                        {
                            int status = response == null ? 0 : (int)response.StatusCode;
                            result.Error = "API 返回 HTTP " + status + ". ";
                            if (status == 401 || status == 403) result.Error += "请检查 API 密钥和访问权限。";
                            else if (status == 404) result.Error += "请检查 v1 路径和 Nginx 路由。";
                            else if (status == 400) result.Error += "请检查模型名、上下文长度与最大输出长度。";
                            else result.Error += ex.Message;
                        }
                    }
                    else result.Error = "无法连接内网 API：" + ex.Message;
                }
                catch (Exception ex)
                {
                    if (job.cancel.IsCancellationRequested) result.Cancelled = true;
                    else result.Error = ex.Message;
                }
                finally
                {
                    result.Content = output.ToString();
                    try { onDone(result); }
                    finally { job.cancel.Dispose(); }
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            return job;
        }
        private static Dictionary<string, object> Message(string role, string content)
        {
            return new Dictionary<string, object> { { "role", role }, { "content", content } };
        }
        internal static bool Emit(string data, JavaScriptSerializer json, StringBuilder output, Action<string> onChunk)
        {
            if (data.Trim() == "[DONE]") return true;
            Dictionary<string, object> value = json.DeserializeObject(data) as Dictionary<string, object>;
            if (value == null) throw new InvalidDataException("API 返回的事件不是 JSON 对象。");
            if (value.ContainsKey("error")) throw new InvalidDataException("模型服务返回错误事件，请检查服务日志与参数。");
            object choicesObject;
            if (!value.TryGetValue("choices", out choicesObject)) return false;
            System.Collections.IList choices = choicesObject as System.Collections.IList;
            if (choices == null || choices.Count == 0) return false;
            Dictionary<string, object> choice = choices[0] as Dictionary<string, object>;
            if (choice == null) return false;
            object item;
            Dictionary<string, object> message = null;
            if (choice.TryGetValue("delta", out item)) message = item as Dictionary<string, object>;
            if (message == null && choice.TryGetValue("message", out item)) message = item as Dictionary<string, object>;
            if (message != null && message.TryGetValue("content", out item) && item is string)
            {
                string content = (string)item;
                if (output.Length + content.Length > 250000) throw new InvalidDataException("回答过长，已停止显示。");
                output.Append(content);
                if (content.Length > 0) onChunk(content);
            }
            return false;
        }
    }
}
