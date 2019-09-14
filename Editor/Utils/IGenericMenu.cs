using UnityEditor;
using UnityEngine;

namespace UniGit.Utils
{
	public interface IGenericMenu
	{
		void AddItem(GUIContent content, bool on, GenericMenu.MenuFunction func);
		void AddDisabledItem(GUIContent content);
		void AddItem(GUIContent content, bool on, GenericMenu.MenuFunction2 func, object data);
		void AddSeparator(string text);
	}
}