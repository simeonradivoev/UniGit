using System.IO;
using System.Threading;
using LibGit2Sharp;
using UnityEngine;
using UnityEditor;
using NUnit.Framework;
using UniGit;
using UniGit.Settings;
using UniGit.Status;
using Assert = UnityEngine.Assertions.Assert;

public class CallbackTests
{
	private int updateRepositoryCalled;
	private int onRepositoryLoadedCalled;
	private GitManager gitManager;

	[SetUp]
	public void Setup()
	{
		var callbacks = new GitCallbacks();
		var settings = new GitSettingsJson()
		{
			Threading = 0
		};
		var prefs = new GitPrefs();
		gitManager = new GitManager(UniGitLoader.GitManager.RepoPath, callbacks, settings, prefs);
		updateRepositoryCalled = 0;
		onRepositoryLoadedCalled = 0;
		gitManager.Callbacks.OnRepositoryLoad += OnRepositoryLoad;
		gitManager.Callbacks.UpdateRepository += RepositoryUpdate;
		gitManager.Settings.Threading = 0;
	}

	[TearDown]
	public void Teardown()
	{
		gitManager.Callbacks.OnRepositoryLoad -= OnRepositoryLoad;
		gitManager.Callbacks.UpdateRepository -= RepositoryUpdate;

		gitManager.Dispose();
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
		File.WriteAllText(Application.dataPath + "/test.txt", "Text Asset");
		AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
		ForceGitUpdate();
		File.Delete(Application.dataPath + "/test.txt");
		AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
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
