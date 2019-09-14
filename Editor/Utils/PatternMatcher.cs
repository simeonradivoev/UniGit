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
			char ch = '\0';
			char ch2 = '\0';
			int[] sourceArray = new int[16];
			int[] numArray2 = new int[16];
			bool flag = false;
			if (((name == null) || (name.Length == 0)) || ((expression == null) || (expression.Length == 0)))
			{
				return false;
			}
			if (expression.Equals("*") || expression.Equals("*.*"))
			{
				return true;
			}
			if ((expression[0] == '*') && (expression.IndexOf('*', 1) == -1))
			{
				int length = expression.Length - 1;
				if ((name.Length >= length) && (string.Compare(expression, 1, name, name.Length - length, length, StringComparison.OrdinalIgnoreCase) == 0))
				{
					return true;
				}
			}
			sourceArray[0] = 0;
			int num7 = 1;
			int num = 0;
			int num8 = expression.Length * 2;
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
				int index = 0;
				int num5 = 0;
				int num6 = 0;
				while (index < num7)
				{
					int num2 = (sourceArray[index++] + 1) / 2;
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
								int num11 = numArray2.Length * 2;
								int[] destinationArray = new int[num11];
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
								bool flag2 = false;
								if (!flag && (ch == '.'))
								{
									int num13 = name.Length;
									for (int i = num; i < num13; i++)
									{
										char ch3 = name[i];
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
							int num14 = sourceArray.Length;
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
				int[] numArray4 = sourceArray;
				sourceArray = numArray2;
				numArray2 = numArray4;
				num7 = num5;
			}
			num9 = sourceArray[num7 - 1];
			return (num9 == num8);
		}
	}
}