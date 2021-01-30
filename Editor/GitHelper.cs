using System.Diagnostics;

namespace UniGit
{
	public static class GitHelper
	{
		public static string RunExeOutput(string repoPath,string exe, string arguments, string input, bool hideWindow = true)
        {
            using var process = new Process
            {
                StartInfo =
                {
                    FileName = exe,
                    Arguments = arguments,
                    WorkingDirectory = repoPath,
                    RedirectStandardInput = input != null,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = hideWindow
                }
            };
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