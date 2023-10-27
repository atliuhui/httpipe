namespace httpipe.Extensions
{
    internal class HttpResponseText
    {
        public int Code { get; set; }
        public string Message { get; set; }
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
        public string Content { get; set; }
    }
}
