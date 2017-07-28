namespace UniGit.Settings
{
	public interface ISettingsAffector
	{
		void AffectThreading(ref GitSettingsJson.ThreadingType setting);
	}
}