using System;

namespace UniGit.Utils
{
	//taken from .NET
	public static class PatternMatcher
	{
		// Methods
		public static bool StrictMatchPattern(string expression, string name)
		{
			expression = expression.ToLowerInvariant();
			name = name.ToLowerInvariant();
			int num9;
			var ch = '\0';
			var ch2 = '\0';
			var sourceArray = new int[16];
			var numArray2 = new int[16];
			var flag = false;
			if (((name.Length == 0)) || ((expression.Length == 0)))
			{
				return false;
			}
			if (expression.Equals("*") || expression.Equals("*.*"))
			{
				return true;
			}
			if ((expression[0] == '*') && (expression.IndexOf('*', 1) == -1))
			{
				var length = expression.Length - 1;
				if ((name.Length >= length) && (string.Compare(expression, 1, name, name.Length - length, length, StringComparison.OrdinalIgnoreCase) == 0))
				{
					return true;
				}
			}
			sourceArray[0] = 0;
			var num7 = 1;
			var num = 0;
			var num8 = expression.Length * 2;
			while (!flag)
			{
				int num3;
				if (num < name.Length)
				{
					ch = name[num];
					num3 = 1;
					num++;
				}
				else
				{
					flag = true;
					if (sourceArray[num7 - 1] == num8)
					{
						break;
					}
				}
				var index = 0;
				var num5 = 0;
				var num6 = 0;
				while (index < num7)
				{
					var num2 = (sourceArray[index++] + 1) / 2;
					num3 = 0;
					Label_00F2:
					if (num2 != expression.Length)
					{
						num2 += num3;
						num9 = num2 * 2;
						if (num2 == expression.Length)
						{
							numArray2[num5++] = num8;
						}
						else
						{
							ch2 = expression[num2];
							num3 = 1;
							if (num5 >= 14)
							{
								var num11 = numArray2.Length * 2;
								var destinationArray = new int[num11];
								Array.Copy(numArray2, destinationArray, numArray2.Length);
								numArray2 = destinationArray;
								destinationArray = new int[num11];
								Array.Copy(sourceArray, destinationArray, sourceArray.Length);
								sourceArray = destinationArray;
							}
							if (ch2 == '*')
							{
								numArray2[num5++] = num9;
								numArray2[num5++] = num9 + 1;
								goto Label_00F2;
							}
							if (ch2 == '>')
							{
								var flag2 = false;
								if (!flag && (ch == '.'))
								{
									var num13 = name.Length;
									for (var i = num; i < num13; i++)
									{
										var ch3 = name[i];
										num3 = 1;
										if (ch3 == '.')
										{
											flag2 = true;
											break;
										}
									}
								}
								if ((flag || (ch != '.')) || flag2)
								{
									numArray2[num5++] = num9;
									numArray2[num5++] = num9 + 1;
								}
								else
								{
									numArray2[num5++] = num9 + 1;
								}
								goto Label_00F2;
							}
							num9 += num3 * 2;
							switch (ch2)
							{
								case '<':
									if (flag || (ch == '.'))
									{
										goto Label_00F2;
									}
									numArray2[num5++] = num9;
									goto Label_028D;

								case '"':
									if (flag)
									{
										goto Label_00F2;
									}
									if (ch == '.')
									{
										numArray2[num5++] = num9;
										goto Label_028D;
									}
									break;
							}
							if (!flag)
							{
								if (ch2 == '?')
								{
									numArray2[num5++] = num9;
								}
								else if (ch2 == ch)
								{
									numArray2[num5++] = num9;
								}
							}
						}
					}
					Label_028D:
					if ((index < num7) && (num6 < num5))
					{
						while (num6 < num5)
						{
							var num14 = sourceArray.Length;
							while ((index < num14) && (sourceArray[index] < numArray2[num6]))
							{
								index++;
							}
							num6++;
						}
					}
				}
				if (num5 == 0)
				{
					return false;
				}
				var numArray4 = sourceArray;
				sourceArray = numArray2;
				numArray2 = numArray4;
				num7 = num5;
			}
			num9 = sourceArray[num7 - 1];
			return (num9 == num8);
		}
	}
}