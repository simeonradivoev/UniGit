using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using LibGit2Sharp;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UniGit
{
	public static class GitConflictsHandler
	{
		public static event Action<string,Object,CancelEventArgs> OnHandleConflictEvent;

		public static bool CanResolveConflictsWithTool(string path)
		{
			if (!GitManager.IsValidRepo) return false;
			var conflict = GitManager.Repository.Index.Conflicts[path];
			return !GitManager.Repository.Lookup<Blob>(conflict.Ours.Id).IsBinary;
		}

		public static void ResolveConflicts(string path, MergeFileFavor favor)
		{
			if(!GitManager.IsValidRepo) return;

			if (favor == MergeFileFavor.Normal)
			{
				Object asset = AssetDatabase.LoadAssetAtPath(path, typeof(Object));
				var arg = new CancelEventArgs();
				if(OnHandleConflictEvent != null) OnHandleConflictEvent.Invoke(path,asset, arg);

				if (arg.Cancel) return;
				var conflict = GitManager.Repository.Index.Conflicts[path];
				var ancestor = conflict.Ancestor;
				var ours = conflict.Ours;
				var theirs = conflict.Theirs;

				var ancestorBlob = (ancestor != null) ? (Blob)GitManager.Repository.Lookup(ancestor.Id) : null;
				var ourBlob = (ours != null) ? (Blob)GitManager.Repository.Lookup(ours.Id) : null;
				var theirBlob = (theirs != null) ? (Blob)GitManager.Repository.Lookup(theirs.Id) : null;

				var ourStream = (ours != null) ? ourBlob.GetContentStream(new FilteringOptions(ours.Path)) : null;
				var theirStream = (theirs != null) ? theirBlob.GetContentStream(new FilteringOptions(theirs.Path)) : null;
				var ancestorStream = (ancestor != null) ? ancestorBlob.GetContentStream(new FilteringOptions(ancestor.Path)) : null;

				var conflictPathOurs = Application.dataPath.Replace("Assets", "Temp/our_conflict_file_tmp");
				var conflictPathTheirs = Application.dataPath.Replace("Assets", "Temp/their_conflict_file_tmp");
				var conflictPathAncestor = Application.dataPath.Replace("Assets", "Temp/ancestor_conflict_file_tmp");

				if (ourStream != null)
				{
					using (var ourOutputStream = File.Create(conflictPathOurs))
					{
						ourStream.CopyTo(ourOutputStream);
					}
				}
				if (theirStream != null)
				{
					using (var theirOutputStream = File.Create(conflictPathTheirs))
					{
						theirStream.CopyTo(theirOutputStream);
					}
				}
				if (ancestorStream != null)
				{
					using (var ancestorOutputStream = File.Create(conflictPathAncestor))
					{
						ancestorStream.CopyTo(ancestorOutputStream);
					}
				}

				if (asset is Texture)
				{
					CallProccess("TortoiseGitIDiff.exe", string.Format("-base:\"{0}\" -mine:\"{1}\" -theirs:\"{2}\" -merged:\"{3}\"", conflictPathAncestor, conflictPathOurs, conflictPathTheirs, path));
				}
				else
				{
					CallProccess("TortoiseGitMerge.exe", string.Format("-base:\"{0}\" -mine:\"{1}\" -theirs:\"{2}\" -merged:\"{3}\"", conflictPathAncestor, conflictPathOurs, conflictPathTheirs, path));
				}
			}
			else if (favor == MergeFileFavor.Ours)
			{
				var conflict = GitManager.Repository.Index.Conflicts[path];
				var ours = conflict.Ours;
				if (ours != null)
				{
					GitManager.Repository.Index.Remove(ours.Path);
					GitManager.Repository.CheckoutPaths("ORIG_HEAD", new[] { ours.Path });
				}
			}
			else if (favor == MergeFileFavor.Theirs)
			{
				var conflict = GitManager.Repository.Index.Conflicts[path];
				var theirs = conflict.Theirs;
				if (theirs != null)
				{
					GitManager.Repository.Index.Remove(theirs.Path);
					GitManager.Repository.CheckoutPaths("MERGE_HEAD", new[] { theirs.Path });
				}
			}

			//Debug.Log(EditorUtility.InvokeDiffTool(Path.GetFileName(theirs.Path) + " - Theirs", conflictPathTheirs, Path.GetFileName(ours.Path) + " - Ours", conflictPathOurs, "", conflictPathAncestor));
		}

		private static bool CallProccess(string name,string parameters)
		{
			if (ExistsOnPath(name))
			{
				ProcessStartInfo startInfo = new ProcessStartInfo();
				startInfo.CreateNoWindow = false;
				startInfo.UseShellExecute = false;
				startInfo.FileName = "TortoiseGitIDiff.exe";
				startInfo.WindowStyle = ProcessWindowStyle.Hidden;
				startInfo.Arguments = parameters;

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

		public static bool ExistsOnPath(string fileName)
		{
			return GetFullPath(fileName) != null;
		}

		public static string GetFullPath(string fileName)
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
	}
}