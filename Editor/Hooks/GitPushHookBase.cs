using System.Collections.Generic;
using LibGit2Sharp;
using UniGit;
using UniGit.Utils;

namespace UniGit.Hooks
{
	public abstract class GitPushHookBase
	{
		protected readonly GitManager gitManager;
		public abstract bool OnPrePush(IEnumerable<PushUpdate> updates);

		[UniGitInject]
		protected GitPushHookBase(GitManager gitManager)
		{
			this.gitManager = gitManager;
		}
	}
}