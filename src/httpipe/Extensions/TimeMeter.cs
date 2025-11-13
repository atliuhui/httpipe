using Serilog;
using System.Diagnostics;

namespace Httpipe.Extensions
{
    internal class TimeMeter : IDisposable
    {
        readonly string name;
        readonly string session;
        readonly Stopwatch stopwatch;
        int counter;

        string tag => $"{this.name}@{this.session}";

        public TimeMeter(string name)
        {
            this.name = name;
            this.session = DateTimeOffset.Now.ToUnixTimeSeconds().ToString("x");
            this.stopwatch = Stopwatch.StartNew();
            this.counter = 1;

            Log.Debug($"{tag} {this.stopwatch.Elapsed} start");
        }
        public void Dispose()
        {
            this.stopwatch.Stop();
            Log.Debug($"{tag} {this.stopwatch.Elapsed} completed in {this.stopwatch.Elapsed.TotalSeconds} s");
        }

        public void Checkpoint(string note = "")
        {
            if (string.IsNullOrEmpty(note))
            {
                Log.Debug($"{tag} {this.stopwatch.Elapsed} [{this.counter}]");
            }
            else
            {
                Log.Debug($"{tag} {this.stopwatch.Elapsed} {note}");
            }

            this.counter = this.counter + 1;
        }
    }
}
