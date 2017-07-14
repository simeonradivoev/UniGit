using System;
using System.Collections;
using System.IO;
using LibGit2Sharp;
using NUnit.Framework;
using UniGit;
using UniGit.Settings;

public class TestRepoFixture
{
	protected GitManager gitManager;
	protected GitSettingsJson gitSettings;
	protected GitCallbacks gitCallbacks;
	protected Signature signature;
	protected GitPrefs gitPrefs;

	[SetUp]
	public void Setup()
	{
		string repoPath = @"D:\Test_Repo";
		gitSettings = new GitSettingsJson();
		gitSettings.GitStatusMultithreaded = false;
		gitCallbacks = new GitCallbacks();
		gitPrefs = new GitPrefs();
		gitManager = new GitManager(repoPath, gitCallbacks, gitSettings, gitPrefs);
		signature = new Signature("Test", "Test@Test.com", DateTime.Now);
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