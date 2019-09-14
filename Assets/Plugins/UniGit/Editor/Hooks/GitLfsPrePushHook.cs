using System.Collections.Generic;
using System.Diagnostics;
using LibGit2Sharp;
using UniGit;
using UniGit.Utils;
using UnityEngine;

namespace Assets.Plugins.UniGit.Editor.Hooks
{
	public class GitLfsPrePushHook : GitPushHookBase
	{
		private readonly GitLfsManager lfsManager;
		private readonly ILogger logger;

		[UniGitInject]
		public GitLfsPrePushHook(GitManager gitManager, GitLfsManager lfsManager,ILogger logger) : base(gitManager)
		{
			this.lfsManager = lfsManager;
			this.logger = logger;
		}

		public override bool OnPrePush(IEnumerable<PushUpdate> updates)
		{
			if (!lfsManager.Installed || !lfsManager.CheckInitialized()) return true;

			using (var process = new Process())
			{
				process.StartInfo.FileName = "git-lfs";
				process.StartInfo.Arguments = "pre-push origin";
				process.StartInfo.WorkingDirectory = gitManager.GetCurrentRepoPath();
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
				if (!string.IsNullOrEmpty(output)) logger.LogFormat(LogType.Log,"git-lfs pre-push results: {0}",output);
				if (!string.IsNullOrEmpty(outputErr))
				{
					logger.LogFormat(LogType.Error,"git-lfs pre-push error results: {0}",outputErr);
					return false;
				}
			}
			return true;
		}
	}
}