using System.Diagnostics;
using System.Net;
using System.Text;

namespace Mercurius.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly string _logDirectory;
        private readonly StreamWriter _logWriter;
        private readonly Timer _flushTimer;
        private bool _disposed;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IWebHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _logDirectory = Path.Combine(env.ContentRootPath, "logs");
            
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            var logFile = Path.Combine(_logDirectory, $"mercurius_{DateTime.UtcNow:yyyyMMdd}.log");
            _logWriter = new StreamWriter(logFile, append: true) { AutoFlush = false };
            
            // Flush every 5 seconds
            _flushTimer = new Timer(_ => FlushAsync().ConfigureAwait(false).GetAwaiter().GetResult(), 
                null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                _logger.LogTrace($"Request: {context.Request.Method} {context.Request.Path}");
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var requestPath = context.Request.Path;
            var requestMethod = context.Request.Method;
            var user = context.User?.Identity?.Name ?? "Anonymous";
            
            var errorMessage = $@"========================================
UNHANDLED EXCEPTION
========================================
Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} UTC
Request: {requestMethod} {requestPath}
User: {user}
UserAgent: {context.Request.Headers["User-Agent"].ToString()}
IP: {context.Connection.RemoteIpAddress}
Exception: {exception.GetType().Name}: {exception.Message}
StackTrace: {exception.StackTrace}
========================================";

            _logger.LogCritical(errorMessage);

            // Write to file with fallback
            await WriteToFileAsync(errorMessage);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            // For API/AJAX requests, return JSON
            if (context.Request.Path.StartsWithSegments("/api") || 
                context.Request.Headers["Accept"].ToString().Contains("application/json") ||
                context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var response = new
                {
                    StatusCode = context.Response.StatusCode,
                    Message = "An unexpected error occurred. Please check the logs for details.",
                    RequestId = context.TraceIdentifier
                };
                
                await context.Response.WriteAsJsonAsync(response);
                return;
            }

            // For regular requests, redirect to error page
            context.Response.Redirect($"/Home/Error?requestId={context.TraceIdentifier}");
        }

        private async Task WriteToFileAsync(string message)
        {
            try
            {
                await _semaphore.WaitAsync();
                try
                {
                    await _logWriter.WriteAsync(message + "\n\n");
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (Exception ex)
            {
                // Fallback to Event Log
                FallbackLog("CRITICAL", message + $"\nFile write failed: {ex.Message}");
            }
        }

        private void FallbackLog(string level, string message)
        {
            try
            {
#if WINDOWS
                var source = "Mercurius";
                var logName = "Application";
                
                if (!EventLog.SourceExists(source))
                {
                    EventLog.CreateEventSource(source, logName);
                }
                
                EventLog.WriteEntry(source, message, EventLogEntryType.Error);
#else
                System.Diagnostics.Debug.WriteLine($"[{level}] {message}");
#endif
            }
            catch
            {
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
                FallbackLog("WARN", $"Failed to flush exception log: {ex.Message}");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            _flushTimer.Dispose();
            _semaphore.Wait();
            try
            {
                _logWriter.Flush();
                _logWriter.Dispose();
            }
            finally
            {
                _semaphore.Dispose();
            }
        }
    }

    public static class ExceptionHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ExceptionHandlingMiddleware>();
        }
    }
}
