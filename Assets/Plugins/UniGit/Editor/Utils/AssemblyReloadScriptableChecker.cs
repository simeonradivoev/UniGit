using System;
using UnityEngine;

namespace UniGit.Utils
{
	public class AssemblyReloadScriptableChecker : ScriptableObject
	{
		internal Action OnBeforeReloadAction;

		private void OnDisable()
		{
			if(OnBeforeReloadAction != null) OnBeforeReloadAction.Invoke();
		}
	}
}