using System;
using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UniGit;
using UnityEditor;

public class PackageExporter
{
	public const string PackageName = "UniGit";

	public static readonly string[] AssetFolders =
	{
		"Assets/Editor Default Resources/Icons/UniGit",
		"Assets/Plugins/LibGit2Sharp",
		"Assets/Plugins/WindowsCredentialManagement"
	};

	public static readonly string[] AssetFiles =
	{
		"Assets/Editor Default Resources/UniGit/gitignore.txt",
		"Assets/Plugins/UniGit/Editor/UniGitVs.dll",
		"Assets/Plugins/UniGit/Editor/UniGitVs.pdb",
		"Assets/Plugins/UniGit/Editor/UniGitVs.dll.mdb"
	};

	[MenuItem("UniGit/Export As Package")]
	public static void GeneratePackage()
	{
		EditorApplication.LockReloadAssemblies();
		GitManager.DisablePostprocessing();
		ProcessStartInfo ProcStartInfo = new ProcessStartInfo("cmd");
		ProcStartInfo.RedirectStandardOutput = false;
		ProcStartInfo.UseShellExecute = false;
		ProcStartInfo.CreateNoWindow = false;
		ProcStartInfo.RedirectStandardError = false;
		Process MyProcess = new Process();
		ProcStartInfo.Arguments = "/c cd UniGitVs & start /wait build_dev.bat";
		MyProcess.StartInfo = ProcStartInfo;
		MyProcess.Start();
		MyProcess.WaitForExit();

		List<string> paths = new List<string>(AssetFiles);
		foreach (var folder in AssetFolders)
		{
			paths.AddRange(GetAssetAt(folder));
		}

		UnityEngine.Debug.Log("---- Paths to be exported ----");
		foreach (var path in paths)
		{
			UnityEngine.Debug.Log(path);
		}
		UnityEngine.Debug.Log("---- ------------------- ----");

		File.Copy(Application.dataPath.Replace("Assets", "UniGitVs") + "\\bin\\Debug\\Plugins\\UniGit\\Editor\\UniGitVs.dll", Application.dataPath + "\\Plugins\\UniGit\\Editor\\UniGitVs.dll");
		File.Copy(Application.dataPath.Replace("Assets", "UniGitVs") + "\\bin\\Debug\\Plugins\\UniGit\\Editor\\UniGitVs.pdb", Application.dataPath + "\\Plugins\\UniGit\\Editor\\UniGitVs.pdb");
		//double refresh so that Unity generates the UniGitVs.dll.mdb file
		AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
		AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
		AssetDatabase.ExportPackage(paths.ToArray(), PackageName + ".unitypackage", ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies);

		AssetDatabase.DeleteAsset("Assets/Plugins/UniGit/Editor/UniGitVs.dll");
		AssetDatabase.DeleteAsset("Assets/Plugins/UniGit/Editor/UniGitVs.pdb");
		AssetDatabase.DeleteAsset("Assets/Plugins/UniGit/Editor/UniGitVs.dll.mdb");

		EditorApplication.UnlockReloadAssemblies();
		GitManager.EnablePostprocessing();
	}

	private static void CopyFilesTo(string from, string to)
	{
		DirectoryInfo dirInfo = new DirectoryInfo(to);
		if (!dirInfo.Exists)
			Directory.CreateDirectory(to);

		List<string> files = Directory.GetFiles(from, "*.*", SearchOption.AllDirectories).ToList();

		foreach (string file in files)
		{
			FileInfo mFile = new FileInfo(file);

			string subDir = mFile.Directory.ToString().Replace(from + "\\", "");
			string newPath = Path.Combine(to, subDir);
			// to remove name collisions
			if (!new FileInfo(Path.Combine(newPath, mFile.Name)).Exists)
			{
				if (!Directory.Exists(newPath))
				{
					Directory.CreateDirectory(newPath);
				}
				mFile.CopyTo(Path.Combine(newPath, mFile.Name));
			}
		}
	}

	private static string[] GetAssetAt(string path)
	{
		return AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith(path)).ToArray();
	}
}
