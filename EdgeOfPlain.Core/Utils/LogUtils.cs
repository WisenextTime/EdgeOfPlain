using System;
using System.IO;
using System.Text;
using System.Threading;
using EdgeOfPlain.Core.Framework.IO;
using Microsoft.Extensions.Logging;

namespace EdgeOfPlain.Core.Utils;

public static class LogUtils
{
	public const string CacheFile = "data://logs/latest.log";
	private static ILoggerFactory _factory = LoggerFactory.Create(builder => builder.AddConsole());
	public static ILogger<T> Logger<T>() => _factory.CreateLogger<T>();
	public static ILogger Logger(Type type) => _factory.CreateLogger(type);

	public static void InitFileSystem()
	{
		_factory = LoggerFactory.Create(CreateFactory);
	}

	private static void CreateFactory(ILoggingBuilder builder)
	{
		builder
			.AddConsole()
			.AddProvider(new FileLoggerProvider(CacheFile));
	}
}

public class FileLoggerProvider(string logPath) : ILoggerProvider
{
	private Stream? _fileStream;
	private StreamWriter? _writer;
	private readonly Lock _gate = new();
	private bool _isDisposed;

	public class FileLogger(FileLoggerProvider provider, string categoryName) : ILogger
	{
		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			if (!IsEnabled(logLevel)) return;

			var message = formatter(state, exception);
			if (string.IsNullOrEmpty(message) && exception is null) return;

			var stringBuilder = new StringBuilder();
			stringBuilder.Append('[');
			stringBuilder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
			stringBuilder.Append("] [");
			stringBuilder.Append(logLevel);
			stringBuilder.Append("] [");
			stringBuilder.Append(categoryName);
			stringBuilder.Append("] ");
			stringBuilder.Append(message);
			if (exception is not null)
			{
				stringBuilder.AppendLine();
				stringBuilder.Append(exception);
			}

			provider.WriteLine(stringBuilder.ToString());
		}

		public bool IsEnabled(LogLevel logLevel) => !provider._isDisposed && logLevel != LogLevel.None;

		public IDisposable BeginScope<TState>(TState state) where TState : notnull => EmptyScope.Instance;

		private sealed class EmptyScope : IDisposable
		{
			public static readonly EmptyScope Instance = new();

			public void Dispose() { }
		}
	}

	private void WriteLine(string line)
	{
		lock (_gate)
		{
			if (_isDisposed) return;

			var writer = GetOrCreateWriter();
			if (writer is null) return;

			writer.WriteLine(line);
			writer.Flush();
		}
	}

	private StreamWriter? GetOrCreateWriter()
	{
		if (_writer is not null) return _writer;

		try
		{
			_fileStream ??= FileIO.OpenWrite(logPath);
			_writer = new StreamWriter(_fileStream, leaveOpen: true);
		}
		catch
		{
			return null;
		}

		return _writer;
	}

	public void Dispose()
	{
		lock (_gate)
		{
			if (_isDisposed) return;
			_isDisposed = true;
			_writer?.Dispose();
			_writer = null;
			_fileStream?.Dispose();
			_fileStream = null;
		}
		GC.SuppressFinalize(this);
	}

	public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);
}