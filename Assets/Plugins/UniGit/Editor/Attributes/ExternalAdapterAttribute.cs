using System;

namespace UniGit.Attributes
{
	[AttributeUsage(AttributeTargets.Class)]
	public class ExternalAdapterAttribute : Attribute
	{
		public string[] ProcessNames { get; private set; }
		public string FriendlyName { get; private set; }

		public ExternalAdapterAttribute(string friendlyName,params string[] processNames)
		{
			ProcessNames = processNames;
			FriendlyName = friendlyName;
		}
	}
}