using System.Security;

namespace UniGit.Adapters
{
	public interface ICredentialsAdapter
	{
		string FormatUrl(string url);
		void DeleteCredentials(string url);
		bool SaveUsername(string url, string username);
		bool LoadUsername(string url, out string username);
		bool LoadPassword(string url, out SecureString password);
		bool SavePassword(string url,string user, SecureString password,bool createMissing);
		bool Exists(string url);
		bool Exists(string url,string username);
	}
}