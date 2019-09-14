using System.Collections.Generic;
using LibGit2Sharp;

namespace UniGit
{
    public static class GitCommands
    {
        public static void Stage(Repository repository, params string[] paths)
        {
           Stage(repository,(IEnumerable<string>)paths);
        }

	    public static void Stage(Repository repository, IEnumerable<string> paths)
	    {
            repository.Stage(paths);
        }

        public static void Unstage(Repository repository, IEnumerable<string> paths)
        {
            repository.Unstage(paths);
        }

        public static void Checkout(Repository repository,Branch branch, CheckoutOptions options)
        {
            repository.Checkout(branch, options);
        }

        public static void Checkout(Repository repository, Commit commit, CheckoutOptions options)
        {
            repository.Checkout(commit, options);
        }

        public static MergeResult Pull(Repository repository,Signature signature,PullOptions pullOptions)
        {
            return repository.Network.Pull(signature, pullOptions);
        }

        internal static void Fetch(Repository repository, Remote remote, FetchOptions fetchOptions)
        {
            repository.Network.Fetch(remote, fetchOptions);
        }
    }
}