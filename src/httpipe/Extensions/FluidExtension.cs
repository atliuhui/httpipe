using Fluid;
using Fluid.Values;
using Newtonsoft.Json.Linq;

namespace httpipe.Extensions
{
    internal static class FluidExtension
    {
        static ValueTask<FluidValue> JsonEncodeFilter(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var value = input.ToObjectValue();
            var format = JToken.FromObject(value).ToString(Newtonsoft.Json.Formatting.None);
            //var format = JToken.FromObject(value).ToString();
            return new StringValue(format, false);
        }
        static TemplateOptions DefaultTemplateOptions()
        {
            TemplateOptions options = new TemplateOptions();
            options.MemberAccessStrategy.Register<JObject, object>((source, name) => source[name]);
            options.ValueConverters.Add(x => x is JObject o ? new ObjectValue(o) : null);
            options.ValueConverters.Add(x => x is JValue v ? v.Value : null);
            options.Filters.AddFilter("json_encode", JsonEncodeFilter);

            return options;
        }
        public static bool TryRender(JObject content, JObject context, string template, out string? text, out string? message, string rootVariableName = "context")
        {
            var parser = new FluidParser();
            if (parser.TryParse(template, out var renderer, out var error))
            {
                var variables = new TemplateContext(content, DefaultTemplateOptions());
                variables.SetValue(rootVariableName, context);

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
