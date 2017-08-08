using System.IO;
using System.Linq;
using NUnit.Framework;
using UniGit;
using UnityEditor;
using UnityEngine;

public class GitDiffWindowTests : TestRepoFixture
{
	private GitDiffWindow diffWindow;
	private EditorWindow prevFocusWindow;

	[SetUp]
	public void GitDiffTestsSetup()
	{
		prevFocusWindow = EditorWindow.focusedWindow;
        injectionHelper.Bind<GitExternalManager>();
	    injectionHelper.Bind<GitCredentialsManager>();
		diffWindow = ScriptableObject.CreateInstance<GitDiffWindow>();
        injectionHelper.Inject(diffWindow);
	    diffWindow.Show();
    }

	[TearDown]
	public void GitDiffTestsTearDown()
	{
		diffWindow.Close();
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
		gitSettings.ReadFromFile = true;

		const string commitText = "First Commit from File Commit Message";
		File.WriteAllText(gitManager.GitCommitMessageFilePath, commitText);
		diffWindow.Commit();
		Assert.AreEqual(1,gitManager.Repository.Commits.Count());
		Assert.AreEqual(commitText,gitManager.Repository.Commits.First().Message);
	}

	[Test]
	public void OnOpen_ReadCommitFileContents()
	{
		gitSettings.ReadFromFile = true;

		const string commitText = "Test Message";
		File.WriteAllText(gitManager.GitCommitMessageFilePath, commitText);
		prevFocusWindow.Focus();
		Assert.AreNotEqual(EditorWindow.focusedWindow, diffWindow);
		diffWindow.Focus();
		Assert.AreEqual(EditorWindow.focusedWindow, diffWindow);
		Assert.AreEqual(commitText, diffWindow.GitDiffSettings.commitMessageFromFile);
	}

	[Test]
	public void OnAmmendCommit_AmmendCommitMessage()
	{
		gitSettings.ReadFromFile = true;

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