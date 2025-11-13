using Newtonsoft.Json.Linq;

namespace Httpipe.Extensions
{
    internal static class TypeExtension
    {
        public static bool IsJson(this string text)
        {
            return Safety(() => JToken.Parse(text));
        }
        public static bool Safety(Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}
