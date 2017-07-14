using System.Diagnostics;
using System.IO;
using Assets.Editor.Tests;
using LibGit2Sharp;
using NUnit.Framework;
using UniGit.Utils;
using UnityEditor;

public class InitializationTests : TestRepoFixture
{
	[Test]
	public void OnInitilize_InitilizeValidRepository_RepositoryInitilized()
	{
		gitManager.InitilizeRepository();
		Assert.IsTrue(Directory.Exists(gitManager.RepoPath));
		Assert.IsTrue(Repository.IsValid(gitManager.RepoPath));
	}

	[Test]
	public void OnInitilize_InitilizeGitIgnore_GitIgnoreInitilized()
	{
		gitManager.InitilizeRepository();
		Assert.IsTrue(File.Exists(gitManager.GitIgnoreFilePath));
		Assert.AreEqual(File.ReadAllText(gitManager.GitIgnoreFilePath),GitIgnoreTemplate.Template);
	}

	[Test]
	[Category("SKIP_SETUP")]
	public void OnInitilize_InitilizeValidRepositoryInExistingProject_RepositoryInitilized()
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
	}
}
