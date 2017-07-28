using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Plugins.UniGit.Editor.Hooks;
using LibGit2Sharp;
using UniGit.Utils;

namespace UniGit
{
	public class GitHookManager
	{
		private readonly List<GitPushHookBase> pushHooks;
		private readonly InjectionHelper injectionHelper;

		[UniGitInject]
		public GitHookManager(GitManager gitManager,GitLfsManager lfsManager)
		{
			pushHooks = new List<GitPushHookBase>();
			injectionHelper = new InjectionHelper();
			injectionHelper.Bind<GitManager>().FromInstance(gitManager);
			injectionHelper.Bind<GitLfsManager>().FromInstance(lfsManager);

			var hookTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes().Where(t => t.IsSubclassOf(typeof(GitPushHookBase))));
			foreach (var hookType in hookTypes)
			{
				var hook = injectionHelper.CreateInstance(hookType);
				pushHooks.Add((GitPushHookBase)hook);
			}
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