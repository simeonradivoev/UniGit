using System;
using LibGit2Sharp;

namespace UniGit
{
    public static class GitCommands
    {
        public static void Stage(Repository repository, params string[] paths)
        {
            repository.Stage(paths);
        }

        public static void Unstage(Repository repository, string[] paths)
        {
            repository.Unstage(paths);
        }

        internal static void Fetch(Repository repository, string name, FetchOptions fetchOptions)
        {
            repository.Fetch(name,fetchOptions);
        }
    }
}