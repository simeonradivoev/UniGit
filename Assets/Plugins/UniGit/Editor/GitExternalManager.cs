using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using LibGit2Sharp;
using UniGit.Adapters;
using UniGit.Attributes;
using UniGit.Utils;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace UniGit
{
	public class GitExternalManager
	{
		private IExternalAdapter[] adapters;
		private GUIContent[] adapterNames;
		private int selectedAdapterIndex = -1;
		private IExternalAdapter selectedAdapter;
		private bool initiazlitedSelected;
		private GitManager gitManager;

		[UniGitInject]
		public GitExternalManager(GitManager gitManager)
		{
			var injectionHelper = new InjectionHelper();
			injectionHelper.Bind<GitManager>().FromInstance(gitManager);
			injectionHelper.Bind<GitExternalManager>().FromInstance(this);
			this.gitManager = gitManager;
			var adaptorTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes().Where(t => typeof(IExternalAdapter).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)).ToArray();
			adapters = new IExternalAdapter[adaptorTypes.Length];
			for (int i = 0; i < adaptorTypes.Length; i++)
			{
				adapters[i] = (IExternalAdapter)injectionHelper.CreateInstance(adaptorTypes[i]);
			}
			Array.Sort(adapters, (l, r) =>
			{
				return GetAdapterPriority(l).CompareTo(GetAdapterPriority(r));
			});
			adapterNames = adapters.Select(a => new GUIContent(GetAdapterName(a))).ToArray();
		}

		#region Selection
		//Using lazy initialization
		private IExternalAdapter SelectedAdatapter
		{
			get
			{
				if (!initiazlitedSelected)
				{
					InitializeSelectedAdapter();
				}
				return selectedAdapter;
			}
		}

		public int SelectedAdapterIndex
		{
			get
			{
				if (!initiazlitedSelected)
				{
					InitializeSelectedAdapter();
				}
				return selectedAdapterIndex; 
			} 
		}

		private void InitializeSelectedAdapter()
		{
			selectedAdapter = adapters.FirstOrDefault(a => Exists(a) && GetAdapterName(a) == gitManager.Settings.ExternalProgram) ?? adapters.FirstOrDefault(a => Exists(a));
			if (selectedAdapter != null) selectedAdapterIndex = Array.IndexOf(adapters, selectedAdapter);
			initiazlitedSelected = true;
		}

		public void SetSelectedAdapter(int index)
		{
			if(index >= adapters.Length && index < 0 && selectedAdapterIndex == index) return;
			selectedAdapterIndex = index;
			selectedAdapter = adapters[index];
		}

		#endregion

		private ExternalAdapterAttribute GetAdapterAttribute(IExternalAdapter adapter)
		{
			return adapter.GetType().GetCustomAttributes(typeof(ExternalAdapterAttribute), false).FirstOrDefault() as ExternalAdapterAttribute;
		}

		private string GetAdapterName(IExternalAdapter adapter)
		{
			ExternalAdapterAttribute attribute = GetAdapterAttribute(adapter);
			if (attribute == null) return null;
			return attribute.FriendlyName;
		}

		private int GetAdapterPriority(IExternalAdapter adapter)
		{
			ExternalAdapterAttribute attribute = GetAdapterAttribute(adapter);
			if (attribute == null) return 0;
			return attribute.Priority;
		}

		private bool Exists(IExternalAdapter adapterInfo)
		{
			ExternalAdapterAttribute attribute = GetAdapterAttribute(adapterInfo);
			if (attribute == null) return false;
			return attribute.ProcessNames.All(p => ExistsOnPath(p));
		}

		public bool TakeCommit(string message)
		{
			if (!gitManager.Settings.ExternalsType.HasFlag(GitSettingsJson.ExternalsTypeEnum.Commit) || SelectedAdatapter == null) return false;
			return SelectedAdatapter.Commit(message);
		}

		public bool TakePush()
		{
			if (!gitManager.Settings.ExternalsType.HasFlag(GitSettingsJson.ExternalsTypeEnum.Push) || SelectedAdatapter == null) return false;
			return SelectedAdatapter.Push();
		}

		public bool TakePull()
		{
			if (!gitManager.Settings.ExternalsType.HasFlag(GitSettingsJson.ExternalsTypeEnum.Pull) || SelectedAdatapter == null) return false;
			return SelectedAdatapter.Pull();
		}

		public bool TakeMerge()
		{
			if (!gitManager.Settings.ExternalsType.HasFlag(GitSettingsJson.ExternalsTypeEnum.Merge) || SelectedAdatapter == null) return false;
			return SelectedAdatapter.Merge();
		}

		public bool TakeFetch(string remote)
		{
			if (!gitManager.Settings.ExternalsType.HasFlag(GitSettingsJson.ExternalsTypeEnum.Fetch) || SelectedAdatapter == null) return false;
			return SelectedAdatapter.Fetch(remote);
		}

		public bool TakeReset(Commit commit)
		{
			if (!gitManager.Settings.ExternalsType.HasFlag(GitSettingsJson.ExternalsTypeEnum.Fetch) || SelectedAdatapter == null) return false;
			return SelectedAdatapter.Reset(commit);
		}

		public bool TakeBlame(string path)
		{
			if (!gitManager.Settings.ExternalsType.HasFlag(GitSettingsJson.ExternalsTypeEnum.Blame) || SelectedAdatapter == null) return false;
			return SelectedAdatapter.Blame(path);
		}

		public void HandleConflict(string path)
		{
			if (SelectedAdatapter == null)
			{
				Debug.LogWarning("No selected external program.");
				return;
			}

			SelectedAdatapter.Conflict(path);
		}

		public bool TakeDiff(string path)
		{
			if (!gitManager.Settings.ExternalsType.HasFlag(GitSettingsJson.ExternalsTypeEnum.Diff) || SelectedAdatapter == null) return false;
			return SelectedAdatapter.Diff(path);
		}

		public bool TakeDiff(string path,string path2)
		{
			if (!gitManager.Settings.ExternalsType.HasFlag(GitSettingsJson.ExternalsTypeEnum.Diff) || SelectedAdatapter == null) return false;
			return SelectedAdatapter.Diff(path, path2);
		}

		public bool TakeDiff(string path, Commit end)
		{
			if (!gitManager.Settings.ExternalsType.HasFlag(GitSettingsJson.ExternalsTypeEnum.Diff) || SelectedAdatapter == null) return false;
			return SelectedAdatapter.Diff(path, end);
		}


		public bool TakeDiff(string path, Commit start,Commit end)
		{
			if (!gitManager.Settings.ExternalsType.HasFlag(GitSettingsJson.ExternalsTypeEnum.Diff) || SelectedAdatapter == null) return false;
			return SelectedAdatapter.Diff(path, start, end);
		}

		public bool TakeRevert(IEnumerable<string> paths)
		{
			if (!gitManager.Settings.ExternalsType.HasFlag(GitSettingsJson.ExternalsTypeEnum.Revert) || SelectedAdatapter == null) return false;
			return SelectedAdatapter.Revert(paths);
		}

		public bool TakeSwitch()
		{
			if (!gitManager.Settings.ExternalsType.HasFlag(GitSettingsJson.ExternalsTypeEnum.Switch) || SelectedAdatapter == null) return false;
			return SelectedAdatapter.Switch();
		}

		#region Process Helpers

		[StringFormatMethod("parametersFormat")]
		public bool CallProccess(string name, string parametersFormat,params object[] arg)
		{
			return CallProccess(name,string.Format(parametersFormat,arg));
		}


		public bool CallProccess(string name, string parameters)
		{
			string fullPath = GetFullPath(name);

			if (fullPath != null)
			{
				ProcessStartInfo startInfo = new ProcessStartInfo
				{
					CreateNoWindow = false,
					UseShellExecute = false,
					FileName = fullPath,
					WorkingDirectory = gitManager.RepoPath,
					WindowStyle = ProcessWindowStyle.Hidden,
					RedirectStandardOutput = true,
					Arguments = parameters
				};

				try
				{
					// Start the process with the info we specified.
					// Call WaitForExit and then the using statement will close.
					using (Process exeProcess = Process.Start(startInfo))
					{
						if (exeProcess == null) return false;
						exeProcess.WaitForExit();
						return true;
					}
				}
				catch
				{
					return false;
				}
			}
			return false;
		}

		private bool ExistsOnPath(string fileName)
		{
			return GetFullPath(fileName) != null;
		}

		private string GetFullPath(string fileName)
		{
			if (File.Exists(fileName))
				return Path.GetFullPath(fileName);

			var values = Environment.GetEnvironmentVariable("PATH",EnvironmentVariableTarget.Machine).Split(';');
			foreach (var path in values)
			{
				var fullPath = Path.Combine(path, fileName);
				if (File.Exists(fullPath))
					return fullPath;
			}
			return null;
		}
		#endregion

		#region Getters and Setters

		public GUIContent[] AdapterNames { get { return adapterNames; } }

		#endregion
	}
}