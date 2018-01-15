namespace UniGit
{
	public interface IGitWatcher
	{
		bool IsWatching { get; }
		bool IsValid { get; }
		void MarkDirty();
	}
}