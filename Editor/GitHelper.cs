using System.Diagnostics;

namespace UniGit
{
	public static class GitHelper
	{
		public static string RunExeOutput(string repoPath,string exe, string arguments, string input, bool hideWindow = true)
		{
			using (Process process = new Process())
			{
				process.StartInfo.FileName = exe;
				process.StartInfo.Arguments = arguments;
				process.StartInfo.WorkingDirectory = repoPath;
				process.StartInfo.RedirectStandardInput = input != null;
				process.StartInfo.RedirectStandardOutput = true;
				process.StartInfo.UseShellExecute = false;
				process.StartInfo.CreateNoWindow = hideWindow;
				process.Start();
				if (input != null)
				{
					process.StandardInput.WriteLine(input);
					process.StandardInput.Flush();
					process.StandardInput.Close();
				}
				process.WaitForExit();

				return process.StandardOutput.ReadToEnd();
			}
		}
	}
}