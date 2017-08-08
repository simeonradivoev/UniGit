using System;
using JetBrains.Annotations;

namespace UniGit.Utils
{
	[AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method)]
    [MeansImplicitUse(ImplicitUseKindFlags.Assign)]
	public class UniGitInject : Attribute
	{
		
	}
}