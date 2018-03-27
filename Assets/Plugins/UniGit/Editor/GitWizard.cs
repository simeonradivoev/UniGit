using UnityEditor;

namespace UniGit
{
	public class GitWizard : ScriptableWizard
	{
		protected SerializedObject serializedObject;

		protected virtual void OnEnable()
		{
			GitWindows.AddWindow(this);
			serializedObject = new SerializedObject(this);
			Repaint();
		}

		protected virtual void OnDisable()
		{
			GitWindows.RemoveWindow(this);
		}
	}
}
