using System.IO;
using LibGit2Sharp;
using UnityEngine;
using NUnit.Framework;
using UniGit;
using UniGit.Status;
using Assert = UnityEngine.Assertions.Assert;

public class CallbackTests : TestRepoFixture
{
	private int updateRepositoryCalled;
	private int onRepositoryLoadedCalled;

	[SetUp]
	public void SetupCallbacks()
	{
		updateRepositoryCalled = 0;
		onRepositoryLoadedCalled = 0;
		gitManager.Callbacks.OnRepositoryLoad += OnRepositoryLoad;
		gitManager.Callbacks.UpdateRepository += RepositoryUpdate;
	}

	[TearDown]
	public void TeardownCallbacks()
	{
		gitManager.Callbacks.OnRepositoryLoad -= OnRepositoryLoad;
		gitManager.Callbacks.UpdateRepository -= RepositoryUpdate;
	}

	private void OnRepositoryLoad(Repository repository)
	{
		onRepositoryLoadedCalled++;
	}

	private void RepositoryUpdate(GitRepoStatus status,string[] paths)
	{
		updateRepositoryCalled++;
	}

	private void ForceGitUpdate()
	{
		gitManager.Callbacks.IssueEditorUpdate();
	}

	[Test]
	public void UpdateRepositorySingleThreaded_OnAssetAddedShouldCallUpdateRepository_UpdateRepositoryCalled()
	{
		File.WriteAllText(Path.Combine(gitManager.RepoPath, "test.txt"), "Text Asset");
		string[] outputs = { Path.Combine(gitManager.RepoPath, "test.txt") };
		injectionHelper.GetInstance<GitCallbacks>().IssueOnWillSaveAssets(outputs, ref outputs);
		ForceGitUpdate();
		File.Delete(Application.dataPath + "/test.txt");
		Assert.AreEqual(updateRepositoryCalled, 1);
	}

	[Test]
	public void UpdateRepositorySingleThreaded_OnAssetRemovedShouldCallUpdateRepository_UpdateRepositoryCalled()
	{
		File.WriteAllText(Path.Combine(gitManager.RepoPath, "test.txt"), "Text Asset");
		string[] outputs = { Path.Combine(gitManager.RepoPath, "test.txt") };
		File.Delete(Application.dataPath + "/test.txt");
		injectionHelper.GetInstance<GitCallbacks>().IssueOnPostprocessDeletedAssets(outputs);
		ForceGitUpdate();
		Assert.AreEqual(updateRepositoryCalled, 1);
	}

	[Test]
	public void OnRepositoryLoad_OnRepositoryDirtyShouldCallRepositoryLoad_OnRepositoryLoadCalled()
	{
		gitManager.MarkDirty(true);
		ForceGitUpdate();
		Assert.AreEqual(onRepositoryLoadedCalled,1);
	}
}
