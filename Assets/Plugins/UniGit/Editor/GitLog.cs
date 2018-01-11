using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UniGit
{
	public class GitLog : ILogHandler, IEnumerable<GitLog.LogEntry>, IDisposable
	{
		private readonly UniGitData data;
		private readonly GitSettingsJson gitSettings;
		private readonly GitCallbacks gitCallbacks;
		private readonly string logPath;
		private Regex logTypeRegex;
		private Regex lineAndNumberRegex;

		[UniGitInject]
		public GitLog(string logPath,UniGitData data,GitSettingsJson gitSettings,GitCallbacks gitCallbacks)
		{
			this.logPath = logPath;
			this.data = data;
			this.gitSettings = gitSettings;
			this.gitCallbacks = gitCallbacks;
			logTypeRegex = new Regex(@"\[.*?\]",RegexOptions.Compiled);
			lineAndNumberRegex = new Regex(@"\(at((.*?):(.*?))\)",RegexOptions.Compiled);

			if (data.LogInitialized)
			{
				data.LogInitialized = true;
				data.LogEntries.Clear();
				LoadLines();
			}
		}

		public void LogFormat(LogType logType, Object context, string format, params object[] args)
		{
			if (gitSettings.UseUnityConsole)
			{
				Debug.unityLogger.LogFormat(logType,context,format,args);
				return;
			}
			var entry = new LogEntry(logType, string.Format(format, args),DateTime.Now,StackTraceUtility.ExtractStackTrace());
			data.LogEntries.Add(entry);
			using (StreamWriter streamWriter = File.AppendText(logPath))
			{
				streamWriter.WriteLine(FormatWithLogType(logType,format),args);
				streamWriter.WriteLine(entry.StackTrace);
			}
			gitCallbacks.IssueLogEntry(entry);
		}

		public void LogException(Exception exception, Object context)
		{
			if (gitSettings.UseUnityConsole)
			{
				Debug.unityLogger.LogException(exception,context);
				return;
			}
			var entry = new LogEntry(LogType.Exception, exception.Message,DateTime.Now,StackTraceUtility.ExtractStringFromException(exception));
			data.LogEntries.Add(entry);
			using (StreamWriter streamWriter = File.AppendText(logPath))
			{
				streamWriter.WriteLine(FormatWithLogType(LogType.Exception,exception.Message));
			}
			gitCallbacks.IssueLogEntry(entry);
		}

		private string FormatWithLogType(LogType logType, string text)
		{
			return string.Format("{0} [{1}] {2}", DateTime.Now, logType, text);
		}

		private void LoadLines()
		{
			StringBuilder stringBuilder = null;

			using(var logFileStream = File.Open(logPath, FileMode.OpenOrCreate))
			using (StreamReader fileReader = new StreamReader(logFileStream))
			{
				LogEntry? currentEntry = null;

				while (!fileReader.EndOfStream)
				{
					string currentLine = fileReader.ReadLine();
					if (string.IsNullOrEmpty(currentLine) && currentEntry.HasValue)
					{
						var entryValue = currentEntry.Value;
						currentEntry = null;

						if (stringBuilder != null)
						{
							entryValue.StackTrace = stringBuilder.ToString();
							stringBuilder = null;
						}
						data.LogEntries.Add(entryValue);
					}
					else if (!currentEntry.HasValue && !string.IsNullOrEmpty(currentLine))
					{
						var typeMatch = logTypeRegex.Match(currentLine);
						string typeStr = typeMatch.Value;
						LogType type = (LogType)Enum.Parse(typeof(LogType),typeStr.Substring(1,typeStr.Length-2));
						string timeString = currentLine.Substring(0, typeMatch.Index);
						DateTime time = DateTime.Parse(timeString);
						string message = currentLine.Substring(typeMatch.Index + typeMatch.Length, currentLine.Length - (typeMatch.Index + typeMatch.Length)).TrimStart(' ');
						currentEntry = new LogEntry(type,message,time,"");
					}
					else if (currentEntry.HasValue && !string.IsNullOrEmpty(currentLine))
					{
						if(stringBuilder == null) stringBuilder = new StringBuilder();
						stringBuilder.AppendLine(currentLine);
					}
				}
			}
		}

		public bool CanOpenLine(string stackTrace)
		{
			var match = lineAndNumberRegex.Match(stackTrace);
			if (match.Success)
			{
				var path = match.Groups[2].Value.Replace(Path.DirectorySeparatorChar,UniGitPath.UnityDeirectorySeparatorChar).Trim();
				return GitManager.IsPathInAssetFolder(path) && !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path));
			}
			return false;
		}

		public bool OpenLine(string stackTrace)
		{
			var match = lineAndNumberRegex.Match(stackTrace);
			if (match.Success)
			{
				var path = match.Groups[2].Value.Replace(Path.DirectorySeparatorChar,UniGitPath.UnityDeirectorySeparatorChar).Trim();
				int line;
				if (int.TryParse(match.Groups[3].Value,out line))
				{
					var scriptAsset = AssetDatabase.LoadMainAssetAtPath(path);
					if (scriptAsset != null)
					{
						return AssetDatabase.OpenAsset(scriptAsset,line);
					}
				}
			}
			return false;
		}

		public void OpenLine(string stackTrace,int offset)
		{
			string[] lines = stackTrace.Split('\n');
			for (int i = offset; i < lines.Length; i++)
			{
				if (OpenLine(lines[i]))
				{
					return;
				}
			}
		}

		public void Clear()
		{
			data.LogEntries.Clear();
			File.WriteAllText(logPath,"");
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public IEnumerator<LogEntry> GetEnumerator()
		{
			return data.LogEntries.GetEnumerator();
		}

		public LogEntry this[int index]
		{
			get { return data.LogEntries[index]; }
		}

		public int Count
		{
			get { return data.LogEntries.Count; }
		}

		public void Dispose()
		{
			
		}

		[Serializable]
		public struct LogEntry
		{
			[SerializeField] private LogType logType;
			[SerializeField] private string message;
			[SerializeField] private long time;
			[SerializeField] private string stackTrace;

			public LogEntry(LogType logType, string message,DateTime time,string stackTrace)
			{
				this.logType = logType;
				this.message = message;
				this.time = time.Ticks;
				this.stackTrace = stackTrace;
			}

			public string StackTrace
			{
				get { return stackTrace; }
				internal set { stackTrace = value; }
			}

			public long Time
			{
				get { return time; }
			}

			public LogType LogType
			{
				get { return logType; }
			}

			public string Message
			{
				get { return message; }
			}

		}
	}
}
