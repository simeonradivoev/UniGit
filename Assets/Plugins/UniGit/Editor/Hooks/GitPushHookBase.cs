using System.Collections.Generic;
using LibGit2Sharp;
using UniGit;

namespace Assets.Plugins.UniGit.Editor.Hooks
{
	public abstract class GitPushHookBase
	{
		protected GitManager gitManager;
		public abstract bool OnPrePush(IEnumerable<PushUpdate> updates);

		protected GitPushHookBase(GitManager gitManager)
		{
			this.gitManager = gitManager;
		}
	}
}