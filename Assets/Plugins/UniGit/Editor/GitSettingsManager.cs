using System;
using System.IO;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;
#pragma warning disable 618   //disables warrnings for the use of depricated GitSettings

namespace UniGit
{
	public class GitSettingsManager : IDisposable
	{
		private readonly GitSettingsJson settings;
		private readonly GitCallbacks gitCallbacks;
		private readonly string settingsPath;
		private readonly ILogger logger;

		[UniGitInject]
		public GitSettingsManager(GitSettingsJson settings,string settingsPath,GitCallbacks gitCallbacks,ILogger logger)
		{
			this.settings = settings;
			this.settingsPath = settingsPath;
			this.gitCallbacks = gitCallbacks;
			this.logger = logger;

			gitCallbacks.EditorUpdate += OnEditorUpdate;
		}

		private void OnEditorUpdate()
		{
			if (settings.IsDirty)
			{
				SaveSettingsToFile();
				gitCallbacks.IssueSettingsChange();
				settings.ResetDirty();
			}
		}

		private void ValidateSettingsPath()
		{
			string settingsFileDirectory = Path.GetDirectoryName(settingsPath);
			if(string.IsNullOrEmpty(settingsFileDirectory)) return;
			if (!Directory.Exists(settingsFileDirectory))
			{
				Directory.CreateDirectory(settingsFileDirectory);
			}
		}


		public void LoadGitSettings()
		{
			string settingsFilePath = settingsPath;
			if (File.Exists(settingsFilePath))
			{
				try
				{
					JsonUtility.FromJsonOverwrite(File.ReadAllText(settingsFilePath), settings);
				}
				catch (Exception e)
				{
					logger.Log(LogType.Error,"Could not deserialize git settings. Creating new settings.");
					logger.LogException(e);
				}
			}
		}

		public void LoadOldSettingsFile()
		{
			var oldSettingsFile = EditorGUIUtility.Load("UniGit/Git-Settings.asset") as GitSettings;

			if (oldSettingsFile != null)
			{
				settings.Copy(oldSettingsFile);
				logger.Log(LogType.Log,"Old Git Settings transferred to new json settings file. Old settings can now safely be removed.");
			}
			SaveSettingsToFile();
		}

		public void SaveSettingsToFile()
		{
			ValidateSettingsPath();
			string settingsFilePath = settingsPath;

			try
			{
				string json = JsonUtility.ToJson(settings);
				File.WriteAllText(settingsFilePath, json);
			}
			catch (Exception e)
			{
				logger.LogFormat(LogType.Error,"Could not serialize GitSettingsJson to json file at: {0}",settingsFilePath);
				logger.LogException(e);
			}
		}

		public void Dispose()
		{
			if (gitCallbacks != null) gitCallbacks.EditorUpdate -= OnEditorUpdate;
		}
	}
}