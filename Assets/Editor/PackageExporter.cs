using System;
using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Win32;
using UniGit;
using UniGit.Utils;
using UnityEditor;
using Debug = UnityEngine.Debug;

public class PackageExporter
{
	public const string PackageName = "UniGit";
	public const string SourceFilesPath = "Assets/Plugins/UniGit";

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

	[MenuItem("UniGit/Dev/Build DLL")]
	public static void BuildDLL()
	{
		UpdateVSProject();

		string devnetPath = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\devenv.exe", null, null).ToString();
		if (string.IsNullOrEmpty(devnetPath))
		{
			Debug.LogError("Could not find devnet in registry!");
			devnetPath = EditorUtility.OpenFilePanel("Devnet.Exe", "", "exe");

			if (string.IsNullOrEmpty(devnetPath))
			{
				Debug.LogError("Could not find devnet!");
				return;
			}
		}
		else
		{
			Debug.Log("Devnet Path: " + devnetPath);
		}

		Process devnetProcess = new Process();
		devnetProcess.StartInfo.Arguments = string.Format("{0} {1} {2} {3}", "\"UniGitVs.sln\"", " /build Debug", "/project \"UniGitVs.csproj\"", "/projectconfig Debug");
		devnetProcess.StartInfo.RedirectStandardError = true;
		devnetProcess.StartInfo.RedirectStandardOutput = true;
		devnetProcess.StartInfo.UseShellExecute = false;
		devnetProcess.StartInfo.FileName = devnetPath.ToString();
		//devnetProcess.StartInfo.Verb = "runas";
		devnetProcess.StartInfo.WorkingDirectory = Application.dataPath.Replace("/", "\\").Replace("Assets", "UniGitVs");

		devnetProcess.Start();
		EditorUtility.DisplayProgressBar("Building Project", "Building in progress", 0.1f);
		devnetProcess.WaitForExit();
		EditorUtility.ClearProgressBar();

		string logs = devnetProcess.StandardOutput.ReadToEnd();
		string errors = devnetProcess.StandardError.ReadToEnd();
		bool buildHasOutput = !string.IsNullOrEmpty(logs) || !string.IsNullOrEmpty(errors);

		if (buildHasOutput)
		{
			Debug.Log("---- Build Process Output ----");
			if (!string.IsNullOrEmpty(logs))
			{
				Debug.Log(logs);
			}

			if (!string.IsNullOrEmpty(errors))
			{
				Debug.LogError(errors);
			}
			Debug.Log("---- ------------------- ----");
		}

		Application.OpenURL(Application.dataPath.Replace("/", "\\").Replace("Assets", UniGitPath.Combine("UniGitVs","bin", "Debug")));
	}

	[MenuItem("UniGit/Dev/Export As Package")]
	public static void GeneratePackage()
	{
		EditorApplication.LockReloadAssemblies();
		UniGitLoader.GitManager.DisablePostprocessing();

		try
		{
			BuildDLL();

			/*ProcessStartInfo ProcStartInfo = new ProcessStartInfo("cmd");
			ProcStartInfo.RedirectStandardOutput = true;
			ProcStartInfo.UseShellExecute = false;
			ProcStartInfo.CreateNoWindow = false;
			ProcStartInfo.RedirectStandardError = true;
			ProcStartInfo.Verb = "runas";
			Process MyProcess = new Process();
			ProcStartInfo.Arguments = "/c cd UniGitVs & start /wait build_dev.bat";
			MyProcess.StartInfo = ProcStartInfo;
			MyProcess.Start();
			MyProcess.WaitForExit();*/

			List<string> paths = new List<string>(AssetFiles);
			foreach (var folder in AssetFolders)
			{
				paths.AddRange(GetAssetAt(folder));
			}

			Debug.Log("---- Paths to be exported ----");
			foreach (var path in paths)
			{
				Debug.Log(path);
			}
			Debug.Log("---- ------------------- ----");

			File.Copy(Application.dataPath.Replace("Assets", "UniGitVs") + "\\bin\\Debug\\Plugins\\UniGit\\Editor\\UniGitVs.dll", Application.dataPath + "\\Plugins\\UniGit\\Editor\\UniGitVs.dll");
			File.Copy(Application.dataPath.Replace("Assets", "UniGitVs") + "\\bin\\Debug\\Plugins\\UniGit\\Editor\\UniGitVs.pdb", Application.dataPath + "\\Plugins\\UniGit\\Editor\\UniGitVs.pdb");
			//double refresh so that Unity generates the UniGitVs.dll.mdb file
			AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
			AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
			AssetDatabase.ExportPackage(paths.ToArray(), PackageName + ".unitypackage", ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies);
			Debug.Log("Unity Package created at: " + Application.dataPath.Replace("Assets", PackageName + ".unitypackage"));

			AssetDatabase.DeleteAsset("Assets/Plugins/UniGit/Editor/UniGitVs.dll");
			AssetDatabase.DeleteAsset("Assets/Plugins/UniGit/Editor/UniGitVs.pdb");
			AssetDatabase.DeleteAsset("Assets/Plugins/UniGit/Editor/UniGitVs.dll.mdb");
		}
		catch (Exception)
		{

			throw;
		}
		finally
		{
			EditorApplication.UnlockReloadAssemblies();
			UniGitLoader.GitManager.EnablePostprocessing();
		}
	}

	private static void UpdateVSProject()
	{
		var projectPath = Application.dataPath.Replace("Assets", "UniGitVs") + "/UniGitVs.csproj";
		var editorDllPath = EditorApplication.applicationPath.Replace("Unity.exe", "Data\\Managed\\UnityEditor.dll").Replace("/", "\\");
		var engineDllPath = EditorApplication.applicationPath.Replace("Unity.exe", "Data\\Managed\\UnityEngine.dll").Replace("/", "\\");

		XmlDocument doc = new XmlDocument();
		doc.Load(projectPath);
		XmlNamespaceManager xmlnsManager = new XmlNamespaceManager(doc.NameTable);
		string ns = doc.DocumentElement.NamespaceURI;
		xmlnsManager.AddNamespace("def", ns);
		#region References
		var RefItemGroup = doc.DocumentElement.GetElementsByTagName("ItemGroup")[0];
		var editorNode = RefItemGroup.SelectSingleNode("//def:Reference[@Include='UnityEditor']/def:HintPath", xmlnsManager);
		if (editorNode != null)
		{
			editorNode.InnerText = editorDllPath;
		}
		else
		{
			throw new Exception("Missing Editor Reference Node");
		}
		var engineNode = RefItemGroup.SelectSingleNode("//def:Reference[@Include='UnityEngine']/def:HintPath", xmlnsManager);
		if (engineNode != null)
		{
			engineNode.InnerText = engineDllPath;
		}
		else
		{
			throw new Exception("Missing Engine Reference Node");
		}
		#endregion
		#region Links
		var LinksItemGroup = doc.DocumentElement.GetElementsByTagName("ItemGroup")[1];
		LinksItemGroup.InnerXml = "";
		string[] sourceFiles = AssetDatabase.FindAssets("t:script",new []{ SourceFilesPath }).Select(g => AssetDatabase.GUIDToAssetPath(g)).Select(p => p.Replace("/","\\")).ToArray();
		foreach (var file in sourceFiles)
		{
			var child = doc.CreateElement("Compile", ns);
			var link = doc.CreateElement("Link", ns);
			child.SetAttribute("Include", "..\\" + file);
			link.InnerText = Path.GetFileName(file);
			child.AppendChild(link);
			LinksItemGroup.AppendChild(child);
		}

		var assemblyInfo = doc.CreateElement("Compile", ns);
		assemblyInfo.SetAttribute("Include", "Properties\\AssemblyInfo.cs");
		LinksItemGroup.AppendChild(assemblyInfo);
		var resources = doc.CreateElement("Compile", ns);
		resources.SetAttribute("Include", "Properties\\Resources.Designer.cs");
		var tmp = doc.CreateElement("AutoGen", ns);
		tmp.InnerText = "True";
		resources.AppendChild(tmp);
		tmp = doc.CreateElement("DesignTime", ns);
		tmp.InnerText = "True";
		resources.AppendChild(tmp);
		tmp = doc.CreateElement("DependentUpon", ns);
		tmp.InnerText = "Resources.resx";
		resources.AppendChild(tmp);
		LinksItemGroup.AppendChild(resources);

		#endregion
		doc.Save(projectPath);
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
			string newPath = UniGitPath.Combine(to, subDir);
			// to remove name collisions
			if (!new FileInfo(UniGitPath.Combine(newPath, mFile.Name)).Exists)
			{
				if (!Directory.Exists(newPath))
				{
					Directory.CreateDirectory(newPath);
				}
				mFile.CopyTo(UniGitPath.Combine(newPath, mFile.Name));
			}
		}
	}

	private static string[] GetAssetAt(string path)
	{
		return AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith(path)).ToArray();
	}
}
