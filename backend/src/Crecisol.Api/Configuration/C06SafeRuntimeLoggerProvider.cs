using Microsoft.Extensions.Logging;

namespace Crecisol.Api.Configuration;

/// <summary>Only bounded diagnostic metadata may cross the process log boundary.
/// Never render messages, state, scopes, exception text, arbitrary categories,
/// request paths/queries, headers, connection strings, identities or payloads.
/// Durable authorization evidence is independently persisted in MySQL.</summary>
public sealed class C06SafeRuntimeLoggerProvider(TextWriter output) : ILoggerProvider
{
    private readonly object gate = new();
    public const string OutcomeCategory = "Crecisol.HttpOutcome";

    public ILogger CreateLogger(string categoryName)
    {
        var kind = categoryName switch
        {
            OutcomeCategory => "http",
            _ when categoryName.StartsWith("Microsoft.Hosting.", StringComparison.Ordinal) => "host",
            _ when categoryName.StartsWith("Microsoft.AspNetCore.", StringComparison.Ordinal) => "framework",
            _ when categoryName.StartsWith("MySqlConnector", StringComparison.Ordinal) => "database",
            _ => "application"
        };
        return new SafeLogger(this, kind);
    }

    private void Write(string kind, LogLevel level, int eventCode)
    {
        lock (gate)
            output.WriteLine(System.FormattableString.Invariant($"CRECISOL_RUNTIME source={kind} level={(int)level} event={eventCode}"));
    }

    public void Dispose() { /* The host owns the writer. */ }

    private sealed class SafeLogger(C06SafeRuntimeLoggerProvider owner, string kind) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => EmptyScope.Instance;
        public bool IsEnabled(LogLevel logLevel) =>
            logLevel is >= LogLevel.Warning and <= LogLevel.Critical ||
            (kind == "http" && logLevel == LogLevel.Information);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel)) owner.Write(kind, logLevel, eventId.Id);
        }
    }

    private sealed class EmptyScope : IDisposable
    {
        internal static readonly EmptyScope Instance = new();
        public void Dispose() { }
    }
}
