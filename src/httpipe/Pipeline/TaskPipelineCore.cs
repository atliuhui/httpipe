namespace Httpipe.Pipeline
{
    public interface ITaskMiddleware
    {
        Task InvokeAsync(TaskArguments arguments, TaskContext context, Func<Task> next);
    }

    public class TaskBuilder
    {
        readonly List<Func<TaskArguments, TaskContext, Func<Task>, Task>> middlewares;

        public TaskBuilder()
        {
            middlewares = new List<Func<TaskArguments, TaskContext, Func<Task>, Task>>();
        }

        public TaskBuilder Use(Func<TaskArguments, TaskContext, Func<Task>, Task> middleware)
        {
            middlewares.Add(middleware);
            return this;
        }
        public TaskBuilder UseMiddleware<T>(Func<T> factory)
            where T : ITaskMiddleware
        {
            return Use((arguments, context, next) =>
            {
                var instance = factory();
                return instance.InvokeAsync(arguments, context, next);
            });
        }
        public Func<TaskArguments, TaskContext, Task> Build()
        {
            return (arguments, context) =>
            {
                Task Next(int index)
                {
                    if (index < middlewares.Count)
                    {
                        return middlewares[index](arguments, context, () => Next(index + 1));
                    }
                    else
                    {
                        return Task.CompletedTask;
                    }
                }
                ;
                return Next(0);
            };
        }
    }

    public abstract class TaskArguments
    {
    }
    public abstract class TaskContext
    {
        public Dictionary<string, object> Items { get; set; } = new Dictionary<string, object>();
    }
    public static class DictionaryExtension
    {
        public static void AddOrUpdate<T>(this Dictionary<string, T> context, string key, T value)
        {
            if (context.ContainsKey(key))
            {
                context[key] = value;
            }
            else
            {
                context.Add(key, value);
            }
        }
        public static void AddOrUpdate(this Dictionary<string, object> context, string space, string key, string value)
        {
            if (context.TryGetValue(space, out var obj) && (obj is Dictionary<string, string> dict))
            {
                dict.AddOrUpdate(key, value);
            }
            else
            {
                context.AddOrUpdate(space, new Dictionary<string, string> { { key, value } });
            }
        }
        public static T? GetValueOrDefault<T>(this Dictionary<string, object> context, string key)
        {
            if (context.ContainsKey(key))
            {
                return (T)context[key];
            }
            else
            {
                return default(T);
            }
        }
        public static T GetRequiredValue<T>(this Dictionary<string, object> context, string key)
        {
            if (context.ContainsKey(key))
            {
                var val = context[key];
                if (val != null)
                {
                    try
                    {
                        return (T)context[key];
                    }
                    catch
                    {
                        throw new ArgumentNullException(nameof(key));
                    }
                }
                else
                {
                    throw new ArgumentNullException(nameof(key));
                }
            }
            else
            {
                throw new ArgumentNullException(nameof(key));
            }
        }
    }
}
