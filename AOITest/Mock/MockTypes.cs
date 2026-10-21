using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AOITest.Mock
{
    /// <summary>
    /// 一次被插件打进来的 HTTP 请求（录制用）。
    /// </summary>
    public class MockRequest
    {
        public DateTime Time { get; set; }
        public string Method { get; set; }

        /// <summary>不含查询串的路径，例如 /LoginCheck</summary>
        public string Path { get; set; }

        /// <summary>查询串（不含 ?），没有则为空串</summary>
        public string Query { get; set; }

        /// <summary>原始请求体文本（UTF-8）</summary>
        public string RawBody { get; set; }

        public string RemoteEndPoint { get; set; }

        /// <summary>实际回给插件的应答</summary>
        public MockResponse Responded { get; set; }

        /// <summary>处理过程中的异常信息，正常为 null</summary>
        public string Error { get; set; }

        /// <summary>把请求体当 JSON 解析；解析不了返回 null（不会抛）。</summary>
        public JToken TryParseJson()
        {
            if (string.IsNullOrWhiteSpace(RawBody)) return null;
            try { return JToken.Parse(RawBody); }
            catch { return null; }
        }

        public override string ToString()
        {
            return string.Format("{0:HH:mm:ss} {1} {2} {3}", Time, Method, Path,
                string.IsNullOrEmpty(Error) ? "" : ("[ERR] " + Error));
        }
    }

    /// <summary>
    /// 一条路由的应答定义。既可写在 json 场景文件里，也可在代码里直接 new。
    /// </summary>
    public class MockResponse
    {
        public MockResponse()
        {
            StatusCode = 200;
            ContentType = "application/json; charset=utf-8";
            Body = "";
            DelayMs = 0;
        }

        /// <summary>HTTP 状态码，默认 200。想做"接口 500"分支就填 500。</summary>
        public int StatusCode { get; set; }

        public string ContentType { get; set; }

        /// <summary>应答正文。空串 = 模拟"服务器返回空值"分支。</summary>
        public string Body { get; set; }

        /// <summary>延迟多少毫秒再回。要触发插件超时逻辑就设成大于 XML 里的 Timeout(秒)*1000。</summary>
        public int DelayMs { get; set; }

        /// <summary>这条应答来自哪个场景文件，仅用于界面显示。</summary>
        public string FromScenario { get; set; }

        /// <summary>备注，界面上能看到，方便说明这条在测什么。</summary>
        public string Note { get; set; }

        /// <summary>从 json 节点解析一条应答定义。</summary>
        public static MockResponse FromJson(JToken token)
        {
            var resp = new MockResponse();
            if (token == null) return resp;

            JObject obj = token as JObject;
            if (obj == null)
            {
                // 直接写了个字符串，当作 raw body
                resp.Body = token.Type == JTokenType.String ? token.Value<string>() : token.ToString();
                return resp;
            }

            resp.StatusCode = obj.Value<int?>("status") ?? obj.Value<int?>("StatusCode") ?? 200;
            resp.DelayMs = obj.Value<int?>("delayMs") ?? obj.Value<int?>("DelayMs") ?? 0;

            string ct = obj.Value<string>("contentType");
            if (!string.IsNullOrEmpty(ct)) resp.ContentType = ct;

            resp.FromScenario = obj.Value<string>("from");
            resp.Note = obj.Value<string>("note");

            JToken body = obj["body"];
            if (body == null) body = obj["Body"];

            if (body == null)
            {
                resp.Body = "";
            }
            else if (body.Type == JTokenType.String)
            {
                resp.Body = body.Value<string>();
            }
            else
            {
                // 对象/数组 → 压成一行 json，和 HttpWebRequest 真实收到的样子一致
                resp.Body = body.ToString(Formatting.None);
            }

            return resp;
        }
    }

    /// <summary>
    /// 一个场景 = 一组 路由 → 应答 的映射。
    /// 加载时会先铺 Default 场景，再用所选场景覆盖同名路由，所以场景文件只需要写"要改的那几条"。
    /// </summary>
    public class MockScenario
    {
        public MockScenario()
        {
            Routes = new Dictionary<string, MockResponse>(StringComparer.OrdinalIgnoreCase);
        }

        public string Name { get; set; }

        /// <summary>key = 路径（/LoginCheck），value = 应答</summary>
        public Dictionary<string, MockResponse> Routes { get; private set; }

        public MockResponse Find(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            MockResponse resp;
            if (Routes.TryGetValue(path, out resp)) return resp;

            // "/LoginCheck" 与 "LoginCheck" 都认
            string alt = path.StartsWith("/") ? path.Substring(1) : "/" + path;
            if (Routes.TryGetValue(alt, out resp)) return resp;

            // 兜底路由
            if (Routes.TryGetValue("*", out resp)) return resp;

            return null;
        }
    }
}
