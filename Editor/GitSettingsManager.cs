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
		private readonly ILogger logger;
		private readonly GitInitializer initializer;
		private readonly UniGitPaths paths;

		[UniGitInject]
		public GitSettingsManager(UniGitPaths paths, GitSettingsJson settings,GitCallbacks gitCallbacks,ILogger logger,GitInitializer initializer)
		{
			this.paths = paths;
			this.settings = settings;
			this.gitCallbacks = gitCallbacks;
			this.logger = logger;
			this.initializer = initializer;

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
			string settingsFileDirectory = paths.SettingsFolderPath;
			if(string.IsNullOrEmpty(settingsFileDirectory)) return;
			if (!Directory.Exists(settingsFileDirectory))
			{
				Directory.CreateDirectory(settingsFileDirectory);
			}
		}


		public void LoadGitSettings()
		{
			string settingsFilePath = paths.SettingsFilePath;
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

				SaveSettingsToFile();
            }
		}

		public void ShowChooseMainRepositoryPathPopup(EditorWindow context = null)
		{
			var rootProjectPath = paths.ProjectPath;

            var repoPath = EditorUtility.OpenFolderPanel("Repository Path", rootProjectPath, "");
			if (string.IsNullOrEmpty(repoPath))
			{
				return;
			}

			bool isRootPath = UniGitPathHelper.PathsEqual(repoPath, rootProjectPath);
			bool isChildOfRoot = UniGitPathHelper.IsSubDirectoryOf(repoPath, rootProjectPath);

            if (isRootPath || isChildOfRoot)
			{
				if (isRootPath)
				{
					EditorPrefs.DeleteKey(UniGitLoader.RepoPathKey);
				}
				else
				{
					string localPath = repoPath.Replace(rootProjectPath + UniGitPathHelper.UnityDeirectorySeparatorChar, "");
					EditorPrefs.SetString(UniGitLoader.RepoPathKey, localPath);
				}

				paths.SetRepoPath(repoPath);
				initializer.RecompileSoft();
            }
			else if(context)
			{
				context.ShowNotification(new GUIContent("Invalid Path !"));
			}
		}

		public void SaveSettingsToFile()
		{
			if(!initializer.IsValidRepo) return;

			ValidateSettingsPath();
			string settingsFilePath = paths.SettingsFilePath;

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