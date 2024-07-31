// See https://aka.ms/new-console-template for more information
using httpipe.Services;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Xml.Linq;
using System.Xml.XPath;

Log.Logger = new LoggerConfiguration()
#if DEBUG
    .ReadFrom.AppSettings()
#else
    .ReadFrom.AppSettings(filePath: Path.Combine(GetCurrentDirectory(), GetConfigName()))
#endif
    .WriteTo.Console()
    .CreateLogger();

var configuration = XDocument.Load(GetConfigName());
var assertHttpCodes = GetValue("assert:csv:httpCodes", configuration)?.Split(',')
    .Select(item => int.Parse(item.Trim())).ToList()
    ?? Enumerable.Empty<int>().ToList();
var context = new Dictionary<string, JToken>
{
    ["CurrentDirectory"] = GetCurrentDirectory(),
    ["State"] = State.Completed.ToString(),
};
var vars = new VariableService(context);
var pipe = new PipelineService(vars, assertHttpCodes);

#if DEBUG
var path = @"samples\hello.http";
#else
var path = args.ElementAtOrDefault(0) ?? throw new ArgumentNullException("path");
#endif

var text = File.ReadAllText(path);
pipe.Execute(text);

static string GetCurrentDirectory()
{
    var file = new FileInfo(Environment.ProcessPath);
    return file.Directory.FullName;
}
static string GetConfigName()
{
    return $"{Path.GetFileNameWithoutExtension(Environment.ProcessPath)}.dll.config";
}
static string? GetValue(string key, XDocument document)
{
    return document.Root?.XPathSelectElement($"appSettings/add[@key='{key}']")?.Attribute("value")?.Value;
}
