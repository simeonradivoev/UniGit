using System.IO;
using System.Linq;
using NUnit.Framework;
using UniGit;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

public class GitDiffWindowTests : TestRepoFixture
{
	private GitDiffWindow diffWindow;
	private GitDiffWindow diffWindow2;

	[SetUp]
	public void GitDiffTestsSetup()
	{
		injectionHelper.Bind<GitExternalManager>();
		injectionHelper.Bind<GitCredentialsManager>();
		injectionHelper.Bind<GitLfsHelper>();
		injectionHelper.Bind<FileLinesReader>();
		injectionHelper.Bind<string>().WithId("repoPath").FromInstance("");
		diffWindow = ScriptableObject.CreateInstance<GitDiffWindow>();
		diffWindow2 = ScriptableObject.CreateInstance<GitDiffWindow>();
		injectionHelper.Inject(diffWindow);
		injectionHelper.Inject(diffWindow2);
		diffWindow.Show();
		diffWindow2.Show();
    }

	[TearDown]
	public void GitDiffTestsTearDown()
	{
		diffWindow.Close();
		diffWindow2.Close();
	}

	[Test]
	public void OnCommit_CommitChanges()
	{
		const string commitText = "First Commit";
		diffWindow.SetCommitMessage(commitText);
		Assert.AreEqual(commitText, diffWindow.GetActiveCommitMessage());
		diffWindow.Commit();
		Assert.AreEqual(1, gitManager.Repository.Commits.Count());
		Assert.AreEqual(commitText, gitManager.Repository.Commits.First().Message);
	}

	[Test]
	public void OnCommit_CommitChangesWithFileCommitMessage()
	{
		injectionHelper.GetInstance<GitSettingsJson>().ReadFromFile = true;

		const string commitText = "First Commit from File Commit Message";
		File.WriteAllText(gitManager.GitCommitMessageFilePath, commitText);
		diffWindow.Commit();
		Assert.AreEqual(1,gitManager.Repository.Commits.Count());
		Assert.AreEqual(commitText,gitManager.Repository.Commits.First().Message);
	}

	[Test]
	public void OnOpen_ReadCommitFileContents()
	{
		injectionHelper.GetInstance<GitSettingsJson>().ReadFromFile = true;

		const string commitText = "Test Message";
		diffWindow2.Focus();
		File.WriteAllText(gitManager.GitCommitMessageFilePath, commitText);
		diffWindow.Focus();
		Assert.AreEqual(commitText, diffWindow.GitDiffSettings.commitMessageFromFile);
	}

	[Test]
	public void OnAmmendCommit_AmmendCommitMessage()
	{
		injectionHelper.GetInstance<GitSettingsJson>().ReadFromFile = true;

		const string commitMessage = "First Commit";
		diffWindow.SetCommitMessage(commitMessage);
		diffWindow.Commit();
		Assert.AreEqual(1,gitManager.Repository.Commits.Count());
		Assert.AreEqual(commitMessage, gitManager.Repository.Commits.First().Message);
		Assert.IsTrue(string.IsNullOrEmpty(diffWindow.GetActiveCommitMessage()));
		diffWindow.AmmendCommit();
		Assert.AreEqual(commitMessage, diffWindow.GetActiveCommitMessage());
	}
}