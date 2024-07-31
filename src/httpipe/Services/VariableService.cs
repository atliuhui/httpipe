using httpipe.Extensions;
using Newtonsoft.Json.Linq;

namespace httpipe.Services
{
    internal class VariableService
    {
        public Dictionary<string, JToken> Context;
        public Dictionary<string, string> Content = new Dictionary<string, string>();

        public VariableService(Dictionary<string, JToken> context)
        {
            this.Context = context;
        }
        public string RenderValue(string template)
        {
            if (FluidExtension.TryRender(JObject.FromObject(this.Content), JObject.FromObject(this.Context), template, out var text, out var message))
            {
                return text ?? string.Empty;
            }

            return string.Empty;
        }

        public void AddOrUpdateContext(string key, JToken value)
        {
            if (this.Context.ContainsKey(key))
            {
                this.Context[key] = value;
            }
            else
            {
                this.Context.Add(key, value);
            }
        }
        public void AddOrUpdate(string key, string? value)
        {
            if (this.Content.ContainsKey(key))
            {
                this.Content[key] = value ?? string.Empty;
            }
            else
            {
                this.Content.Add(key, value ?? string.Empty);
            }
        }
        public void AddOrUpdateWithTemplate(string key, string template)
        {
            this.AddOrUpdate(key, this.RenderValue(template));
        }
        public JToken GetContext(string key)
        {
            if (this.Context.TryGetValue(key, out var value))
            {
                return value;
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
