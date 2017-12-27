using System;
using LibGit2Sharp;
using NUnit.Framework;
using UniGit;
using UniGit.Settings;
using UniGit.Utils;

public class TestRepoFixture
{
	protected Signature signature;
	protected GitManager gitManager;
    protected InjectionHelper injectionHelper;

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

		gitManager = injectionHelper.GetInstance<GitManager>();
		gitManager.InitilizeRepository();
        signature = new Signature("Test", "Test@Test.com", DateTime.Now);

		injectionHelper.GetInstance<GitCallbacks>().IssueEditorUpdate();
	}

	[TearDown]
	public void Teardown()
	{
		gitManager.Dispose();
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