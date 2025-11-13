using Httpipe.Pipeline.TaskMiddlewares;
using Newtonsoft.Json;

namespace Httpipe.Pipeline
{
    internal class HttpTaskArguments : TaskArguments
    {
        public string ScriptPath { get; set; }
        public string CurrentDirectory { get; set; }
    }
    internal class HttpTaskContext : TaskContext
    {
    }

    static internal class TaskPipelineExtensions
    {
        const string BlockSeparator = "\n###";

        private static TaskBuilder UseHttpTask(this TaskBuilder builder, string template, HttpClient client)
        {
            builder.UseMiddleware(() => new RenderTemplateMiddleware(template));
            builder.UseMiddleware(() => new CreateRequestMiddleware());
            builder.UseMiddleware(() => new CreateResponseMiddleware(client));

            return builder;
        }
        public static TaskBuilder UseHttpTask(this TaskBuilder builder, string path)
        {
            var client = new HttpClient();
            var text = File.ReadAllText(path);
            foreach (var item in text.Trim().Split(BlockSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                builder.UseHttpTask(item, client);
            }

            return builder;
        }
        public static void LoadVariables(this HttpTaskContext context, string path, string? env)
        {
            if (string.IsNullOrEmpty(env)) { return; }

            var file = new FileInfo(path);
            if (file.Directory == null) { return; }

            var text = File.ReadAllText(Path.Combine(file.Directory.FullName, "http-client.env.json"));
            var dict = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(text);
            if (dict?.TryGetValue(env, out var vars) ?? false)
            {
                foreach (var item in vars)
                {
                    context.Items?.AddOrUpdate("variables", item.Key, item.Value);
                }
            }
        }
    }
}
