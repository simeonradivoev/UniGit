using System;
using System.IO;
using UnityEditor;
using UnityEngine;
#pragma warning disable 618

namespace UniGit
{
	public class GitSettingsManager
	{
		private GitSettingsJson settings;
		private string settingsPath;

		public GitSettingsManager(GitSettingsJson settings,string settingsPath,GitCallbacks callbacks)
		{
			this.settings = settings;
			this.settingsPath = settingsPath;

			callbacks.EditorUpdate += OnEditorUpdate;
		}

		private void OnEditorUpdate()
		{
			if (settings.IsDirty)
			{
				SaveSettingsToFile();
				settings.ResetDirty();
			}
		}

		private void ValidateSettingsPath()
		{
			string settingsFileDirectory = Path.GetDirectoryName(settingsPath);
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
					Debug.LogError("Could not deserialize git settings. Creating new settings.");
					Debug.LogException(e);
				}
			}
		}

		public void LoadOldSettingsFile()
		{
			var oldSettingsFile = EditorGUIUtility.Load("UniGit/Git-Settings.asset") as GitSettings;

			if (oldSettingsFile != null)
			{
				settings.Copy(oldSettingsFile);
				Debug.Log("Old Git Settings transferred to new json settings file. Old settings can now safely be removed.");
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
				Debug.LogError("Could not serialize GitSettingsJson to json file at: " + settingsFilePath);
				Debug.LogException(e);
			}
		}
	}
}