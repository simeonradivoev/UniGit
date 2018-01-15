using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using LibGit2Sharp;
using NUnit.Framework;
using UniGit;
using UniGit.Settings;
using UniGit.Status;
using UniGit.Utils;
using UnityEngine.TestTools;

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

    [UnityTest]
    public IEnumerator RepositoryHandlesLockedFileWhenWithIgnoreStatus()
    {
        File.AppendAllText(gitManager.GitIgnoreFilePath, "testFile.txt");
        string lockedFilePathName = "testFile.txt";
        string lockedFilePath = UniGitPath.Combine(gitManager.RepoPath, lockedFilePathName);
        using (var lockFileStream = File.CreateText(lockedFilePath))
        {
            lockFileStream.WriteLine("This is a locked test file");
        }
	    injectionHelper.Bind<GitProjectOverlay>().WithArguments(new InjectionArgument("cullNonAssetPaths",false));
	    var projectOverlays = injectionHelper.GetInstance<GitProjectOverlay>();
	    var prefs = injectionHelper.GetInstance<IGitPrefs>();
		prefs.SetBool(GitProjectOverlay.ForceUpdateKey,true);

		Assert.IsTrue(File.Exists(lockedFilePath));
        GitCommands.Stage(gitManager.Repository, lockedFilePathName);
        FileStream lockedFileStream = new FileStream(lockedFilePath, FileMode.Open, FileAccess.Read, FileShare.None);
        try
        {
            gitManager.MarkDirty();
	        yield return null;
            Assert.AreEqual(FileStatus.Ignored, projectOverlays.StatusTree.GetStatus(lockedFilePathName).State);
        }
        finally
        {
            lockedFileStream.Dispose();
        }
    }

	[Test]
	public void RepositoryIgnoresFolderPaths()
	{
		string folderPath = Path.Combine(gitManager.RepoPath, "Test Folder");
		Directory.CreateDirectory(folderPath);
		string folderMetaPath = Path.Combine(gitManager.RepoPath, "Test Folder.meta");
		File.WriteAllText(folderMetaPath,string.Empty);
		string imagePath = Path.Combine(gitManager.RepoPath, "Test Folder.png");
		File.WriteAllText(imagePath,string.Empty);
		string imageMetaPath = Path.Combine(gitManager.RepoPath, "Test Folder.png.meta");
		File.WriteAllText(imageMetaPath,string.Empty);

		string[] paths = gitManager.GetPathWithMeta(folderPath).ToArray();
		Assert.AreEqual(1,paths.Length);
		Assert.Contains(folderMetaPath,paths);

		paths = gitManager.GetPathWithMeta(imagePath).ToArray();
		Assert.AreEqual(2,paths.Length);
		Assert.Contains(imagePath,paths);
		Assert.Contains(imageMetaPath,paths);

		gitManager.MarkDirty(folderPath);
		gitCallbacks.IssueEditorUpdate();
	}

	[UnityTest]
	public IEnumerator MissingFolderForDriftingMetaCreated()
	{
		injectionHelper.GetInstance<GitSettingsJson>().CreateFoldersForDriftingMeta = true;

		string metaFileName = "Test Folder.meta";
		string metaFilePath = Path.Combine(gitManager.RepoPath, metaFileName);
		File.WriteAllText(metaFilePath,"Test Meta");
		Assert.IsTrue(File.Exists(metaFilePath));
		gitManager.AutoStage(metaFileName);
		yield return null;
		GitStatusEntry metaStatusEntry;
		Assert.IsTrue(data.RepositoryStatus.Get(metaFileName, out metaStatusEntry));
		Assert.AreEqual(FileStatus.NewInIndex,metaStatusEntry.Status);
		var addedMetaCommit = gitManager.Repository.Commit("Drifting Meta Commit", signature, signature);
		yield return null;
		File.Delete(metaFilePath);
		gitManager.AutoStage(metaFileName);
		yield return null;
		Assert.IsTrue(data.RepositoryStatus.Get(metaFileName, out metaStatusEntry));
		Assert.AreEqual(FileStatus.DeletedFromIndex,metaStatusEntry.Status);
		Assert.IsFalse(File.Exists(metaFilePath));
		gitManager.Repository.Commit("Removed meta", signature, signature);
		gitManager.Repository.Checkout(addedMetaCommit,new CheckoutOptions()
		{
			CheckoutNotifyFlags = CheckoutNotifyFlags.Updated,
			OnCheckoutNotify = gitManager.CheckoutNotifyHandler
		});
		Assert.IsTrue(File.Exists(metaFilePath));
		Assert.IsTrue(Directory.Exists(GitManager.AssetPathFromMeta(metaFilePath)));
	}
}
