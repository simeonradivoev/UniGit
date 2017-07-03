namespace UniGit.Adapters
{
	public interface ICredentialsAdapter
	{
		string FormatUrl(string url);
		void DeleteCredentials(string url);
		bool SaveUsername(string url, string username);
		bool LoadPassword(string url, ref string password);
		bool SavePassword(string url,string user, string password,bool createMissing);
	}
}