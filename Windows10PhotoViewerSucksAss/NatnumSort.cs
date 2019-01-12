using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Windows10PhotoViewerSucksAss
{
	class NatnumSort : IComparer<string>
	{
		public const int L_BIGGER = 1;
		public const int R_BIGGER = -1;
		public const int NO_STRING_TOKEN = -1;

		public static int Sort(string l, string r)
		{
			int cursor_l = 0;
			int cursor_r = 0;
			while (true)
			{
				if (cursor_l >= l.Length && cursor_r >= r.Length)
				{
					return 0;
				}
				if (cursor_l >= l.Length)
				{
					return L_BIGGER;
				}
				if (cursor_r >= r.Length)
				{
					return R_BIGGER;
				}

				cursor_l = GrabToken(l, cursor_l, out int string_token_l, out ulong ulong_token_l);
				cursor_r = GrabToken(r, cursor_r, out int string_token_r, out ulong ulong_token_r);

				if (string_token_l == NO_STRING_TOKEN && string_token_r == NO_STRING_TOKEN)
				{
					// both have numbers here
					if (ulong_token_l > ulong_token_r)
					{
						return L_BIGGER;
					}
					else if (ulong_token_r > ulong_token_l)
					{
						return R_BIGGER;
					}
					// Else equal; keep going.
				}
				else if (string_token_l == NO_STRING_TOKEN)
				{
					// l has a number, r has a string here. string is bigger (number comes first).
					return R_BIGGER;
				}
				else if (string_token_r == NO_STRING_TOKEN)
				{
					// see above
					return L_BIGGER;
				}
				else
				{
					var stringComparison = CompareStringParts(l, string_token_l, cursor_l, r, string_token_r, cursor_r);
					if (stringComparison != 0)
					{
						return stringComparison;
					}
				}
			}
		}

		// Ends are exclusive
		private static int CompareStringParts(string l, int start_l, int end_l, string r, int start_r, int end_r)
		{
			int cursor_l = start_l;
			int cursor_r = start_r;

			while (true)
			{
				if (cursor_l == end_l && cursor_r == end_r)
				{
					return 0;
				}
				if (cursor_l == end_l)
				{
					// l already ended -> r is bigger
					return R_BIGGER;
				}
				if (cursor_r == end_r)
				{
					// r already ended -> l is bigger
					return L_BIGGER;
				}

				char char_l = l[cursor_l];
				char char_r = r[cursor_r];
				if (char_l > char_r)
				{
					return L_BIGGER;
				}
				else if (char_r > char_l)
				{
					return R_BIGGER;
				}

				cursor_l += 1;
				cursor_r += 1;
			}
		}

		private static int GrabToken(string str, int startIndex, out int string_token_start, out ulong int_token)
		{
			Debug.Assert(str != null);

			if (startIndex >= str.Length)
			{
				string_token_start = startIndex;
				int_token = default(ulong);
				return startIndex;
			}

			int cursor = startIndex + 1;

			if (Char.IsDigit(str[startIndex]))
			{
				// This MIGHT be a number
				// It's only a number if it starts with a digit, but is NOT terminated with a letter.
				// This is so that hexadecimal strings (read: filenames that are MD5 hashes) are not
				// sorted "naturally" -- mostly at least. If the hex number ends with decimal digits,
				// this might not work.
				for (; cursor < str.Length; ++cursor)
				{
					char c = str[cursor];
					if (Char.IsDigit(c))
					{
						continue;
					}
					else if (Char.IsLetter(c))
					{
						// Nope; not a number. Pretend this is a normal string token, until we find the next digit.
						goto __nah;
					}
					else
					{
						break;
					}
				}

				// The digits were terminated either by the end of the string, or by some special character.
				// This is really a number.
				var substr = str.Substring(startIndex, cursor - startIndex);
				if (UInt64.TryParse(substr, NumberStyles.None, CultureInfo.InvariantCulture, out int_token))
				{
					string_token_start = NO_STRING_TOKEN;
					return cursor;
				}
				else
				{
					// It might be too long to fit in a uint64
					string_token_start = startIndex;
					return cursor;
				}
			}

			__nah:

			// This is a normal text token. Continue until we find the next digit and then return the text.
			for (; cursor < str.Length; ++cursor)
			{
				char c = str[cursor];
				if (Char.IsDigit(c))
				{
					break;
				}
			}
			
			string_token_start = startIndex;
			int_token = default(ulong);
			return cursor;
		}

		public int Compare(string x, string y)
		{
			return Sort(x, y);
		}

		public static NatnumSort Instance { get; } = new NatnumSort();
	}
}
