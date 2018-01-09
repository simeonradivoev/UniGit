using System;
using LibGit2Sharp;
using NUnit.Framework;
using NUnit.Framework.Internal;
using UniGit;
using UniGit.Settings;
using UniGit.Utils;
using UnityEngine;

public class TestRepoFixture
{
	protected Signature signature;
	protected GitManager gitManager;
    protected InjectionHelper injectionHelper;
	protected UniGitData data;

	[SetUp]
	public void Setup()
	{
		data = ScriptableObject.CreateInstance<UniGitData>();
		data.hideFlags = HideFlags.HideAndDontSave;

	    injectionHelper = new InjectionHelper();
		injectionHelper.Bind<string>().WithId("repoPath").FromInstance(@"D:\Test_Repo");
		injectionHelper.Bind<GitSettingsJson>().FromInstance(new GitSettingsJson {Threading = 0});
		injectionHelper.Bind<GitCallbacks>();
		injectionHelper.Bind<IGitPrefs>().To<GitPrefs>();
		injectionHelper.Bind<GitAsyncManager>();
		injectionHelper.Bind<GitManager>();
		injectionHelper.Bind<UniGitData>().FromInstance(data);
		injectionHelper.Bind<GitReflectionHelper>();
		injectionHelper.Bind<GitOverlay>();
		injectionHelper.Bind<IGitResourceManager>().To<GitResourceManagerMock>();

		gitManager = injectionHelper.GetInstance<GitManager>();
		gitManager.InitilizeRepository();
        signature = new Signature("Test", "Test@Test.com", DateTime.Now);

		injectionHelper.GetInstance<GitCallbacks>().IssueEditorUpdate();
	}

	[TearDown]
	public void Teardown()
	{
		UnityEngine.Object.DestroyImmediate(data);
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