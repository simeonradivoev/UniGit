using System.IO;
using LibGit2Sharp;
using NUnit.Framework;
using UnityEngine;

public class GitManagerTests : TestRepoFixture
{
	[Test]
	public void RepositoryHandlesLockedFileWhileRetrivingStatus()
	{
		gitManager.InitilizeRepository(false);
		string lockedFilePathName = "testFile.txt";
		string lockedFilePath = Path.Combine(gitManager.RepoPath, lockedFilePathName);
		using (var lockFileStream = File.CreateText(lockedFilePath))
		{
			lockFileStream.WriteLine("This is a locked test file");
		}
		Assert.IsTrue(File.Exists(lockedFilePath));
        Commands.Stage(gitManager.Repository, lockedFilePathName);
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
	}
}
