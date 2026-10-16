using System;
using System.IO;
using System.Text;

namespace LOMAssistant;

public static class FileLogger
{
	private static readonly object _lock = new object();

	private static string _logFilePath;

	private static bool _initialized;

	public static void Init(string pluginDirectory)
	{
		if (_initialized)
		{
			return;
		}
		try
		{
			string text = Path.Combine(pluginDirectory, "logs");
			if (!Directory.Exists(text))
			{
				Directory.CreateDirectory(text);
			}
			string path = "LOMAssistant_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log";
			_logFilePath = Path.Combine(text, path);
			_initialized = true;
			Log("活侠传助手日志系统已初始化。");
		}
		catch (Exception ex)
		{
			_initialized = false;
			Console.WriteLine("[LOMAssistant] 初始化日志文件失败: " + ex);
		}
	}

	public static void Log(object message)
	{
		Write("INFO", message);
	}

	public static void LogError(object message)
	{
		Write("ERROR", message);
	}

	public static void LogWarning(object message)
	{
		Write("WARN", message);
	}

	private static void Write(string level, object message)
	{
		if (!_initialized || string.IsNullOrEmpty(_logFilePath))
		{
			return;
		}
		try
		{
			string text = string.Format("[{0}] [{1}] {2}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), level, message);
			lock (_lock)
			{
				File.AppendAllText(_logFilePath, text + Environment.NewLine, Encoding.UTF8);
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("[LOMAssistant] 写入日志失败: " + ex);
		}
	}
}
