using System;
using System.IO;
using System.Text;
using System.Threading;
using EdgeOfPlain.Core.Framework.IO;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;

namespace EdgeOfPlain.Core.Utils;

public static class LogUtils
{
	public const string CacheFile = "data://logs/latest.log";
	private static ILoggerFactory Factory => field ??= LoggerFactory.Create(CreateFactory);
	public static ILogger<T> Logger<T>() => Factory.CreateLogger<T>();
	public static ILogger Logger(Type type) => Factory.CreateLogger(type);

	public static void CreateFactory(ILoggingBuilder builder)
	{
		builder
			.AddConsole()
			.AddProvider(new FileLoggerProvider(CacheFile));
	}
}

public class FileLoggerProvider(string logPath) : ILoggerProvider
{
	private Stream? _fileStream;
	private bool _isDisposed;

	public class FileLogger(Func<bool> enable, Func<Stream?> getStream) : ILogger
	{
		private StreamWriter? _writer;
		private readonly Lock _gate = new();

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			var stream = getStream();
			if (stream is null) return;

			var stringBuilder = new StringBuilder();
			stringBuilder.Append('[');
			stringBuilder.Append(DateAndTime.Now.ToString("yyyy-MM-dd HH-mm-ss"));
			stringBuilder.Append(']');
			stringBuilder.Append(formatter(state, exception));

			lock (_gate)
			{
				_writer ??= new StreamWriter(stream, leaveOpen: true);
				_writer.WriteLine(stringBuilder);
				_writer.Flush();
			}
		}

		public bool IsEnabled(LogLevel logLevel) => enable();

		public IDisposable BeginScope<TState>(TState state) where TState : notnull => EmptyScope.Instance;

		private sealed class EmptyScope : IDisposable
		{
			public static readonly EmptyScope Instance = new();

			public void Dispose() { }
		}
	}

	private Stream? OpenStream()
	{
		if (_fileStream is not null) return _fileStream;

		try
		{
			_fileStream = FileIO.OpenWrite(logPath);
		}
		catch
		{
			return null;
		}

		return _fileStream;
	}

	public void Dispose()
	{
		_isDisposed = true;
		_fileStream?.Dispose();
		GC.SuppressFinalize(this);
	}

	public ILogger CreateLogger(string categoryName) => new FileLogger(() => !_isDisposed, OpenStream);
}