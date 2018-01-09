using UniGit;
using UnityEngine;

public class GitResourceManagerMock : IGitResourceManager
{
	public Texture2D GetTexture(string name, bool throwError = true)
	{
		return Texture2D.whiteTexture;
	}
}