// See https://aka.ms/new-console-template for more information
using Httpipe.Extensions;
using Httpipe.Pipeline;
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

#if DEBUG
var path = @"samples\hello.http";
var env = "local";
#else
var path = args.ElementAt(0);
var env = args.ElementAtOrDefault(1);
#endif

var configuration = XDocument.Load(GetConfigName());

using (var meter = new TimeMeter(Path.GetFileNameWithoutExtension(path)))
{
    var builder = new TaskBuilder();
    var task = builder.UseHttpTask(path).Build();

    var arguments = new HttpTaskArguments
    {
        ScriptPath = path,
        CurrentDirectory = GetCurrentDirectory(),
    };
    var context = new HttpTaskContext
    {
    };
    context.LoadVariables(path, env);
    task(arguments, context).Wait();

    Log.Debug($"{(float)GC.GetTotalMemory(false) / 1024 / 1024:N2} MB");
}

static string GetCurrentDirectory()
{
    ArgumentNullException.ThrowIfNull(Environment.ProcessPath);
    var file = new FileInfo(Environment.ProcessPath);

    ArgumentNullException.ThrowIfNull(file.Directory);
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
