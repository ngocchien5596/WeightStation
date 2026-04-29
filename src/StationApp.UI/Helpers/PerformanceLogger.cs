using System;
using System.IO;
using System.Threading;
using System.Text.Json;

namespace StationApp.UI.Helpers;

public static class PerformanceLogger
{
    private static readonly string LogFilePath;
    private static readonly object LockObj = new object();

    static PerformanceLogger()
    {
        var appData = AppDomain.CurrentDomain.BaseDirectory;
        var logDir = Path.Combine(appData, "logs");
        if (!Directory.Exists(logDir))
        {
            Directory.CreateDirectory(logDir);
        }
        LogFilePath = Path.Combine(logDir, "perf_metrics.jsonl");
    }

    public static IDisposable Track(string operationName)
    {
        return new PerformanceTrackerScope(operationName);
    }

    private class PerformanceTrackerScope : IDisposable
    {
        private readonly string _operationName;
        private readonly DateTime _startTime;
        private readonly int _threadId;

        public PerformanceTrackerScope(string operationName)
        {
            _operationName = operationName;
            _startTime = DateTime.UtcNow;
            _threadId = Environment.CurrentManagedThreadId;
        }

        public void Dispose()
        {
            var duration = (DateTime.UtcNow - _startTime).TotalMilliseconds;
            Log(_operationName, duration, _threadId);
        }
    }

    public static void Log(string operationName, double durationMs, int? threadId = null)
    {
        var entry = new
        {
            Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            MachineName = Environment.MachineName,
            ThreadId = threadId ?? Environment.CurrentManagedThreadId,
            Operation = operationName,
            DurationMs = Math.Round(durationMs, 2)
        };

        try
        {
            var json = JsonSerializer.Serialize(entry);
            lock (LockObj)
            {
                File.AppendAllText(LogFilePath, json + Environment.NewLine);
            }
        }
        catch
        {
            // Fail silently to avoid crashing UI
        }
    }
}
