using Microsoft.Extensions.Logging;
using System;

namespace JsonViewer.Services;

/// <summary>
/// 简单的控制台日志记录器
/// </summary>
public class ConsoleLogger<T> : ILogger<T>
{
    public IDisposable BeginScope<TState>(TState state) => null!;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        Console.WriteLine($"[{logLevel}] {typeof(T).Name}: {message}");
        if (exception != null)
        {
            Console.WriteLine(exception.ToString());
        }
    }
}