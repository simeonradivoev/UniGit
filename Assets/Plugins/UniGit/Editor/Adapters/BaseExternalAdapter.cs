using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using JetBrains.Annotations;
using LibGit2Sharp;

namespace UniGit.Adapters
{
	public abstract class BaseExternalAdapter : IExternalAdapter
	{
		protected readonly GitManager gitManager;

		protected BaseExternalAdapter(GitManager gitManager)
		{
			this.gitManager = gitManager;
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
		public bool CallProccess(string name, string parametersFormat, params object[] arg)
		{
			return CallProccess(name, string.Format(parametersFormat, arg));
		}

		public bool CallProccess(string name, string parameters)
		{
			string fullPath = GitExternalManager.GetFullPath(name);

			if (fullPath != null)
			{
				ProcessStartInfo startInfo = new ProcessStartInfo
				{
					CreateNoWindow = false,
					UseShellExecute = false,
					FileName = fullPath,
					WorkingDirectory = gitManager.RepoPath,
					WindowStyle = ProcessWindowStyle.Hidden,
					RedirectStandardOutput = true,
					Arguments = parameters
				};

				try
				{
					// Start the process with the info we specified.
					// Call WaitForExit and then the using statement will close.
					using (Process exeProcess = Process.Start(startInfo))
					{
						if (exeProcess == null) return false;
						exeProcess.WaitForExit();
						return true;
					}
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