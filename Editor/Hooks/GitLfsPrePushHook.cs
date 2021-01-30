using System.Collections.Generic;
using System.Diagnostics;
using LibGit2Sharp;
using UniGit.Utils;
using UnityEngine;

namespace UniGit.Hooks
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

            using var process = new Process
            {
                StartInfo =
                {
                    FileName = "git-lfs",
                    Arguments = "pre-push origin",
                    WorkingDirectory = gitManager.GetCurrentRepoPath(),
                    CreateNoWindow = false,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            process.Start();

            foreach (var update in updates)
            {
                var value =
                    $"{update.SourceRefName} {update.SourceObjectId.Sha} {update.DestinationRefName} {update.DestinationObjectId.Sha}\n";
                process.StandardInput.Write(value);
                UnityEngine.Debug.Log(value);
            }

            process.StandardInput.Write("\0");
            process.StandardInput.Flush();
            process.StandardInput.Close();
            process.WaitForExit();

            var output = process.StandardOutput.ReadToEnd();
            var outputErr = process.StandardError.ReadToEnd();
            if (!string.IsNullOrEmpty(output)) logger.LogFormat(LogType.Log,"git-lfs pre-push results: {0}",output);
            if (string.IsNullOrEmpty(outputErr)) return true;
            logger.LogFormat(LogType.Error,"git-lfs pre-push error results: {0}",outputErr);
            return false;

        }
	}
}