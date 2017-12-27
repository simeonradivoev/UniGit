using System.IO;
using LibGit2Sharp;
using NUnit.Framework;
using UniGit;
using UniGit.Utils;
using UnityEngine;

public class GitManagerTests : TestRepoFixture
{
    /*[Test]
	public void RepositoryHandlesLockedFileWhileRetrivingStatus()
	{
		string lockedFilePathName = "testFile.txt";
		string lockedFilePath = Path.Combine(gitManager.RepoPath, lockedFilePathName);
		using (var lockFileStream = File.CreateText(lockedFilePath))
		{
			lockFileStream.WriteLine("This is a locked test file");
		}
		Assert.IsTrue(File.Exists(lockedFilePath));
	    GitCommands.Stage(gitManager.Repository, lockedFilePathName);
		FileStream lockedFileStream = new FileStream(lockedFilePath,FileMode.Open,FileAccess.Read,FileShare.None);
		try
		{
		    gitManager.MarkDirty();
            gitCallbacks.IssueEditorUpdate();
			Assert.AreEqual(FileStatus.NewInIndex, gitManager.StatusTree.GetStatus(lockedFilePathName).State);
		}
		finally
		{
			lockedFileStream.Dispose();
		}
	}*/

    [Test]
    public void RepositoryHandlesLockedFileWhenWithIgnoreStatus()
    {
        File.AppendAllText(gitManager.GitIgnoreFilePath, "testFile.txt");
        string lockedFilePathName = "testFile.txt";
        string lockedFilePath = UniGitPath.Combine(gitManager.RepoPath, lockedFilePathName);
        using (var lockFileStream = File.CreateText(lockedFilePath))
        {
            lockFileStream.WriteLine("This is a locked test file");
        }
	    injectionHelper.Bind<GitProjectOverlay>();
	    var projectOverlays = injectionHelper.GetInstance<GitProjectOverlay>();

		Assert.IsTrue(File.Exists(lockedFilePath));
        GitCommands.Stage(gitManager.Repository, lockedFilePathName);
        FileStream lockedFileStream = new FileStream(lockedFilePath, FileMode.Open, FileAccess.Read, FileShare.None);
        try
        {
            gitManager.MarkDirty();
            injectionHelper.GetInstance<GitCallbacks>().IssueEditorUpdate();
            Assert.AreEqual(FileStatus.Ignored, projectOverlays.StatusTree.GetStatus(lockedFilePathName).State);
        }
        finally
        {
            lockedFileStream.Dispose();
        }
    }
}
