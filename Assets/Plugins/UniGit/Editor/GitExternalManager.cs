using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using LibGit2Sharp;
using UniGit.Adapters;
using UniGit.Attributes;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace UniGit
{
	public static class GitExternalManager
	{
		private static IExternalAdapter[] adapters;
		private static GUIContent[] adapterNames;
		private static int selectedAdapterIndex = -1;
		private static IExternalAdapter selectedAdapter;
		private static bool initiazlitedSelected;

		internal static void Load()
		{
			adapters = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes().Where(t => typeof(IExternalAdapter).IsAssignableFrom(t) && t.IsClass &&  !t.IsAbstract)).Select(t => Activator.CreateInstance(t)).Cast<IExternalAdapter>().ToArray();
			adapterNames = adapters.Select(a => new GUIContent(GetAdapterName(a))).ToArray();
		}

		#region Selection
		//Using lazy initialization
		private static IExternalAdapter SelectedAdatapter
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

		public static int SelectedAdapterIndex
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

		private static void InitializeSelectedAdapter()
		{
			selectedAdapter = adapters.FirstOrDefault(a => Exists(a) && GetAdapterName(a) == GitManager.Settings.ExternalProgram) ?? adapters.FirstOrDefault(a => Exists(a));
			if (selectedAdapter != null) selectedAdapterIndex = Array.IndexOf(adapters, selectedAdapter);
			initiazlitedSelected = true;
		}

		public static void SetSelectedAdapter(int index)
		{
			if(index >= adapters.Length && index < 0 && selectedAdapterIndex == index) return;
			selectedAdapterIndex = index;
			selectedAdapter = adapters[index];
		}

		#endregion

		private static string GetAdapterName(IExternalAdapter adapter)
		{
			ExternalAdapterAttribute attribute = adapter.GetType().GetCustomAttributes(typeof (ExternalAdapterAttribute),false).FirstOrDefault() as ExternalAdapterAttribute;
			if (attribute == null) return null;
			return attribute.FriendlyName;
		}

		private static bool Exists(IExternalAdapter adapterInfo)
		{
			ExternalAdapterAttribute attribute = adapterInfo.GetType().GetCustomAttributes(typeof(ExternalAdapterAttribute), false).FirstOrDefault() as ExternalAdapterAttribute;
			if (attribute == null) return false;
			return attribute.ProcessNames.All(p => ExistsOnPath(p));
		}

		public static bool TakeCommit(string message)
		{
			if (!GitManager.Settings.ExternalsType.HasFlag(GitSettings.ExternalsTypeEnum.Commit) || SelectedAdatapter == null) return false;
			return SelectedAdatapter.Commit(message);
		}

		public static bool TakePush()
		{
			if (!GitManager.Settings.ExternalsType.HasFlag(GitSettings.ExternalsTypeEnum.Push) || SelectedAdatapter == null) return false;
			return SelectedAdatapter.Push();
		}

		public static bool TakePull()
		{
			if (!GitManager.Settings.ExternalsType.HasFlag(GitSettings.ExternalsTypeEnum.Pull) || SelectedAdatapter == null) return false;
			return SelectedAdatapter.Pull();
		}

		public static bool TakeMerge()
		{
			if (!GitManager.Settings.ExternalsType.HasFlag(GitSettings.ExternalsTypeEnum.Merge) || SelectedAdatapter == null) return false;
			return SelectedAdatapter.Merge();
		}

		public static bool TakeFetch(string remote)
		{
			if (!GitManager.Settings.ExternalsType.HasFlag(GitSettings.ExternalsTypeEnum.Fetch) || SelectedAdatapter == null) return false;
			return SelectedAdatapter.Fetch(remote);
		}

		public static bool TakeReset(Commit commit)
		{
			if (!GitManager.Settings.ExternalsType.HasFlag(GitSettings.ExternalsTypeEnum.Fetch) || SelectedAdatapter == null) return false;
			return SelectedAdatapter.Reset(commit);
		}

		public static void HandleConflict(string path)
		{
			if (SelectedAdatapter == null)
			{
				Debug.LogWarning("No selected external program.");
				return;
			}

			SelectedAdatapter.Conflict(path);
		}

		public static void ShowDiff(string path)
		{
			if (SelectedAdatapter == null)
			{
				Debug.LogWarning("No selected external program.");
				return;
			}

			SelectedAdatapter.Diff(path);
		}

		public static void ShowDiff(string path,string path2)
		{
			if (SelectedAdatapter == null)
			{
				Debug.LogWarning("No selected external program.");
				return;
			}

			SelectedAdatapter.Diff(path, path2);
		}

		public static void ShowDiff(string path, Commit start,Commit end)
		{
			if (SelectedAdatapter == null)
			{
				Debug.LogWarning("No selected external program.");
				return;
			}

			SelectedAdatapter.Diff(path, start, end);
		}

		public static bool TakeRevert(IEnumerable<string> paths)
		{
			if (SelectedAdatapter == null)
			{
				Debug.LogWarning("No selected external program.");
				return false;
			}

			return SelectedAdatapter.Revert(paths);
		}

		public static bool TakeSwitch()
		{
			if (!GitManager.Settings.ExternalsType.HasFlag(GitSettings.ExternalsTypeEnum.Switch) || SelectedAdatapter == null) return false;
			return SelectedAdatapter.Switch();
		}

		#region Process Helpers

		[StringFormatMethod("parametersFormat")]
		public static bool CallProccess(string name, string parametersFormat,params object[] arg)
		{
			return CallProccess(name,string.Format(parametersFormat,arg));
		}


		public static bool CallProccess(string name, string parameters)
		{
			string fullPath = GetFullPath(name);

			if (fullPath != null)
			{
				ProcessStartInfo startInfo = new ProcessStartInfo
				{
					CreateNoWindow = false,
					UseShellExecute = false,
					FileName = fullPath,
					WorkingDirectory = GitManager.RepoPath,
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

		private static bool ExistsOnPath(string fileName)
		{
			return GetFullPath(fileName) != null;
		}

		private static string GetFullPath(string fileName)
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

		public static GUIContent[] AdapterNames { get { return adapterNames; } }

		#endregion
	}
}