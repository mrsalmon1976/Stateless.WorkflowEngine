using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Test.Stateless.WorkflowEngine.Logging
{
    /// <summary>
    /// A logger factory that records every log entry, for tests.
    /// </summary>
    public class ListLoggerFactory : ILoggerFactory
    {
        private readonly List<LogEntry> _entries = new List<LogEntry>();

        public IReadOnlyList<LogEntry> Entries
        {
            get
            {
                lock (_entries)
                {
                    return _entries.ToList();
                }
            }
        }

        public IEnumerable<LogEntry> WithEventId(int eventId)
        {
            return this.Entries.Where(x => x.EventId.Id == eventId);
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new ListLogger(this, categoryName);
        }

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }

        private void Add(LogEntry entry)
        {
            lock (_entries)
            {
                _entries.Add(entry);
            }
        }

        public class LogEntry
        {
            public string Category { get; set; }

            public LogLevel Level { get; set; }

            public EventId EventId { get; set; }

            public string Message { get; set; }

            public Exception Exception { get; set; }
        }

        private class ListLogger : ILogger
        {
            private readonly ListLoggerFactory _factory;
            private readonly string _category;

            public ListLogger(ListLoggerFactory factory, string category)
            {
                _factory = factory;
                _category = category;
            }

            public IDisposable BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                _factory.Add(new LogEntry
                {
                    Category = _category,
                    Level = logLevel,
                    EventId = eventId,
                    Message = formatter(state, exception),
                    Exception = exception
                });
            }
        }
    }
}
