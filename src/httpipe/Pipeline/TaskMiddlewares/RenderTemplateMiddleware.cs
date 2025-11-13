using Httpipe.Extensions;
using Newtonsoft.Json.Linq;
using Serilog;

namespace Httpipe.Pipeline.TaskMiddlewares
{
    internal class RenderTemplateMiddleware : ITaskMiddleware
    {
        readonly string template;

        public RenderTemplateMiddleware(string template)
        {
            this.template = template;
        }

        public async Task InvokeAsync(TaskArguments arguments, TaskContext context, Func<Task> next)
        {
            var variables = JObject.FromObject(context.Items.GetValueOrDefault("variables", new Dictionary<string, string>()));
            if (FluidExtension.TryRender(variables, JObject.FromObject(context.Items), this.template, out var text, out var message))
            {
                ArgumentNullException.ThrowIfNull(text);

                context.Items.AddOrUpdate("message", text);

                await next();
            }
            else
            {
                Log.Error(message);
            }
        }
    }
}
