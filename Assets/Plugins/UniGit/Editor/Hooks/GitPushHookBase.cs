using System.Collections.Generic;
using LibGit2Sharp;

namespace Assets.Plugins.UniGit.Editor.Hooks
{
	public abstract class GitPushHookBase
	{
		public abstract bool OnPrePush(IEnumerable<PushUpdate> updates);
	}
}