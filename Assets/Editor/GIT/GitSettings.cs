using UnityEngine;
using System.Collections;

namespace UniGit
{
	public class GitSettings : ScriptableObject
	{
		[SerializeField] public bool AutoStage = true;
	}
}