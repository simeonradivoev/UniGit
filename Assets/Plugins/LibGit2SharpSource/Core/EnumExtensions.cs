using System;
using System.Collections.Generic;
using System.Linq;

namespace LibGit2Sharp.Core
{
    internal static class EnumExtensions
    {
        public static bool HasAny(this Enum enumInstance, IEnumerable<Enum> entries)
        {
            return entries.Any(e => HasFlag(enumInstance,e));
        }

	    private static bool HasFlag(Enum e,Enum flag)
	    {
		    if (e.GetType() != flag.GetType())
		    {
			    throw new ArgumentException("Argument_EnumTypeDoesNotMatch", "e");
		    }

		    ulong num = Convert.ToUInt64(e);
		    ulong num2 = Convert.ToUInt64(flag);
		    return (num2 & num) == num;

	    }
    }
}
