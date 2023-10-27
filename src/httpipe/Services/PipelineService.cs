using httpipe.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Data;
using System.Net;
using System.Text;

namespace httpipe.Services
{
    internal class PipelineService
    {
        const string BlockSeparator = "\n###";
        const string LineSeparator = "\n";
        const string CommentSeparator = "#";
        const string VariableSeparator = "@";
        const string FunctionSeparator = "$";

        readonly VariableService context;

        public PipelineService(VariableService context)
        {
            this.context = context;
        }

        public void Execute(string text)
        {
            var counter = 1;
            using (var all = new ActionTimer($"Pipeline"))
            {
                foreach (var item in text.Trim().Split(BlockSeparator, StringSplitOptions.RemoveEmptyEntries))
                {
                    using (var timer = new ActionTimer($"Block#{counter++}", true))
                    {
                        ExecuteBlock(item);
                    }
                    Log.Debug($"==========================");
                }
            }
        }
        private void ExecuteBlock(string text)
        {
            var lines = new Queue<string>(text.Trim().Split(LineSeparator));
            var currentPosition = TextPosition.Unknown;

            var message = new HttpRequestMessage();
            var requestUri = string.Empty;
            var contentType = string.Empty;

            while (lines.Count > 0)
            {
                var line = lines.Dequeue();
                var currentLine = line.Trim();
                var nextLine = lines.Any() ? lines.Peek().Trim() : string.Empty;

                if (currentPosition == TextPosition.Unknown)
                {
                    if (currentLine.StartsWith(VariableSeparator, StringComparison.OrdinalIgnoreCase))
                    {
                        currentPosition = TextPosition.Variable;
                    }
                    else if (currentLine.StartsWith(FunctionSeparator, StringComparison.OrdinalIgnoreCase))
                    {
                        currentPosition = TextPosition.Function;
                    }
                    else if (currentLine.StartsWith(CommentSeparator, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    else if (string.IsNullOrEmpty(currentLine))
                    {
                        continue;
                    }
                    else if (currentLine.StartsWith(new string[] { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS", "TRACE" }, StringComparison.OrdinalIgnoreCase))
                    {
                        currentPosition = TextPosition.Route;
                    }
                }

                switch (currentPosition)
                {
                    case TextPosition.Variable:
                        currentPosition = TextPosition.Unknown;
                        SetVariable(currentLine);
                        break;
                    case TextPosition.Function:
                        currentPosition = TextPosition.Unknown;
                        RunFunction(currentLine);
                        break;
                    case TextPosition.Route:
                        if (string.IsNullOrEmpty(nextLine?.Trim()))
                        {
                            currentPosition = TextPosition.Content;
                        }
                        else
                        {
                            currentPosition = TextPosition.Headers;
                        }
                        SetAddress(message, currentLine, ref requestUri);
                        break;
                    case TextPosition.Headers:
                        if (string.IsNullOrEmpty(nextLine?.Trim()))
                        {
                            currentPosition = TextPosition.Content;
                        }
                        if (currentLine.StartsWith(CommentSeparator))
                        {
                            break;
                        }
                        SetHeaders(message, currentLine, ref requestUri, ref contentType);
                        break;
                    case TextPosition.Content:
                        var contentLines = lines.ToArray();
                        var contentText = string.Join(LineSeparator, contentLines).Trim();
                        lines.Clear();
                        SetContent(message, contentType, contentText, ref requestUri);
                        break;
                    case TextPosition.Unknown:
                    default:
                        currentPosition = TextPosition.Unknown;
                        break;
                }
            }

            if (string.IsNullOrEmpty(requestUri)) return;

            message.RequestUri = new Uri(requestUri);

            using (var timer = new ActionTimer(nameof(HttpClient)))
            {
                var client = new HttpClient();
                var response = client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead).Result;
                this.SetResponse(response);
            }
        }

        private void FuncDebugging()
        {
            Log.Information($"---context---");
            foreach (var item in this.context.Context)
            {
                Log.Information($"{item.Key}={item.Value}");
            }
            Log.Information($"---content---");
            foreach (var item in this.context.Content)
            {
                Log.Information($"{item.Key}={item.Value}");
            }
            Log.Information($"-------------");
        }
        private void RunFunction(string line)
        {
            if ("$debugging".StartsWith(line, StringComparison.OrdinalIgnoreCase))
            {
                this.FuncDebugging();
            }
            else
            {
                Log.Error($"unknown function {line}");
            }
        }
        private void SetVariable(string line)
        {
            var pair = line.Split('=');
            var name = pair.ElementAt(0).Trim().TrimStart('@');
            var template = pair.ElementAt(1).Trim();
            this.context.AddOrUpdateWithTemplate(name, template);
        }
        private void SetAddress(HttpRequestMessage message, string line, ref string requestUri)
        {
            var address = line.Split(' ');
            requestUri = this.context.RenderValue(address.ElementAt(1).Trim());
            message.Method = new HttpMethod(address.ElementAt(0).Trim());
            message.Version = new Version(address.ElementAt(2).Split('/').ElementAt(1).Trim());
        }
        private void SetHeaders(HttpRequestMessage message, string line, ref string requestUri, ref string contentType)
        {
            var separator = ':';
            var header = new Queue<string>(line.Split(separator));
            var name = header.Dequeue().Trim();
            var value = string.Join(separator.ToString(), header.ToArray()).Trim();
            header.Clear();

            if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                //do nothing
            }
            else if (name.Equals("X-Request-Type", StringComparison.OrdinalIgnoreCase))
            {
                //do nothing
            }
            else if (name.Equals("Host", StringComparison.OrdinalIgnoreCase))
            {
                if (!requestUri.StartsWith(value))
                {
                    requestUri = $"http://{value}{requestUri}";
                }

                message.Headers.TryAddWithoutValidation(name, value);
            }
            else if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                contentType = value;

                message.Headers.TryAddWithoutValidation(name, value);
            }
            else if (name.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
            {
                var authorization = value.Split(' ');
                var schema = authorization.ElementAt(0).Trim();
                var token = authorization.ElementAt(1).Trim();

                if (schema.Equals("Basic", StringComparison.OrdinalIgnoreCase) && token.Contains(':'))
                {
                    value = $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(token))}";
                }

                message.Headers.TryAddWithoutValidation(name, value);
            }
            else
            {
                message.Headers.TryAddWithoutValidation(name, this.context.RenderValue(value));
            }
        }
        private void SetContent(HttpRequestMessage message, string contentType, string contentText, ref string requestUri)
        {
            var formatted = this.context.RenderValue(contentText);

            if (message.Method == HttpMethod.Get && !string.IsNullOrEmpty(formatted))
            {
                var contentPair = ParseUrlEncodedContent(formatted);
                var query = string.Join("&", contentPair.Select(item => $"{item.Key}={WebUtility.UrlEncode(item.Value)}"));
                requestUri = $"{requestUri}?{query}";
            }
            else if (contentType.IndexOf("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var contentPair = ParseUrlEncodedContent(formatted);
                var content = new FormUrlEncodedContent(contentPair.Select(item => new KeyValuePair<string, string>(item.Key, WebUtility.UrlEncode(item.Value))));
                message.Content = content;
            }
            else if (contentType.IndexOf("application/xml", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var content = new StringContent(formatted, Encoding.UTF8, "application/xml");
                message.Content = content;
            }
            else if (contentType.IndexOf("text/plain", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var content = new StringContent(formatted, Encoding.UTF8, "text/plain");
                message.Content = content;
            }
            else if (contentType.IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (TypeExtension.IsValidJson(formatted))
                {
                    var content = new StringContent(formatted, Encoding.UTF8, "application/json");
                    message.Content = content;
                }
            }
            else // application/octet-stream
            {
                var content = new ByteArrayContent(Encoding.UTF8.GetBytes(contentText));
                message.Content = content;
            }
        }
        private void SetResponse(HttpResponseMessage message)
        {
            var contentType = string.Empty;
            var temp = new HttpResponseText();

            //temp.Protocol = "HTTP";
            //temp.Version = responseMessage.Version.ToString();
            temp.Code = (int)message.StatusCode;
            temp.Message = message.ReasonPhrase;

            foreach (var item in message.Headers)
            {
                temp.Headers.Add(item.Key, message.Headers.GetValue(item.Key));
            }

            this.context.AddOrUpdateContext("Text", string.Empty);
            this.context.AddOrUpdateContext("Json", string.Empty);
            this.context.AddOrUpdateContext("Xml", string.Empty);
            this.context.AddOrUpdateContext("Form", string.Empty);
            this.context.AddOrUpdateContext("Base64", string.Empty);
            this.context.AddOrUpdateContext("Response", string.Empty);

            if (message.Content != null)
            {
                foreach (var item in message.Content.Headers)
                {
                    if (item.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                    {
                        contentType = message.Content.Headers.GetValue(item.Key);
                    }
                    temp.Headers.Add(item.Key, message.Content.Headers.GetValue(item.Key));
                }

                if (contentType.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase))
                {
                    temp.Content = message.Content.ReadAsStringAsync().Result;
                    this.context.AddOrUpdateContext("Text", temp.Content);
                }
                else if (contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
                {
                    temp.Content = message.Content.ReadAsStringAsync().Result;
                    this.context.AddOrUpdateContext("Json", JToken.Parse(temp.Content));
                }
                else if (contentType.StartsWith("application/xml", StringComparison.OrdinalIgnoreCase))
                {
                    temp.Content = message.Content.ReadAsStringAsync().Result;
                    this.context.AddOrUpdateContext("Xml", temp.Content);
                }
                else if (contentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
                {
                    var contentPair = ParseUrlEncodedContent(message.Content.ReadAsStringAsync().Result);
                    temp.Content = $"{string.Join("&", contentPair.Select(item => $"{item.Key}={WebUtility.UrlDecode(item.Value)}"))}";
                    this.context.AddOrUpdateContext("Form", temp.Content);
                }
                else if (contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
                {
                    temp.Content = message.Content.ReadAsStringAsync().Result;
                    this.context.AddOrUpdateContext("Html", temp.Content);
                }
                else if (contentType.StartsWith("text/csv", StringComparison.OrdinalIgnoreCase))
                {
                    temp.Content = message.Content.ReadAsStringAsync().Result;
                    this.context.AddOrUpdateContext("Csv", JArray.FromObject(new DataTable().Load(temp.Content)));
                }
                else // application/octet-stream
                {
                    temp.Content = $"{Convert.ToBase64String(message.Content.ReadAsByteArrayAsync().Result)}";
                    this.context.AddOrUpdateContext("Base64", temp.Content);
                }
            }

            this.context.AddOrUpdateContext("Response", JObject.FromObject(temp));
        }
        private Dictionary<string, string> ParseUrlEncodedContent(string contentText)
        {
            Dictionary<string, string> contentPair;
            if (TypeExtension.IsValidJson(contentText))
            {
                contentPair = JsonConvert.DeserializeObject<Dictionary<string, string>>(contentText);
            }
            else
            {
                contentPair = new Dictionary<string, string>();
                foreach (var item in contentText.Split('&'))
                {
                    var pair = item.Trim().Split('=');
                    contentPair.StringValue(pair.ElementAt(0).Trim(), pair.ElementAt(1).Trim());
                }
            }

            return contentPair;
        }
    }

    enum TextPosition
    {
        Unknown,
        Route,
        Headers,
        Content,
        Variable,
        Function,
    }
}
