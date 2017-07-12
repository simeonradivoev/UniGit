using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Assets.Plugins.UniGit.Editor.Hooks;
using LibGit2Sharp;

namespace UniGit
{
	public static class GitHookManager
	{
		private static GitPushHookBase[] pushHooks;

		internal static void Load(GitManager gitManager)
		{
			var managerParams = new object[] {gitManager};
			pushHooks = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes().Where(t => t.IsSubclassOf(typeof (GitPushHookBase)))).Select(t => Activator.CreateInstance(t, managerParams)).Cast<GitPushHookBase>().ToArray();
		}

		public static bool PrePushHandler(IEnumerable<PushUpdate> updates)
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