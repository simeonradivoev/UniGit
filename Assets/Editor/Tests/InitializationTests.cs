using System.Diagnostics;
using System.IO;
using LibGit2Sharp;
using NUnit.Framework;
using UniGit.Utils;
using UnityEditor;

public class InitializationTests : TestRepoFixture
{
	[Test]
	public void InitilizeValidRepository_RepositoryInitilized()
	{
		Assert.IsTrue(Directory.Exists(gitManager.RepoPath));
		Assert.IsTrue(Repository.IsValid(gitManager.RepoPath));
	}

	[Test]
	public void InitilizeUniGitSettingsFolder_SettingsFolderCreated()
	{
		Assert.IsTrue(Directory.Exists(gitManager.GitSettingsFolderPath));
	}

	[Test]
	public void InitilizeGitIgnore_GitIgnoreInitilized()
	{
		Assert.IsTrue(File.Exists(gitManager.GitIgnoreFilePath));
		Assert.AreEqual(File.ReadAllText(gitManager.GitIgnoreFilePath),GitIgnoreTemplate.Template);
	}

	/*[Test]
	public void InitilizeValidRepositoryInExistingProject_RepositoryInitilized()
	{
		using (Process process = new Process())
		{
			process.StartInfo.FileName = EditorApplication.applicationPath;
			process.StartInfo.Arguments = "-batchmode -quit -createProject " + gitManager.RepoPath;
			process.Start();
			process.WaitForExit();
		}

		gitManager.InitilizeRepository();

		Assert.IsTrue(Directory.Exists(gitManager.RepoPath));
		Assert.IsTrue(Repository.IsValid(gitManager.RepoPath));
		Assert.IsTrue(File.Exists(gitManager.GitIgnoreFilePath));
	}*/
}
