using System;
using System.Collections.Generic;
using System.Diagnostics;
using LibGit2Sharp;
using UniGit;

namespace Assets.Plugins.UniGit.Editor.Hooks
{
	public class GitLfsPrePushHook : GitPushHookBase
	{
		public override bool OnPrePush(IEnumerable<PushUpdate> updates)
		{
			using (var process = new Process())
			{
				process.StartInfo.FileName = "git-lfs";
				process.StartInfo.Arguments = "pre-push origin";
				process.StartInfo.WorkingDirectory = GitManager.RepoPath;
				process.StartInfo.CreateNoWindow = false;
				process.StartInfo.UseShellExecute = false;
				process.StartInfo.RedirectStandardInput = true;
				process.StartInfo.RedirectStandardOutput = true;
				process.StartInfo.RedirectStandardError = true;
				process.Start();

				foreach (var update in updates)
				{
					string value = string.Format("{0} {1} {2} {3}\n", update.SourceRefName, update.SourceObjectId.Sha, update.DestinationRefName, update.DestinationObjectId.Sha);
					process.StandardInput.Write(value);
					UnityEngine.Debug.Log(value);
				}

				process.StandardInput.Write("\0");
				process.StandardInput.Flush();
				process.StandardInput.Close();
				process.WaitForExit();

				string output = process.StandardOutput.ReadToEnd();
				string outputErr = process.StandardError.ReadToEnd();
				if (!string.IsNullOrEmpty(output)) Console.WriteLine("git-lfs pre-push results: " + output);
				if (!string.IsNullOrEmpty(outputErr))
				{
					UnityEngine.Debug.Log("git-lfs pre-push error results: " + outputErr);
					return false;
				}
			}
			return true;
		}
	}
}