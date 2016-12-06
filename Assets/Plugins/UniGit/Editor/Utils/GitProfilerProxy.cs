using UnityEngine;
using UnityEngine.Profiling;

namespace UniGit.Utils
{
	public class GitProfilerProxy
	{
		public static void BeginSample(string name)
		{
#if UNITY_EDITOR
			Profiler.BeginSample(name);
#endif
		}

		public static void BeginSample(string name,Object target)
		{
#if UNITY_EDITOR
			Profiler.BeginSample(name, target);
#endif
		}

		public static void EndSample()
		{
#if UNITY_EDITOR
			Profiler.EndSample();
#endif
		}
	}
}