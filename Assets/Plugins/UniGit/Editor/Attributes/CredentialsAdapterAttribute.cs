using System;

namespace UniGit.Attributes
{
	[AttributeUsage(AttributeTargets.Class)]
	public class CredentialsAdapterAttribute : Attribute
	{
		public string Name { get; private set; }
		public string Id { get; private set; }

		public CredentialsAdapterAttribute(string id,string name)
		{
			Name = name;
			Id = id;
		}
	}
}