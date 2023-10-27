// See https://aka.ms/new-console-template for more information
using httpipe.Services;
using Newtonsoft.Json.Linq;
using Serilog;

Log.Logger = new LoggerConfiguration()
#if DEBUG
    .ReadFrom.AppSettings()
#else
    .ReadFrom.AppSettings(filePath: Path.Combine(GetCurrentDirectory(), GetConfigName()))
#endif
    .WriteTo.Console()
    .CreateLogger();

var context = new Dictionary<string, JToken>
{
    ["CurrentDirectory"] = GetCurrentDirectory(),
};
var vars = new VariableService(context);
var pipe = new PipelineService(vars);

#if DEBUG
var path = @"samples\hello.http";
#else
var path = args.ElementAt(0) ?? throw new ArgumentNullException("path");
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