using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using LibGit2Sharp;
using UnityEngine;
using Debug = UnityEngine.Debug;
using FilterMode = LibGit2Sharp.FilterMode;

namespace UniGit.Filters
{
	public class GitLfsFilter : Filter
	{
		private readonly Dictionary<string, Process> processes = new Dictionary<string, Process>();
		private readonly Dictionary<string, FilterMode> modes = new Dictionary<string, FilterMode>();
		private readonly GitManager gitManager;
		private readonly GitLfsManager lfsManager;
		private readonly ILogger logger;

		public GitLfsFilter(string name, IEnumerable<FilterAttributeEntry> attributes, GitLfsManager lfsManager, GitManager gitManager,ILogger logger) : base(name, attributes)
		{
			this.gitManager = gitManager;
			this.lfsManager = lfsManager;
			this.logger = logger;
		}

		protected override void Clean(string path, string root, Stream input, Stream output)
		{
			if(!lfsManager.IsEnabled) return;
			try
			{
				Process process;
				FilterMode mode;
				if (!processes.TryGetValue(path,out process))
				{
					logger.LogFormat(LogType.Log,"Could not find lfs process for path: {0} when cleaning",path);
					return;
				}

				if (!modes.TryGetValue(path, out mode))
				{
					logger.LogFormat(LogType.Log,"Could not find lfs filter mode for path: {0} when cleaning",path);
					return;
				}

				if (mode != FilterMode.Clean)
				{
					logger.LogFormat(LogType.Error,"Filter mode mismatch when cleaning for path: {0}",path);
				}

				// write file data to stdin
				input.CopyTo(process.StandardInput.BaseStream);
				input.Flush();
			}
			catch (Exception e)
			{
				logger.Log(LogType.Error,"LFS Clean Error!");
				logger.LogException(e);
			}
		}

		protected override void Complete(string path, string root, Stream output)
		{
			if (!lfsManager.IsEnabled) return;
			try
			{
				Process process;
				FilterMode mode;

				if (!processes.TryGetValue(path, out process))
				{
					throw new Exception("Could not find lfs process for path: " + path);
				}

				if (!modes.TryGetValue(path, out mode))
				{
					throw new Exception("Could not find lfs filter mode for path: " + path);
				}

				try
				{
					process.StandardInput.Flush();
					process.StandardInput.Close();

					// finalize stdin and wait for git-lfs to finish
					if (mode == FilterMode.Clean)
					{
						// write git-lfs pointer for 'clean' to git or file data for 'smudge' to working copy
						process.StandardOutput.BaseStream.CopyTo(output);
						process.StandardOutput.BaseStream.Flush();
						process.StandardOutput.Close();
						output.Flush();
						output.Close();

						process.WaitForExit();
					}
					else if (mode == FilterMode.Smudge)
					{
						// write git-lfs pointer for 'clean' to git or file data for 'smudge' to working copy
						process.StandardOutput.BaseStream.CopyTo(output);
						process.StandardOutput.BaseStream.Flush();
						process.StandardOutput.Close();
						output.Flush();
						output.Close();

						process.WaitForExit();
					}
				}
				finally
				{
					process.Dispose();
				}
			}
			catch (ThreadAbortException)
			{
				
			}
			catch (Exception e)
			{
				logger.Log(LogType.Error,"LFS Complete Error!");
				logger.LogException(e);
			}
			finally
			{
				processes.Remove(path);
				modes.Remove(path);
			}
		}

		protected override void Create(string path, string root, FilterMode mode)
		{
			if (!lfsManager.IsEnabled) return;
			try
			{
				var process = new Process();
				var startInfo = new ProcessStartInfo
				{
					FileName = "git-lfs",
					WorkingDirectory = gitManager.RepoPath,
					RedirectStandardInput = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true,
					UseShellExecute = false
				};

				// launch git-lfs smudge or clean
				switch (mode)
				{
					case FilterMode.Smudge:
						startInfo.Arguments = "smudge";
						break;
					case FilterMode.Clean:
						startInfo.Arguments = "clean";
						break;
					default:
						throw new ArgumentOutOfRangeException("mode");
				}

				process.StartInfo = startInfo;
				if (!process.Start())
				{
					logger.LogFormat(LogType.Error,"Cound not start lfs process of type: {0} for path: {1}",mode,path);
				}
				else
				{
					if (processes.ContainsKey(path))
					{
						logger.LogFormat(LogType.Error,"There is already lfs process for path: {0}",path);
						return;
					}
					processes.Add(path,process);
					modes.Add(path,mode);
				}
			}
			catch (Exception e)
			{
				logger.Log(LogType.Error,"LFS Create Error!");
				logger.LogException(e);
			}
		}

		protected override void Smudge(string path, string root, Stream input, Stream output)
		{
			if (!lfsManager.IsEnabled) return;
			try
			{
				Process process;
				FilterMode mode;
				if (!processes.TryGetValue(path, out process))
				{
					logger.LogFormat(LogType.Log,"Could not find lfs process for path: {0} when smudging",path);
					return;
				}

				if (!modes.TryGetValue(path, out mode))
				{
					logger.LogFormat(LogType.Log,"Could not find lfs filter mode for path: {0} when smudging",path);
					return;
				}

				if (mode != FilterMode.Smudge)
				{
					logger.LogFormat(LogType.Log,"Filter mode mismatch when smudging for path: {0}",path);
				}

				// write git-lfs pointer to stdin
				input.CopyTo(process.StandardInput.BaseStream);
				input.Flush();
			}
			catch (Exception e)
			{
				logger.Log(LogType.Error,"LFS Smudge Error!");
				logger.LogException(e);
			}
		}
	}
}