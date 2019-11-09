using System.Collections;
using System.IO;
using LibGit2Sharp;
using UnityEngine;
using NUnit.Framework;
using UniGit;
using UniGit.Status;
using UnityEngine.TestTools;
using Assert = UnityEngine.Assertions.Assert;

public class CallbackTests : TestRepoFixture
{
	private int updateRepositoryCalled;
	private int onRepositoryLoadedCalled;
	private int editorUpdatesCalled;

	[SetUp]
	public void SetupCallbacks()
	{
		updateRepositoryCalled = 0;
		onRepositoryLoadedCalled = 0;
		editorUpdatesCalled = 0;
		gitCallbacks.OnRepositoryLoad += OnRepositoryLoad;
		gitCallbacks.UpdateRepository += RepositoryUpdate;
		gitCallbacks.EditorUpdate += OnEditorUpdate;
	}

	[TearDown]
	public void TeardownCallbacks()
	{
		gitCallbacks.OnRepositoryLoad -= OnRepositoryLoad;
		gitCallbacks.UpdateRepository -= RepositoryUpdate;
	}

	private void OnEditorUpdate()
	{
		editorUpdatesCalled++;
	}

	private void OnRepositoryLoad(Repository repository)
	{
		onRepositoryLoadedCalled++;
	}

	private void RepositoryUpdate(GitRepoStatus status,string[] paths)
	{
		updateRepositoryCalled++;
	}

	[UnityTest]
	public IEnumerator UpdateRepositorySingleThreaded_OnAssetAddedShouldCallUpdateRepository_UpdateRepositoryCalled()
	{
		File.WriteAllText(Path.Combine(gitManager.GetCurrentRepoPath(), "test.txt"), "Text Asset");
		string[] outputs = { Path.Combine(gitManager.GetCurrentRepoPath(), "test.txt") };
		injectionHelper.GetInstance<GitCallbacks>().IssueOnWillSaveAssets(outputs, ref outputs);
		yield return null;
		File.Delete(Application.dataPath + "/test.txt");
		Assert.AreEqual(updateRepositoryCalled, 1);
	}

	[UnityTest]
	public IEnumerator UpdateRepositorySingleThreaded_OnAssetRemovedShouldCallUpdateRepository_UpdateRepositoryCalled()
	{
		File.WriteAllText(Path.Combine(gitManager.GetCurrentRepoPath(), "test.txt"), "Text Asset");
		string[] outputs = { Path.Combine(gitManager.GetCurrentRepoPath(), "test.txt") };
		File.Delete(Application.dataPath + "/test.txt");
		gitCallbacks.IssueOnPostprocessDeletedAssets(outputs);
		yield return null;
		Assert.AreEqual(updateRepositoryCalled, 1);
	}

	[UnityTest]
	public IEnumerator OnRepositoryLoad_OnRepositoryDirtyShouldCallRepositoryLoad_OnRepositoryLoadCalled()
	{
		Assert.IsFalse(gitManager.IsUpdating);
		gitManager.MarkDirty(true);
		yield return null;
		Assert.AreEqual(1,onRepositoryLoadedCalled);
	}
}
