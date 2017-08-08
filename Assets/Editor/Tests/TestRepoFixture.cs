using System;
using LibGit2Sharp;
using NUnit.Framework;
using UniGit;
using UniGit.Settings;
using UniGit.Utils;

public class TestRepoFixture
{
	protected GitManager gitManager;
	protected GitSettingsJson gitSettings;
	protected GitCallbacks gitCallbacks;
	protected Signature signature;
	protected GitPrefs gitPrefs;
    protected InjectionHelper injectionHelper;

	[SetUp]
	public void Setup()
	{
	    injectionHelper = new InjectionHelper();
        string repoPath = @"D:\Test_Repo";
		gitSettings = new GitSettingsJson();
		gitSettings.Threading = 0;
        gitCallbacks = new GitCallbacks();
        gitPrefs = new GitPrefs();
		gitManager = new GitManager(repoPath, gitCallbacks, gitSettings, gitPrefs);
	    gitManager.InitilizeRepository();
        signature = new Signature("Test", "Test@Test.com", DateTime.Now);

	    injectionHelper.Bind<GitSettingsJson>().FromInstance(gitSettings);
        injectionHelper.Bind<GitCallbacks>().FromInstance(gitCallbacks);
	    injectionHelper.Bind<IGitPrefs>().To<GitPrefs>().FromInstance(gitPrefs);
	    injectionHelper.Bind<GitManager>().FromInstance(gitManager);

        gitCallbacks.IssueEditorUpdate();
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