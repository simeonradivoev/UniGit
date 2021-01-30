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
		private readonly UniGitPaths paths;
		private readonly UniGitData data;
		private readonly GitSettingsJson gitSettings;
		private readonly GitCallbacks gitCallbacks;
		private Regex logTypeRegex;
		private Regex lineAndNumberRegex;

		[UniGitInject]
		public GitLog(UniGitPaths paths,UniGitData data,GitSettingsJson gitSettings,GitCallbacks gitCallbacks)
		{
			this.paths = paths;
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
			using (var streamWriter = File.AppendText(paths.LogsFilePath))
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
			using (var streamWriter = File.AppendText(paths.LogsFilePath))
			{
				streamWriter.WriteLine(FormatWithLogType(LogType.Exception,exception.Message));
			}
			gitCallbacks.IssueLogEntry(entry);
		}

		private string FormatWithLogType(LogType logType, string text)
		{
			return $"{DateTime.Now} [{logType}] {text}";
		}

		private void LoadLines()
		{
			StringBuilder stringBuilder = null;

            using var logFileStream = File.Open(paths.LogsFilePath, FileMode.OpenOrCreate);
            using var fileReader = new StreamReader(logFileStream);
            LogEntry? currentEntry = null;

            while (!fileReader.EndOfStream)
            {
                var currentLine = fileReader.ReadLine();
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
                    var typeStr = typeMatch.Value;
                    var type = (LogType)Enum.Parse(typeof(LogType),typeStr.Substring(1,typeStr.Length-2));
                    var timeString = currentLine.Substring(0, typeMatch.Index);
                    var time = DateTime.Parse(timeString);
                    var message = currentLine.Substring(typeMatch.Index + typeMatch.Length, currentLine.Length - (typeMatch.Index + typeMatch.Length)).TrimStart(' ');
                    currentEntry = new LogEntry(type,message,time,"");
                }
                else if (currentEntry.HasValue && !string.IsNullOrEmpty(currentLine))
                {
                    stringBuilder ??= new StringBuilder();
                    stringBuilder.AppendLine(currentLine);
                }
            }
        }

		public bool CanOpenLine(string stackTrace)
		{
			var match = lineAndNumberRegex.Match(stackTrace);
			if (match.Success)
			{
				var path = match.Groups[2].Value.Replace(Path.DirectorySeparatorChar,UniGitPathHelper.UnityDeirectorySeparatorChar).Trim();
				return UniGitPathHelper.IsPathInAssetFolder(path) && !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path));
			}
			return false;
		}

		public bool OpenLine(string stackTrace)
		{
			var match = lineAndNumberRegex.Match(stackTrace);
            if (!match.Success) return false;
            var path = match.Groups[2].Value.Replace(Path.DirectorySeparatorChar,UniGitPathHelper.UnityDeirectorySeparatorChar).Trim();
            if (!int.TryParse(match.Groups[3].Value, out var line)) return false;
            var scriptAsset = AssetDatabase.LoadMainAssetAtPath(path);
            return scriptAsset != null && AssetDatabase.OpenAsset(scriptAsset,line);
        }

		public void OpenLine(string stackTrace,int offset)
		{
			var lines = stackTrace.Split('\n');
			for (var i = offset; i < lines.Length; i++)
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
			File.WriteAllText(paths.LogsFilePath,"");
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public IEnumerator<LogEntry> GetEnumerator()
		{
			return data.LogEntries.GetEnumerator();
		}

		public LogEntry this[int index] => data.LogEntries[index];

        public int Count => data.LogEntries.Count;

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
				get => stackTrace;
                internal set => stackTrace = value;
            }

			public long Time => time;

            public LogType LogType => logType;

            public string Message => message;
        }
	}
}
