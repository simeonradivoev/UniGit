﻿using System.Collections.Generic;
using UniGit.Utils;
using UnityEngine;

namespace UniGit
{
	public class GitResourceManager : IGitResourceManager
	{
		private readonly Dictionary<string,Texture2D> textures;
		private readonly ILogger logger;

		[UniGitInject]
		public GitResourceManager(ILogger logger)
		{
			this.logger = logger;
			textures = new Dictionary<string, Texture2D>();
		}

		public Texture2D GetTexture(string name, bool throwError = true)
		{
			Texture2D tex;
			if (!textures.TryGetValue(name, out tex))
			{
				tex = Resources.Load<Texture2D>($"Icons/{name}");
				if (tex)
				{
					textures.Add(name, tex);
					return tex;
                }
			}

			if (throwError)
			{
				logger.LogFormat(LogType.Error,"Could not find texture with key: {0}",name);
			}
			return null;
		}
	}
}