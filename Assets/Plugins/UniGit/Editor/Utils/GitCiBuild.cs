using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;
using UnityEngine;

namespace UniGit.Utils
{
	public class GitCiBuild
	{
		[UsedImplicitly]
		static void PerformBuild()
		{
			string dllDir = GetArg("-dlldir");
			if (!Directory.Exists(dllDir)) Directory.CreateDirectory(dllDir);
			else
			{
				Directory.Delete(dllDir,true);
				Directory.CreateDirectory(dllDir);
			}

			List<string> filesToCopy = new List<string>
			{
				Application.dataPath.Replace("Assets", UniGitPath.Combine("Library", "ScriptAssemblies", "UniGit.dll")), 
				Application.dataPath.Replace("Assets", UniGitPath.Combine("Library", "ScriptAssemblies", "UniGit.dll.mdb")), 
				UniGitPath.Combine(Application.dataPath, "Plugins", "UniGit", "Editor", "UniGitResources.dll"),
				UniGitPath.Combine(Application.dataPath, "Plugins", "UniGit", "Editor", "UniGitResources.dll.mdb"),
				UniGitPath.Combine(Application.dataPath, "Plugins", "UniGit", "Editor", "UniGitResources.pdb")
			};

			foreach (var file in filesToCopy)
			{
				File.Copy(file,Path.Combine(dllDir,Path.GetFileName(file)),true);
			}
		}

		private static string GetArg(string name)
		{
			var args = System.Environment.GetCommandLineArgs();
			for (int i = 0; i < args.Length; i++)
			{
				if (args[i] == name && args.Length > i + 1)
				{
					return args[i + 1];
				}
			}
			return null;
		}
	}
}
