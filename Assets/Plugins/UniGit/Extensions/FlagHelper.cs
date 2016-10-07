using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Utils.Extensions
{
	public static class FlagHelper
	{
		public static int GetIndex<T>(this T values) where T : struct
		{
			CheckIsEnum<T>(true);
			int value = Convert.ToInt32(values);
			int index = 0;
			while (value > 0)
			{
				value >>= 1;
				index++;
			}
			return index;
		}

		private static void CheckIsEnum<T>(bool withFlags)
		{
			if (!typeof(T).IsEnum)
				throw new ArgumentException(string.Format("Type '{0}' is not an enum", typeof(T).FullName));
			if (withFlags && !Attribute.IsDefined(typeof(T), typeof(FlagsAttribute)))
				throw new ArgumentException(string.Format("Type '{0}' doesn't have the 'Flags' attribute", typeof(T).FullName));
		}

		public static bool IsFlagSet<T>(this T value, T flag) where T : struct
		{
			CheckIsEnum<T>(true);
			long lValue = Convert.ToInt64(value);
			long lFlag = Convert.ToInt64(flag);
			return (lValue & lFlag) != 0;
		}

		public static IEnumerable<T> GetFlags<T>(this T value) where T : struct
		{
			CheckIsEnum<T>(true);
			foreach (T flag in Enum.GetValues(typeof(T)).Cast<T>())
			{
				if (value.IsFlagSet(flag))
					yield return flag;
			}
		}

		public static T SetFlags<T>(this T value, T flags, bool on) where T : struct
		{
			CheckIsEnum<T>(true);
			long lValue = Convert.ToInt64(value);
			long lFlag = Convert.ToInt64(flags);
			if (on)
			{
				lValue |= lFlag;
			}
			else
			{
				lValue &= (~lFlag);
			}
			return (T)Enum.ToObject(typeof(T), lValue);
		}

		public static T SetFlags<T>(this T value, T flags) where T : struct
		{
			return value.SetFlags(flags, true);
		}

		public static T ClearFlags<T>(this T value, T flags) where T : struct
		{
			return value.SetFlags(flags, false);
		}

		public static T CombineFlags<T>(this IEnumerable<T> flags) where T : struct
		{
			CheckIsEnum<T>(true);
			long lValue = 0;
			foreach (T flag in flags)
			{
				long lFlag = Convert.ToInt64(flag);
				lValue |= lFlag;
			}
			return (T)Enum.ToObject(typeof(T), lValue);
		}

		public static string GetDescription(this Enum val)
		{
			string name = Enum.GetName(val.GetType(), val);
			if (name != null)
			{
				FieldInfo field = val.GetType().GetField(name);
				if (field != null)
				{
					DescriptionAttribute attr = Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) as DescriptionAttribute;
					if (attr != null)
					{
						return attr.Description;
					}
				}
			}
			return name;
		}

		public static string GetDescription<T>(this T value) where T : struct
		{
			CheckIsEnum<T>(false);
			string name = Enum.GetName(typeof(T), value);
			if (name != null)
			{
				FieldInfo field = typeof(T).GetField(name);
				if (field != null)
				{
					DescriptionAttribute attr = Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) as DescriptionAttribute;
					if (attr != null)
					{
						return attr.Description;
					}
				}
			}
			return name;
		}
	}
}