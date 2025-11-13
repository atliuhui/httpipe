using CsvHelper;
using Httpipe.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Data;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;

namespace Httpipe.Pipeline.TaskMiddlewares
{
    internal class CreateResponseMiddleware : ITaskMiddleware
    {
        readonly HttpClient client;

        public CreateResponseMiddleware(HttpClient client)
        {
            this.client = client;
        }

        public async Task InvokeAsync(TaskArguments arguments, TaskContext context, Func<Task> next)
        {
            var request = context.Items.GetValueOrDefault<HttpRequestMessage>("request");
            if (request != null)
            {
                var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                SetResponse(arguments, context, response);

                Log.Information($"HTTP/{request.Version} {request.Method} {request.RequestUri} responded {(int)response.StatusCode}");
            }

            await next();
        }

        static void SetResponse(TaskArguments arguments, TaskContext context, HttpResponseMessage message)
        {
            context.Items.AddOrUpdate("response", message);

            if (message.Content != null)
            {
                var contentType = string.Empty;
                foreach (var item in message.Content.Headers)
                {
                    if (item.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                    {
                        contentType = GetHeaders(message.Content.Headers, item.Key);
                    }
                    //temp.Headers.Add(item.Key, message.Content.Headers.GetValue(item.Key));
                }

                if (contentType.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase))
                {
                    var content = message.Content.ReadAsStringAsync().Result;
                    context.Items.AddOrUpdate("data", content);
                }
                else if (contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
                {
                    var content = message.Content.ReadAsStringAsync().Result;
                    context.Items.AddOrUpdate("data", JToken.Parse(content));
                }
                else if (contentType.StartsWith("application/xml", StringComparison.OrdinalIgnoreCase))
                {
                    var content = message.Content.ReadAsStringAsync().Result;
                    context.Items.AddOrUpdate("data", content);
                }
                else if (contentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
                {
                    var contentPair = ParseUrlEncodedContent(message.Content.ReadAsStringAsync().Result);
                    var content = $"{string.Join("&", contentPair.Select(item => $"{item.Key}={WebUtility.UrlDecode(item.Value)}"))}";
                    context.Items.AddOrUpdate("data", content);
                }
                else if (contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
                {
                    var content = message.Content.ReadAsStringAsync().Result;
                    context.Items.AddOrUpdate("data", content);
                }
                else if (contentType.StartsWith("text/csv", StringComparison.OrdinalIgnoreCase))
                {
                    var content = message.Content.ReadAsStringAsync().Result;
                    context.Items.AddOrUpdate("data", JArray.FromObject(CsvToTable(content)));
                }
                else // application/octet-stream
                {
                    var content = $"{Convert.ToBase64String(message.Content.ReadAsByteArrayAsync().Result)}";
                    context.Items.AddOrUpdate("data", content);
                }
            }
        }
        static DataTable CsvToTable(string csvText)
        {
            var context = new DataTable();
            using (var reader = new StringReader(csvText))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                using (var data = new CsvDataReader(csv))
                {
                    context.Load(data);
                }
            }

            return context;
        }
        static Dictionary<string, string> ParseUrlEncodedContent(string contentText)
        {
            Dictionary<string, string> contentPair;
            if (contentText.IsJson())
            {
                contentPair = JsonConvert.DeserializeObject<Dictionary<string, string>>(contentText) ?? throw new ArgumentException(nameof(contentText));
            }
            else
            {
                contentPair = new Dictionary<string, string>();
                foreach (var item in contentText.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var pair = item.Split('=', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    contentPair.AddOrUpdate(pair.ElementAt(0), pair.ElementAt(1));
                }
            }

            return contentPair;
        }
        static string GetHeaders(HttpContentHeaders headers, string name)
        {
            if (!headers.Contains(name))
            {
                return string.Empty;
            }

            //http://pretty-rfc.herokuapp.com/RFC2616#entity.header.fields
            if (name.Equals("Allow", StringComparison.OrdinalIgnoreCase)) { return headers.Allow.ToString() ?? string.Empty; }
            else if (name.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase)) { return headers.ContentEncoding.ToString() ?? string.Empty; }
            else if (name.Equals("Content-Language", StringComparison.OrdinalIgnoreCase)) { return headers.ContentLanguage.ToString() ?? string.Empty; }
            else if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) { return headers.ContentLength?.ToString() ?? string.Empty; }
            else if (name.Equals("Content-Location", StringComparison.OrdinalIgnoreCase)) { return headers.ContentLocation?.ToString() ?? string.Empty; }
            else if (name.Equals("Content-MD5", StringComparison.OrdinalIgnoreCase)) { return headers.ContentMD5?.ToString() ?? string.Empty; }
            else if (name.Equals("Content-Range", StringComparison.OrdinalIgnoreCase)) { return headers.ContentRange?.ToString() ?? string.Empty; }
            else if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)) { return headers.ContentType?.ToString() ?? string.Empty; }
            else if (name.Equals("Expires", StringComparison.OrdinalIgnoreCase)) { return headers.Expires?.ToString() ?? string.Empty; }
            else if (name.Equals("Last-Modified", StringComparison.OrdinalIgnoreCase)) { return headers.LastModified?.ToString() ?? string.Empty; }
            else { return string.Join(", ", headers.GetValues(name)); }
        }
        static string GetHeaders(HttpResponseHeaders headers, string name)
        {
            if (!headers.Contains(name))
            {
                return string.Empty;
            }

            //http://pretty-rfc.herokuapp.com/RFC2616#general.header.fields
            if (name.Equals("Cache-Control", StringComparison.OrdinalIgnoreCase)) { return headers.CacheControl?.ToString() ?? string.Empty; }
            else if (name.Equals("Connection", StringComparison.OrdinalIgnoreCase)) { return headers.Connection.ToString(); }
            else if (name.Equals("Date", StringComparison.OrdinalIgnoreCase)) { return headers.Date?.ToString() ?? string.Empty; }
            else if (name.Equals("Pragma", StringComparison.OrdinalIgnoreCase)) { return headers.Pragma.ToString(); }
            else if (name.Equals("Trailer", StringComparison.OrdinalIgnoreCase)) { return headers.Trailer.ToString(); }
            else if (name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase)) { return headers.TransferEncoding.ToString(); }
            else if (name.Equals("Upgrade", StringComparison.OrdinalIgnoreCase)) { return headers.Upgrade.ToString(); }
            else if (name.Equals("Via", StringComparison.OrdinalIgnoreCase)) { return headers.Via.ToString(); }
            else if (name.Equals("Warning", StringComparison.OrdinalIgnoreCase)) { return headers.Warning.ToString(); }
            //http://pretty-rfc.herokuapp.com/RFC2616#response.header.fields
            else if (name.Equals("Accept-Ranges", StringComparison.OrdinalIgnoreCase)) { return headers.AcceptRanges.ToString(); }
            else if (name.Equals("Age", StringComparison.OrdinalIgnoreCase)) { return headers.Age?.ToString() ?? string.Empty; }
            else if (name.Equals("ETag", StringComparison.OrdinalIgnoreCase)) { return headers.ETag?.ToString() ?? string.Empty; }
            else if (name.Equals("Location", StringComparison.OrdinalIgnoreCase)) { return headers.Location?.ToString() ?? string.Empty; }
            else if (name.Equals("Proxy-Authenticate", StringComparison.OrdinalIgnoreCase)) { return headers.ProxyAuthenticate.ToString(); }
            else if (name.Equals("Retry-After", StringComparison.OrdinalIgnoreCase)) { return headers.RetryAfter?.ToString() ?? string.Empty; }
            else if (name.Equals("Server", StringComparison.OrdinalIgnoreCase)) { return headers.Server.ToString(); }
            else if (name.Equals("Vary", StringComparison.OrdinalIgnoreCase)) { return headers.Vary.ToString(); }
            else if (name.Equals("WWW-Authenticate", StringComparison.OrdinalIgnoreCase)) { return headers.WwwAuthenticate.ToString(); }
            else { return string.Join(", ", headers.GetValues(name)); }
        }
    }
}
