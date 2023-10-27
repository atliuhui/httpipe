using Serilog;
using System.Diagnostics;

namespace httpipe.Extensions
{
    internal class ActionTimer : IDisposable
    {
        public string name;
        public Stopwatch stopwatch;

        public ActionTimer(string name, bool printStart = false)
        {
            this.name = name;
            this.stopwatch = Stopwatch.StartNew();

            if (printStart)
            {
                Log.Debug($"{DateTime.Now.ToString("s")}, {name} start");
            }
        }

        public void Dispose()
        {
            this.stopwatch.Stop();

            Log.Debug($"{DateTime.Now.ToString("s")}, {name} completed within {this.stopwatch.Elapsed.TotalMilliseconds} ms");
        }
    }
}
