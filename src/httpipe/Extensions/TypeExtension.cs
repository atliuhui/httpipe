using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;

namespace httpipe.Extensions
{
    internal static class TypeExtension
    {
        public static bool StartsWith(this string context, IEnumerable<string> values, StringComparison comparisonType)
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
        public static bool IsValidJson(string text)
        {
            try
            {
                JToken.Parse(text);
                return true;
            }
            catch
            {
                return false;
            }
        }
        public static string StringValue(this Dictionary<string, string> context, string key)
        {
            if (context.TryGetValue(key, out var value))
            {
                return value;
            }
            else
            {
                return string.Empty;
            }
        }
        public static string StringValue(this Dictionary<string, string> context, string key, string value)
        {
            if (context.ContainsKey(key))
            {
                context[key] = value;
            }
            else
            {
                context.Add(key, value);
            }

            return value;
        }
        public static string GetValue(this HttpRequestHeaders headers, string name)
        {
            if (!headers.Contains(name))
            {
                return default(string);
            }

            //http://pretty-rfc.herokuapp.com/RFC2616#general.header.fields
            if (name.Equals("Cache-Control", StringComparison.OrdinalIgnoreCase)) { return headers.CacheControl.ToString(); }
            else if (name.Equals("Connection", StringComparison.OrdinalIgnoreCase)) { return headers.Connection.ToString(); }
            else if (name.Equals("Date", StringComparison.OrdinalIgnoreCase)) { return headers.Date.ToString(); }
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
            else if (name.Equals("Authorization", StringComparison.OrdinalIgnoreCase)) { return headers.Authorization.ToString(); }
            else if (name.Equals("Expect", StringComparison.OrdinalIgnoreCase)) { return headers.Expect.ToString(); }
            else if (name.Equals("From", StringComparison.OrdinalIgnoreCase)) { return headers.From.ToString(); }
            else if (name.Equals("Host", StringComparison.OrdinalIgnoreCase)) { return headers.Host.ToString(); }
            else if (name.Equals("If-Match", StringComparison.OrdinalIgnoreCase)) { return headers.IfMatch.ToString(); }
            else if (name.Equals("If-Modified-Since", StringComparison.OrdinalIgnoreCase)) { return headers.IfModifiedSince.ToString(); }
            else if (name.Equals("If-None-Match", StringComparison.OrdinalIgnoreCase)) { return headers.IfNoneMatch.ToString(); }
            else if (name.Equals("If-Range", StringComparison.OrdinalIgnoreCase)) { return headers.IfRange.ToString(); }
            else if (name.Equals("If-Unmodified-Since", StringComparison.OrdinalIgnoreCase)) { return headers.IfUnmodifiedSince.ToString(); }
            else if (name.Equals("Max-Forwards", StringComparison.OrdinalIgnoreCase)) { return headers.MaxForwards.ToString(); }
            else if (name.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase)) { return headers.ProxyAuthorization.ToString(); }
            else if (name.Equals("Range", StringComparison.OrdinalIgnoreCase)) { return headers.Range.ToString(); }
            else if (name.Equals("Referer", StringComparison.OrdinalIgnoreCase)) { return headers.Referrer.ToString(); }
            else if (name.Equals("TE", StringComparison.OrdinalIgnoreCase)) { return headers.TE.ToString(); }
            else if (name.Equals("User-Agent", StringComparison.OrdinalIgnoreCase)) { return headers.UserAgent.ToString(); }
            else { return string.Join(", ", headers.GetValues(name)); }
        }
        public static string GetValue(this HttpResponseHeaders headers, string name)
        {
            if (!headers.Contains(name))
            {
                return default(string);
            }

            //http://pretty-rfc.herokuapp.com/RFC2616#general.header.fields
            if (name.Equals("Cache-Control", StringComparison.OrdinalIgnoreCase)) { return headers.CacheControl.ToString(); }
            else if (name.Equals("Connection", StringComparison.OrdinalIgnoreCase)) { return headers.Connection.ToString(); }
            else if (name.Equals("Date", StringComparison.OrdinalIgnoreCase)) { return headers.Date.ToString(); }
            else if (name.Equals("Pragma", StringComparison.OrdinalIgnoreCase)) { return headers.Pragma.ToString(); }
            else if (name.Equals("Trailer", StringComparison.OrdinalIgnoreCase)) { return headers.Trailer.ToString(); }
            else if (name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase)) { return headers.TransferEncoding.ToString(); }
            else if (name.Equals("Upgrade", StringComparison.OrdinalIgnoreCase)) { return headers.Upgrade.ToString(); }
            else if (name.Equals("Via", StringComparison.OrdinalIgnoreCase)) { return headers.Via.ToString(); }
            else if (name.Equals("Warning", StringComparison.OrdinalIgnoreCase)) { return headers.Warning.ToString(); }
            //http://pretty-rfc.herokuapp.com/RFC2616#response.header.fields
            else if (name.Equals("Accept-Ranges", StringComparison.OrdinalIgnoreCase)) { return headers.AcceptRanges.ToString(); }
            else if (name.Equals("Age", StringComparison.OrdinalIgnoreCase)) { return headers.Age.ToString(); }
            else if (name.Equals("ETag", StringComparison.OrdinalIgnoreCase)) { return headers.ETag.ToString(); }
            else if (name.Equals("Location", StringComparison.OrdinalIgnoreCase)) { return headers.Location.ToString(); }
            else if (name.Equals("Proxy-Authenticate", StringComparison.OrdinalIgnoreCase)) { return headers.ProxyAuthenticate.ToString(); }
            else if (name.Equals("Retry-After", StringComparison.OrdinalIgnoreCase)) { return headers.RetryAfter.ToString(); }
            else if (name.Equals("Server", StringComparison.OrdinalIgnoreCase)) { return headers.Server.ToString(); }
            else if (name.Equals("Vary", StringComparison.OrdinalIgnoreCase)) { return headers.Vary.ToString(); }
            else if (name.Equals("WWW-Authenticate", StringComparison.OrdinalIgnoreCase)) { return headers.WwwAuthenticate.ToString(); }
            else { return string.Join(", ", headers.GetValues(name)); }
        }
        public static string GetValue(this HttpContentHeaders headers, string name)
        {
            if (!headers.Contains(name))
            {
                return default(string);
            }

            //http://pretty-rfc.herokuapp.com/RFC2616#entity.header.fields
            if (name.Equals("Allow", StringComparison.OrdinalIgnoreCase)) { return headers.Allow.ToString(); }
            else if (name.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase)) { return headers.ContentEncoding.ToString(); }
            else if (name.Equals("Content-Language", StringComparison.OrdinalIgnoreCase)) { return headers.ContentLanguage.ToString(); }
            else if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) { return headers.ContentLength.ToString(); }
            else if (name.Equals("Content-Location", StringComparison.OrdinalIgnoreCase)) { return headers.ContentLocation.ToString(); }
            else if (name.Equals("Content-MD5", StringComparison.OrdinalIgnoreCase)) { return headers.ContentMD5.ToString(); }
            else if (name.Equals("Content-Range", StringComparison.OrdinalIgnoreCase)) { return headers.ContentRange.ToString(); }
            else if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)) { return headers.ContentType.ToString(); }
            else if (name.Equals("Expires", StringComparison.OrdinalIgnoreCase)) { return headers.Expires.ToString(); }
            else if (name.Equals("Last-Modified", StringComparison.OrdinalIgnoreCase)) { return headers.LastModified.ToString(); }
            else { return string.Join(", ", headers.GetValues(name)); }
        }
    }
}
