using System;

namespace EdgeOfPlain.Core.Utils;

public static class LogUtils
{
	public enum LogType { Info, Warning, Error, Tip }

	public static void LogMessage(string message, LogType type = LogType.Info)
	{
		Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{type.ToString()}] {message}"); 
	}
}
