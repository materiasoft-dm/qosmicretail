using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Text;

namespace Mercurius.Services
{
    public class LoggerService : ILoggerService, IAsyncDisposable
    {
        private readonly ILogger<LoggerService> _logger;
        private readonly string _logDirectory;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly StreamWriter _logWriter;
        private readonly string _currentLogFile;
        private readonly Timer _flushTimer;
        private bool _disposed;

        public LoggerService(ILogger<LoggerService> logger, IWebHostEnvironment env)
        {
            _logger = logger;

            // Prefer a directory *outside* OneDrive. When the log file lives under
            // %USERPROFILE%\OneDrive - MSFT\..., OneDrive's metadata scans briefly
            // hold an exclusive lock and the second concurrent LoggerService ctor
            // (AddScoped in Program.cs) throws IOException, which the middleware
            // fails to log, which 500's /Products and friends. Logs in
            // %LOCALAPPDATA%\Mercurius\logs default; override via MERCURIUS_LOG_DIR.
            var overrideDir = Environment.GetEnvironmentVariable("MERCURIUS_LOG_DIR");
            _logDirectory = !string.IsNullOrWhiteSpace(overrideDir)
                ? overrideDir!
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Mercurius", "logs");

            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            _currentLogFile = Path.Combine(_logDirectory, $"mercurius_{DateTime.UtcNow:yyyyMMdd}.log");
            // FileMode.Append on Windows forces FileShare.None and ignores any FileShare
            // value set on the FileStream, which made concurrent LoggerService instances
            // (AddScoped in Program.cs) collide on the ctor and 500 every request.
            // Switch to OpenOrCreate + manual seek-to-end so FileShare.ReadWrite takes effect.
            var fileStream = new FileStream(
                _currentLogFile,
                FileMode.OpenOrCreate,
                FileAccess.Write,
                FileShare.ReadWrite);
            fileStream.Seek(0, SeekOrigin.End);
            _logWriter = new StreamWriter(fileStream, Encoding.UTF8) { AutoFlush = false };
            
            // Flush every 5 seconds to balance performance and data safety
            _flushTimer = new Timer(_ => FlushAsync().ConfigureAwait(false).GetAwaiter().GetResult(), 
                null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        public void LogInformation(string message) => WriteLog("INFO", message);
        public void LogWarning(string message) => WriteLog("WARN", message);
        public void LogError(string message, Exception? exception = null) => WriteLog("ERROR", message, exception);
        public void LogDebug(string message) => WriteLog("DEBUG", message);
        public void LogCritical(string message, Exception? exception = null) => WriteLog("CRITICAL", message, exception);
        public void LogTrace(string message) => WriteLog("TRACE", message);

        private void WriteLog(string level, string message, Exception? exception = null)
        {
            _logger.Log(level == "ERROR" || level == "CRITICAL" ? LogLevel.Error : 
                        level == "WARN" ? LogLevel.Warning : LogLevel.Information, message);

            try
            {
                var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logEntry = $"[{timestamp}] [{level}] {message}";
                
                if (exception != null)
                {
                    logEntry += $"\nException: {exception.GetType().Name}: {exception.Message}";
                    logEntry += $"\nStackTrace: {exception.StackTrace}";
                    
                    if (exception.InnerException != null)
                    {
                        logEntry += $"\nInnerException: {exception.InnerException.GetType().Name}: {exception.InnerException.Message}";
                    }
                }
                
                logEntry += "\n" + new string('-', 80) + "\n";
                
                _semaphore.Wait();
                try
                {
                    _logWriter.Write(logEntry);
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (Exception ex)
            {
                // Fallback to Event Log if file logging fails
                FallbackLog(level, message, ex);
            }
        }

        private void FallbackLog(string level, string message, Exception? fileEx = null)
        {
            try
            {
                var fullMessage = $"[{level}] {message}";
                if (fileEx != null)
                {
                    fullMessage += $"\nFile logging failed: {fileEx.Message}";
                }

#if WINDOWS
                var source = "Mercurius";
                var logName = "Application";
                
                if (!EventLog.SourceExists(source))
                {
                    EventLog.CreateEventSource(source, logName);
                }
                
                EventLog.WriteEntry(source, fullMessage, 
                    level == "ERROR" || level == "CRITICAL" ? EventLogEntryType.Error : EventLogEntryType.Warning);
#else
                System.Diagnostics.Debug.WriteLine(fullMessage);
#endif
            }
            catch
            {
                // Last resort: write to debug output
                System.Diagnostics.Debug.WriteLine($"[{level}] {message}");
            }
        }

        private async Task FlushAsync()
        {
            if (_disposed) return;
            
            await _semaphore.WaitAsync();
            try
            {
                await _logWriter.FlushAsync();
            }
            catch (Exception ex)
            {
                FallbackLog("WARN", $"Failed to flush log: {ex.Message}");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            
            _flushTimer.Dispose();
            await _semaphore.WaitAsync();
            try
            {
                await _logWriter.FlushAsync();
                _logWriter.Dispose();
            }
            finally
            {
                _semaphore.Dispose();
            }
        }
    }
}
