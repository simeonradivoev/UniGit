using System.Collections.Generic;
using System.Diagnostics;
using JetBrains.Annotations;
using LibGit2Sharp;

namespace UniGit.Adapters
{
	public abstract class BaseExternalAdapter : IExternalAdapter
	{
		protected readonly GitManager gitManager;
		protected readonly GitSettingsJson gitSettings;

		protected BaseExternalAdapter(GitManager gitManager,GitSettingsJson gitSettings)
		{
			this.gitManager = gitManager;
			this.gitSettings = gitSettings;
		}

		public abstract bool Push();
		public abstract bool Pull();
		public abstract bool Reset(Commit commit);
		public abstract bool Merge();
		public abstract bool Commit(string message);
		public abstract bool Fetch(string remote);
		public abstract bool Conflict(string path);
		public abstract bool Diff(string path);
		public abstract bool Diff(string path, string path2);
		public abstract bool Diff(string path, Commit end);
		public abstract bool Diff(string path, Commit start, Commit end);
		public abstract bool Revert(IEnumerable<string> paths);
		public abstract bool Blame(string path);
		public abstract bool Switch();

		[StringFormatMethod("parametersFormat")]
		public bool CallProcess(string name, string parametersFormat, params object[] arg)
		{
			return CallProcess(name, string.Format(parametersFormat, arg));
		}

		public bool CallProcess(string name, string parameters)
		{
			var fullPath = GitExternalManager.GetFullPath(name);

			if (fullPath != null)
			{
				var startInfo = new ProcessStartInfo
				{
					CreateNoWindow = false,
					UseShellExecute = false,
					FileName = fullPath,
					WorkingDirectory = gitManager.GetCurrentRepoPath(),
					WindowStyle = ProcessWindowStyle.Hidden,
					RedirectStandardOutput = true,
					Arguments = parameters
				};

				try
                {
                    // Start the process with the info we specified.
					// Call WaitForExit and then the using statement will close.
                    using var exeProcess = Process.Start(startInfo);
                    if (exeProcess == null) return false;
                    exeProcess.WaitForExit();
                    return true;
                }
				catch
				{
					return false;
				}
			}
			return false;
		}
	}
}