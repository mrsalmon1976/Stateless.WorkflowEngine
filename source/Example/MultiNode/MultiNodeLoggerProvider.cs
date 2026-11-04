using Microsoft.Extensions.Logging;

namespace Example.MultiNode
{
    /// <summary>
    /// Writes log entries through <see cref="MultiNodeContext.Log"/>, so the workflow engine's logs go to the console and
    /// the node's log file alongside the node's own messages.
    /// </summary>
    internal class MultiNodeLoggerProvider : ILoggerProvider
    {
        private readonly MultiNodeContext _context;

        public MultiNodeLoggerProvider(MultiNodeContext context)
        {
            _context = context;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new MultiNodeLogger(_context);
        }

        public void Dispose()
        {
        }

        private class MultiNodeLogger : ILogger
        {
            private readonly MultiNodeContext _context;

            public MultiNodeLogger(MultiNodeContext context)
            {
                _context = context;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return logLevel != LogLevel.None;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (!this.IsEnabled(logLevel))
                {
                    return;
                }

                string message = formatter(state, exception);
                if (exception != null)
                {
                    message += $": {exception.Message}";
                }
                _context.Log($"[{logLevel}] {message}", GetColor(logLevel));
            }

            private static ConsoleColor? GetColor(LogLevel logLevel)
            {
                return logLevel switch
                {
                    LogLevel.Critical or LogLevel.Error => ConsoleColor.Red,
                    LogLevel.Warning => ConsoleColor.Yellow,
                    _ => null
                };
            }
        }
    }
}
