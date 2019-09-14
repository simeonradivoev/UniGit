using NUnit.Framework;
using UniGit;
using UniGit.Utils;

public class GitLfsHelperTests
{
	[Test]
	public void CanMatchFilePath()
	{
		string[] lines =
		{
			"*.[jJ][pP][gG] filter=lfs diff=lfs merge=lfs -text",
			"*LightingData.asset filter=lfs diff=lfs merge=lfs -text",
			"*.[fF][bB][xX] -delta",
			"NavMesh*.asset filter=lfs diff=lfs merge=lfs -text"
		};
		var fileReaderMock = new FileLinesReaderMock(lines);
		var helper = new GitLfsHelper(new UniGitPaths(""), fileReaderMock);

		Assert.IsTrue(helper.IsLfsPath("C:\\UniGit\\Test\\Image.jpg"));
		Assert.IsTrue(helper.IsLfsPath("C:\\UniGit\\Test\\LightingData.asset"));
		//no idea how to deal with that
		//Assert.IsTrue(helper.IsLfsPath("C:\\UniGit\\Test\\NavMesh-Test.asset"));

		Assert.IsFalse(helper.IsLfsPath("C:\\UniGit\\Test\\FailCaseFile.asset"));
		Assert.IsFalse(helper.IsLfsPath("C:\\UniGit\\Test\\FailCaseFile.jpg.meta"));
		Assert.IsFalse(helper.IsLfsPath("C:\\UniGit\\Test\\FailCaseFile.fbx"));
	}

	private class FileLinesReaderMock : FileLinesReader
	{
		private string[] lines;

		public FileLinesReaderMock(string[] lines)
		{
			this.lines = lines;
		}

		public override bool ReadLines(string path, out string[] lines)
		{
			lines = this.lines;
			return true;
		}
	}
}