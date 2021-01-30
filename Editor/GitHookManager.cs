using System.Collections.Generic;
using LibGit2Sharp;
using UniGit.Hooks;
using UniGit.Utils;

namespace UniGit
{
	public class GitHookManager
	{
		private readonly ICollection<GitPushHookBase> pushHooks;

		[UniGitInject]
		public GitHookManager(ICollection<GitPushHookBase> pushHooks)
		{
			this.pushHooks = pushHooks;
		}

		public bool PrePushHandler(IEnumerable<PushUpdate> updates)
		{
			var continueFlag = true;
			foreach (var hook in pushHooks)
			{
				if (!hook.OnPrePush(updates))
				{
					continueFlag = false;
				}
			}
			return continueFlag;
		}
	}
}