using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Profiling;

namespace UniGit
{
	public class GitResourceManager
	{
		private static Dictionary<string,Texture2D> textures = new Dictionary<string, Texture2D>();

		internal static void Initilize()
		{
			Profiler.BeginSample("UniGit Resource Loading");
			try
			{
				LoadDLLResources();
			}
			finally
			{
				Profiler.EndSample();
			}
		}

		public static Texture2D GetTexture(string name, bool throwError = true)
		{
			Texture2D tex;
			if (textures.TryGetValue(name, out tex))
			{
				return tex;
			}
			if (throwError)
				Debug.LogError("Could not find texture with key: " + name);
			return null;
		}

		public static void LoadDLLResources()
		{
			try
			{
				Assembly myAssembly = Assembly.Load("UniGitResources");
				var rc = new System.Resources.ResourceManager("UniGitResources.Properties.Resources", myAssembly);
				foreach (DictionaryEntry e in rc.GetResourceSet(CultureInfo.InvariantCulture, true, true))
				{
					if (e.Value.GetType().Name == "Bitmap")
					{
						textures.Add((string)e.Key, LoadTextureFromBitmap((string)e.Key, e.Value));
					}

				}
				rc.ReleaseAllResources();
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}
		}

		private static Texture2D LoadTextureFromBitmap(string name, object resource)
		{
			var bitmapType = resource.GetType();

			var widthProperty = bitmapType.GetProperty("Width", BindingFlags.Public | BindingFlags.Instance);
			int width = (int)widthProperty.GetValue(resource, null);
			var heightProperty = bitmapType.GetProperty("Height", BindingFlags.Public | BindingFlags.Instance);
			int height = (int)heightProperty.GetValue(resource, null);

			var imageFormatProperty = bitmapType.GetProperty("RawFormat", BindingFlags.Public | BindingFlags.Instance);
			var imageFromat = imageFormatProperty.GetValue(resource, null);

			var saveToStreamMethod = bitmapType.GetMethod("Save", BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(Stream), imageFromat.GetType() }, null);
			byte[] imageBytes;
			using (MemoryStream ms = new MemoryStream())
			{
				saveToStreamMethod.Invoke(resource, new object[] { ms, imageFromat });
				imageBytes = ms.ToArray();
			}

			var img = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
			{
				hideFlags = HideFlags.HideAndDontSave,
				name = "UniGitEditorResource.Image." + name,
				wrapMode = TextureWrapMode.Clamp
			};
			if (!img.LoadImage(imageBytes))
			{
				Debug.Log("There was a problem while loading a texture: " + name);
			}
			img.Apply();

			return img;
		}
	}
}