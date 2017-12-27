using System;
using JetBrains.Annotations;
using UnityEngine;

namespace UniGit.Utils
{
	public class AssemblyReloadScriptableChecker : ScriptableObject
	{
		internal Action OnBeforeReloadAction;

		[UsedImplicitly]
		private void OnDisable()
		{
			if(OnBeforeReloadAction != null) OnBeforeReloadAction.Invoke();
		}
	}
}