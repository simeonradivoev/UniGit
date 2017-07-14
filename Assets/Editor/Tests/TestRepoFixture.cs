using System.Collections;
using NUnit.Framework;
using UniGit;

namespace Assets.Editor.Tests
{
	public class TestRepoFixture
	{
		protected GitManager gitManager;
		protected GitSettingsJson gitSettings;
		protected GitCallbacks gitCallbacks;

		[SetUp]
		public void Setup()
		{
			gitSettings = new GitSettingsJson();
			gitCallbacks = new GitCallbacks();
			gitManager = new GitManager(@"D:\Test_Repo", gitCallbacks, gitSettings);
		}

		[TearDown]
		public void Teardown()
		{
			gitManager.Dispose();
			gitManager.DeleteRepository();
		}
	}
}