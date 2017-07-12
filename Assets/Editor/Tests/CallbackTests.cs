using System.IO;
using System.Threading;
using LibGit2Sharp;
using UnityEngine;
using UnityEditor;
using NUnit.Framework;
using UniGit;
using UniGit.Status;
using Assert = UnityEngine.Assertions.Assert;

public class CallbackTests
{
	private int updateRepositoryCalled;
	private int onRepositoryLoadedCalled;
	private bool oldMultiThreaded;
	private GitManager gitManager;

	[SetUp]
	public void Setup()
	{
		gitManager = GitManager.Instance;
		updateRepositoryCalled = 0;
		onRepositoryLoadedCalled = 0;
		GitCallbacks.OnRepositoryLoad += OnRepositoryLoad;
		GitCallbacks.UpdateRepository += RepositoryUpdate;
		oldMultiThreaded = gitManager.Settings.GitStatusMultithreaded;
		gitManager.Settings.GitStatusMultithreaded = false;
	}

	[TearDown]
	public void Teardown()
	{
		GitCallbacks.OnRepositoryLoad -= OnRepositoryLoad;
		GitCallbacks.UpdateRepository -= RepositoryUpdate;
		gitManager.Settings.GitStatusMultithreaded = oldMultiThreaded;
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
		GitCallbacks.IssueEditorUpdate();
	}

	[Test]
	public void UpdateRepositorySingleThreaded_OnAssetAddedShouldCallUpdateRepository_UpdateRepositoryCalled()
	{
		File.WriteAllText(Application.dataPath + "/test.txt", "Text Asset");
		AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
		ForceGitUpdate();
		File.Delete(Application.dataPath + "/test.txt");
		AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
		ForceGitUpdate();
		Assert.AreEqual(updateRepositoryCalled, 2);
	}

	[Test]
	public void UpdateRepositorySingleThreaded_OnAssetDatabaseRefreshEmptyUpdateNotCalled_UpdateRepositoryNotCalled()
	{
		AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
		ForceGitUpdate();
		Assert.AreEqual(updateRepositoryCalled, 0);
	}

	[Test]
	public void OnRepositoryLoad_OnRepositoryDirtyShouldCallRepositoryLoad_OnRepositoryLoadCalled()
	{
		gitManager.MarkDirty(true);
		ForceGitUpdate();
		Assert.AreEqual(onRepositoryLoadedCalled,1);
	}
}
