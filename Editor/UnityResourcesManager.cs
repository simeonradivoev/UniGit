using System.Collections.Generic;
using System.IO;
using UniGit.Utils;
using UnityEngine;

namespace UniGit
{
    public class UnityResourcesManager : IGitResourceManager
    {
        private readonly Dictionary<string, Texture2D> textures;
        private readonly ILogger logger;

        [UniGitInject]
        public UnityResourcesManager(ILogger logger)
        {
            this.logger = logger;
            textures = new Dictionary<string, Texture2D>();
        }

        public Texture2D GetTexture(string name, bool throwError = true)
        {
            if (textures.TryGetValue(name, out var tex))
            {
                return tex;
            }

            tex = LoadUniGitAsset<Texture2D>($"Editor/Icons/{name}.png");
            if (tex)
            {
                textures.Add(name, tex);
                return tex;
            }

            if (throwError)
            {
                logger.LogFormat(LogType.Error, "Could not find texture with key: {0}", name);
            }
            return null;
        }

        public T LoadUniGitAsset<T>(string path) where T : Object
        {
            var pathWithoutExtension = Path.ChangeExtension(path,null);
            return Resources.Load<T>(pathWithoutExtension);
        }
    }
}