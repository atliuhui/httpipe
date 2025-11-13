using Httpipe.Extensions;
using Newtonsoft.Json;
using Serilog;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Httpipe.Pipeline.TaskMiddlewares
{
    enum TextPosition
    {
        Unknown,
        Route,
        Headers,
        Content,
        Variable,
        Function,
    }

    internal class CreateRequestMiddleware : ITaskMiddleware
    {
        const string LineSeparator = "\n";
        const string CommentSeparator = "#";
        const string VariableSeparator = "@";
        const string FunctionSeparator = "#$";

        public CreateRequestMiddleware()
        {
        }

        public async Task InvokeAsync(TaskArguments arguments, TaskContext context, Func<Task> next)
        {
            var message = context.Items.GetRequiredValue<string>("message");

            var lines = new Queue<string>(message.Trim().Split(LineSeparator, StringSplitOptions.TrimEntries));
            var currentPosition = TextPosition.Unknown;

            var request = new HttpRequestMessage();
            var requestUri = string.Empty;
            var contentType = string.Empty;

            while (lines.Count > 0)
            {
                var currentLine = lines.Dequeue();
                var nextLine = lines.Any() ? lines.Peek() : string.Empty;

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
                    else if (StartsWith(currentLine, new string[] { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS", "TRACE" }, StringComparison.OrdinalIgnoreCase))
                    {
                        currentPosition = TextPosition.Route;
                    }
                }

                switch (currentPosition)
                {
                    case TextPosition.Variable:
                        currentPosition = TextPosition.Unknown;
                        SetVariable(arguments, context, currentLine);
                        break;
                    case TextPosition.Function:
                        currentPosition = TextPosition.Unknown;
                        var continued = RunFunction(arguments, context, currentLine);
                        if (continued == false) return;
                        break;
                    case TextPosition.Route:
                        if (string.IsNullOrEmpty(nextLine))
                        {
                            currentPosition = TextPosition.Content;
                        }
                        else
                        {
                            currentPosition = TextPosition.Headers;
                        }
                        SetAddress(request, currentLine, ref requestUri);
                        break;
                    case TextPosition.Headers:
                        if (string.IsNullOrEmpty(nextLine))
                        {
                            currentPosition = TextPosition.Content;
                        }
                        if (currentLine.StartsWith(CommentSeparator))
                        {
                            break;
                        }
                        SetHeaders(request, currentLine, ref requestUri, ref contentType);
                        break;
                    case TextPosition.Content:
                        var contentLines = lines.ToArray();
                        var contentText = string.Join(LineSeparator, contentLines);
                        lines.Clear();
                        SetContent(request, contentType, contentText, ref requestUri);
                        break;
                    case TextPosition.Unknown:
                    default:
                        currentPosition = TextPosition.Unknown;
                        break;
                }
            }

            if (string.IsNullOrEmpty(requestUri) == false)
            {
                request.RequestUri = new Uri(requestUri);
                context.Items.AddOrUpdate("request", request);
            }
            else
            {
                context.Items.Remove("request");
            }

            await next();
        }

        static void SetVariable(TaskArguments arguments, TaskContext context, string line)
        {
            var pair = line.Split('=', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var name = pair.ElementAt(0).TrimStart(VariableSeparator.ToCharArray());
            var value = pair.ElementAtOrDefault(1) ?? string.Empty;
            context.Items?.AddOrUpdate("variables", name, value);
        }
        static bool RunFunction(TaskArguments arguments, TaskContext context, string line)
        {
            var pair = line.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var name = pair.ElementAt(0).TrimStart(FunctionSeparator.ToCharArray());
            var args = pair.ElementAtOrDefault(1)?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? new string[0];

            return FuncFactory(arguments, context, name, args);
        }
        static void SetAddress(HttpRequestMessage message, string line, ref string requestUri)
        {
            var address = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            requestUri = address.ElementAt(1);
            message.Method = new HttpMethod(address.ElementAt(0));
            message.Version = new Version((address.ElementAtOrDefault(2) ?? "HTTP/1.1").Split('/').ElementAt(1));
        }
        static void SetHeaders(HttpRequestMessage message, string line, ref string requestUri, ref string contentType)
        {
            var separator = ':';
            var parts = line.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var name = parts.ElementAt(0);
            var value = string.Join(separator.ToString(), parts.Skip(1));

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
                var authorization = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var schema = authorization.ElementAt(0);
                var token = authorization.ElementAt(1);

                if (schema.Equals("Basic", StringComparison.OrdinalIgnoreCase) && token.Contains(':'))
                {
                    value = $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(token))}";
                }

                message.Headers.TryAddWithoutValidation(name, value);
            }
            else
            {
                message.Headers.TryAddWithoutValidation(name, value);
            }
        }
        static void SetContent(HttpRequestMessage message, string contentType, string contentText, ref string requestUri)
        {
            if (message.Method == HttpMethod.Get && !string.IsNullOrEmpty(contentText))
            {
                var contentPair = ParseUrlEncodedContent(contentText);
                var query = string.Join("&", contentPair.Select(item => $"{item.Key}={WebUtility.UrlEncode(item.Value)}"));
                requestUri = $"{requestUri}?{query}";
            }
            else if (contentType.IndexOf("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var contentPair = ParseUrlEncodedContent(contentText);
                var content = new FormUrlEncodedContent(contentPair.Select(item => new KeyValuePair<string, string>(item.Key, WebUtility.UrlEncode(item.Value))));
                message.Content = content;
            }
            else if (contentType.IndexOf("application/xml", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var content = new StringContent(contentText, Encoding.UTF8, "application/xml");
                message.Content = content;
            }
            else if (contentType.IndexOf("text/plain", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var content = new StringContent(contentText, Encoding.UTF8, "text/plain");
                message.Content = content;
            }
            else if (contentType.IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (contentText.IsJson())
                {
                    var content = new StringContent(contentText, Encoding.UTF8, "application/json");
                    message.Content = content;
                }
            }
            else // application/octet-stream
            {
                var content = new ByteArrayContent(Encoding.UTF8.GetBytes(contentText));
                message.Content = content;
            }
        }
        static string GetHeaders(HttpRequestHeaders headers, string name)
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
            //http://pretty-rfc.herokuapp.com/RFC2616#request.header.fields
            else if (name.Equals("Accept", StringComparison.OrdinalIgnoreCase)) { return headers.Accept.ToString(); }
            else if (name.Equals("Accept-Charset", StringComparison.OrdinalIgnoreCase)) { return headers.AcceptCharset.ToString(); }
            else if (name.Equals("Accept-Encoding", StringComparison.OrdinalIgnoreCase)) { return headers.AcceptEncoding.ToString(); }
            else if (name.Equals("Accept-Language", StringComparison.OrdinalIgnoreCase)) { return headers.AcceptLanguage.ToString(); }
            else if (name.Equals("Authorization", StringComparison.OrdinalIgnoreCase)) { return headers.Authorization?.ToString() ?? string.Empty; }
            else if (name.Equals("Expect", StringComparison.OrdinalIgnoreCase)) { return headers.Expect.ToString(); }
            else if (name.Equals("From", StringComparison.OrdinalIgnoreCase)) { return headers.From?.ToString() ?? string.Empty; }
            else if (name.Equals("Host", StringComparison.OrdinalIgnoreCase)) { return headers.Host?.ToString() ?? string.Empty; }
            else if (name.Equals("If-Match", StringComparison.OrdinalIgnoreCase)) { return headers.IfMatch.ToString(); }
            else if (name.Equals("If-Modified-Since", StringComparison.OrdinalIgnoreCase)) { return headers.IfModifiedSince?.ToString() ?? string.Empty; }
            else if (name.Equals("If-None-Match", StringComparison.OrdinalIgnoreCase)) { return headers.IfNoneMatch.ToString(); }
            else if (name.Equals("If-Range", StringComparison.OrdinalIgnoreCase)) { return headers.IfRange?.ToString() ?? string.Empty; }
            else if (name.Equals("If-Unmodified-Since", StringComparison.OrdinalIgnoreCase)) { return headers.IfUnmodifiedSince?.ToString() ?? string.Empty; }
            else if (name.Equals("Max-Forwards", StringComparison.OrdinalIgnoreCase)) { return headers.MaxForwards?.ToString() ?? string.Empty; }
            else if (name.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase)) { return headers.ProxyAuthorization?.ToString() ?? string.Empty; }
            else if (name.Equals("Range", StringComparison.OrdinalIgnoreCase)) { return headers.Range?.ToString() ?? string.Empty; }
            else if (name.Equals("Referer", StringComparison.OrdinalIgnoreCase)) { return headers.Referrer?.ToString() ?? string.Empty; }
            else if (name.Equals("TE", StringComparison.OrdinalIgnoreCase)) { return headers.TE.ToString(); }
            else if (name.Equals("User-Agent", StringComparison.OrdinalIgnoreCase)) { return headers.UserAgent.ToString(); }
            else { return string.Join(", ", headers.GetValues(name)); }
        }
        static bool StartsWith(string context, IEnumerable<string> values, StringComparison comparisonType)
        {
            foreach (var item in values)
            {
                if (context.StartsWith(item, comparisonType))
                {
                    return true;
                }
            }

            return false;
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

        static bool FuncFactory(TaskArguments arguments, TaskContext context, string name, string[] args)
        {
            switch (name.ToLower())
            {
                case "debugging":
                    return TypeExtension.Safety(() => FuncDebugging(arguments, context, args));
                case "assert":
                    return TypeExtension.Safety(() => FuncAssert(arguments, context, args));
                default:
                    throw new Exception($"unknown function {name}");
            }
        }
        static void FuncDebugging(TaskArguments arguments, TaskContext context, string[] args)
        {
            Log.Information($"---arguments---");
            Log.Information($"{JsonConvert.SerializeObject(arguments)}");
            Log.Information($"---variables---");
            Log.Information($"{JsonConvert.SerializeObject(context.Items.GetValueOrDefault("variables", new Dictionary<string, string>()))}");
            Log.Information($"---data---");
            Log.Information($"{context.Items.GetValueOrDefault("data", string.Empty)}");
            Log.Information($"---context---");
            foreach (var item in context.Items)
            {
                Log.Information($"{item.Key}=");
            }
            Log.Information($"-------------");
        }
        static void FuncAssert(TaskArguments arguments, TaskContext context, string[] args)
        {
            var response = context.Items.GetRequiredValue<HttpResponseMessage>("response");

            var code = ((int)response.StatusCode).ToString();
            if (args.Contains(code, StringComparer.OrdinalIgnoreCase) == false)
            {
                throw new Exception($"assert not passed. {code} is not in [{string.Join(',', args)}]");
            }
        }
    }
}
