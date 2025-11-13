using Fluid;
using Fluid.Values;
using Newtonsoft.Json.Linq;

namespace Httpipe.Extensions
{
    internal static class FluidExtension
    {
        static TemplateOptions DefaultTemplateOptions()
        {
            TemplateOptions options = new TemplateOptions();
            options.MemberAccessStrategy.Register<JObject, object>((source, name) => source[name]);
            options.ValueConverters.Add(x => x is JObject o ? new ObjectValue(o) : null);
            options.ValueConverters.Add(x => x is JValue v ? v.Value : null);

            return options;
        }
        public static bool TryRender(JObject arguments, JObject context, string template, out string? text, out string? message)
        {
            var parser = new FluidParser();
            if (parser.TryParse(template, out var renderer, out var error))
            {
                var variables = new TemplateContext(arguments, DefaultTemplateOptions());
                variables.SetValue(nameof(context), context);

                try
                {
                    text = renderer.Render(variables);
                    message = null;
                    return true;
                }
                catch (Exception ex)
                {
                    text = null;
                    message = ex.GetBaseException().Message;
                    return false;
                }
            }
            else
            {
                text = null;
                message = error;
                return false;
            }
        }
    }
}
