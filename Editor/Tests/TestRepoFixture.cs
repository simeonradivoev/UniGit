using System;
using LibGit2Sharp;
using NUnit.Framework;
using UniGit;
using UniGit.Settings;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;

public class TestRepoFixture
{
	protected Signature signature;
	protected GitManager gitManager;
	protected GitCallbacks gitCallbacks;
    protected InjectionHelper injectionHelper;
	protected UniGitData data;
	protected GitSettingsJson gitSettings;

	[SetUp]
	public void Setup()
	{
	    injectionHelper = new InjectionHelper();
	    injectionHelper.Bind<UniGitPaths>().FromInstance(new UniGitPaths(@"D:\Test_Repo", @"D:\Test_Repo"));
	    injectionHelper.Bind<GitSettingsJson>().FromInstance(new GitSettingsJson
	    {
		    Threading = 0
	    });

        injectionHelper.Bind<GitCallbacks>();
		injectionHelper.Bind<IGitPrefs>().To<GitPrefs>();
		injectionHelper.Bind<GitAsyncManager>();
		injectionHelper.Bind<GitManager>();
		injectionHelper.Bind<GitReflectionHelper>();
		injectionHelper.Bind<GitOverlay>();
		injectionHelper.Bind<GitSettingsManager>();
		injectionHelper.Bind<IGitResourceManager>().To<GitResourceManagerMock>();
		injectionHelper.Bind<ILogger>().FromInstance(Debug.unityLogger);
		injectionHelper.Bind<UniGitData>();
		injectionHelper.Bind<GitInitializer>();

		gitManager = injectionHelper.GetInstance<GitManager>();
		injectionHelper.GetInstance<GitInitializer>().InitializeRepository();
		gitCallbacks = injectionHelper.GetInstance<GitCallbacks>();
        signature = new Signature("Test", "Test@Test.com", DateTime.Now);
		data = injectionHelper.GetInstance<UniGitData>();

		EditorApplication.update += gitCallbacks.IssueEditorUpdate;

		gitCallbacks.IssueEditorUpdate();

		injectionHelper.CreateNonLazy();
	}

	[TearDown]
	public void Teardown()
	{
		EditorApplication.update -= gitCallbacks.IssueEditorUpdate;
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