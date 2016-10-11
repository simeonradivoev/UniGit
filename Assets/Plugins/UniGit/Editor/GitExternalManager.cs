using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
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
			SelectedAdatapter.Commit(message);
			return true;
		}

		public static bool TakePush()
		{
			if (!GitManager.Settings.ExternalsType.HasFlag(GitSettings.ExternalsTypeEnum.Push) || SelectedAdatapter == null) return false;
			SelectedAdatapter.Push();
			return true;
		}

		public static bool TakePull()
		{
			if (!GitManager.Settings.ExternalsType.HasFlag(GitSettings.ExternalsTypeEnum.Pull) || SelectedAdatapter == null) return false;
			SelectedAdatapter.Pull();
			return true;
		}

		public static bool TakeMerge()
		{
			if (!GitManager.Settings.ExternalsType.HasFlag(GitSettings.ExternalsTypeEnum.Merge) || SelectedAdatapter == null) return false;
			SelectedAdatapter.Merge();
			return true;
		}

		public static bool TakeFetch(string remote)
		{
			if (!GitManager.Settings.ExternalsType.HasFlag(GitSettings.ExternalsTypeEnum.Fetch) || SelectedAdatapter == null) return false;
			SelectedAdatapter.Fetch(remote);
			return true;
		}

		public static void HandleConflict(string left, string right, string ansestor, string merge,Type assetType)
		{
			if (SelectedAdatapter == null)
			{
				Debug.LogWarning("No selected external program.");
				return;
			}

			SelectedAdatapter.Conflict(left,right,ansestor,merge,assetType);
		}

		public static void ShowDiff(string leftTitle,string leftPath, string rightTitle,string rightPath, [CanBeNull] Type assetType)
		{
			if (SelectedAdatapter == null)
			{
				Debug.LogWarning("No selected external program.");
				return;
			}

			SelectedAdatapter.Diff(leftTitle,leftPath, rightTitle,rightPath, assetType);
		}

		#region Process Helpers

		[StringFormatMethod("parametersFormat")]
		public static bool CallProccess(string name, string parametersFormat,params object[] arg)
		{
			return CallProccess(name,string.Format(parametersFormat,arg));
		}


		public static bool CallProccess(string name, string parameters)
		{
			if (ExistsOnPath(name))
			{
				ProcessStartInfo startInfo = new ProcessStartInfo
				{
					CreateNoWindow = false,
					UseShellExecute = false,
					FileName = name,
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

			var values = Environment.GetEnvironmentVariable("PATH");
			foreach (var path in values.Split(';'))
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