using System;
using LibGit2Sharp;
using NUnit.Framework;
using UniGit;
using UniGit.Settings;
using UniGit.Utils;
using UnityEngine;

public class TestRepoFixture
{
	protected Signature signature;
	protected GitManager gitManager;
	protected GitCallbacks gitCallbacks;
    protected InjectionHelper injectionHelper;
	protected UniGitData data;

	[SetUp]
	public void Setup()
	{
	    injectionHelper = new InjectionHelper();
		injectionHelper.Bind<string>().WithId("repoPath").FromInstance(@"D:\Test_Repo");
		injectionHelper.Bind<GitSettingsJson>().FromInstance(new GitSettingsJson {Threading = 0});
		injectionHelper.Bind<GitCallbacks>();
		injectionHelper.Bind<IGitPrefs>().To<GitPrefs>();
		injectionHelper.Bind<GitAsyncManager>();
		injectionHelper.Bind<GitManager>();
		injectionHelper.Bind<GitReflectionHelper>();
		injectionHelper.Bind<GitOverlay>();
		injectionHelper.Bind<IGitResourceManager>().To<GitResourceManagerMock>();
		injectionHelper.Bind<ILogger>().FromInstance(Debug.unityLogger);
		injectionHelper.Bind<UniGitData>();

		gitManager = injectionHelper.GetInstance<GitManager>();
		gitManager.InitializeRepository();
		gitCallbacks = injectionHelper.GetInstance<GitCallbacks>();
        signature = new Signature("Test", "Test@Test.com", DateTime.Now);
		data = injectionHelper.GetInstance<UniGitData>();

		injectionHelper.GetInstance<GitCallbacks>().IssueEditorUpdate();
	}

	[TearDown]
	public void Teardown()
	{
		if(data != null) UnityEngine.Object.DestroyImmediate(data);
		injectionHelper.Dispose();
		try
		{
			gitManager.DeleteRepository();
		}
		catch
		{
			// ignored
		}
	}
}