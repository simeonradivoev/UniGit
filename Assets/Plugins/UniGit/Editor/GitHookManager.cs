using System.Collections.Generic;
using Assets.Plugins.UniGit.Editor.Hooks;
using LibGit2Sharp;
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
			bool cantinue = true;
			foreach (var hook in pushHooks)
			{
				if (!hook.OnPrePush(updates))
				{
					cantinue = false;
				}
			}
			return cantinue;
		}
	}
}