using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Windows10PhotoViewerSucksAss
{
	// https://docs.microsoft.com/en-gb/archive/blogs/twistylittlepassagesallalike/everyone-quotes-command-line-arguments-the-wrong-way
	static class CommandLineArgumentQuoting
	{
		public static void ArgvQuote(string Argument, StringBuilder CommandLine, bool Force)

		/*++

        Routine Description:

            This routine appends the given argument to a command line such
            that CommandLineToArgvW will return the argument string unchanged.
            Arguments in a command line should be separated by spaces; this
            function does not add these spaces.

        Arguments:

            Argument - Supplies the argument to encode.

            CommandLine - Supplies the command line to which we append the encoded argument string.

            Force - Supplies an indication of whether we should quote
                    the argument even if it does not contain any characters that would
                    ordinarily require quoting.

        Return Value:

            None.

        Environment:

            Arbitrary.

        --*/

		{
			//
			// Unless we're told otherwise, don't quote unless we actually
			// need to do so --- hopefully avoid problems if programs won't
			// parse quotes properly
			//

			if (Force == false &&
				!String.IsNullOrEmpty(Argument) &&
				Argument.IndexOfAny(new char[] { ' ', '\t', '\n', '\v', '\"' }) == -1)
			{
				CommandLine.Append(Argument);
			}
			else
			{
				CommandLine.Append('"');

				using (var enumerator = Argument.GetEnumerator())
				{
					bool hasValue = enumerator.MoveNext();
					for (; ; hasValue = enumerator.MoveNext())
					{
						int NumberBackslashes = 0;

						while (hasValue && enumerator.Current == '\\')
						{
							hasValue = enumerator.MoveNext();
							++NumberBackslashes;
						}

						if (!hasValue)
						{

							//
							// Escape all backslashes, but let the terminating
							// double quotation mark we add below be interpreted
							// as a metacharacter.
							//

							CommandLine.Append("".PadLeft(NumberBackslashes * 2, '\\'));
							break;
						}
						else if (enumerator.Current == '"')
						{

							//
							// Escape all backslashes and the following
							// double quotation mark.
							//

							CommandLine.Append("".PadLeft(NumberBackslashes * 2 + 1, '\\'));
							CommandLine.Append(enumerator.Current);
						}
						else
						{

							//
							// Backslashes aren't special here.
							//

							CommandLine.Append("".PadLeft(NumberBackslashes, '\\'));
							CommandLine.Append(enumerator.Current);
						}
					}

					CommandLine.Append('"');
				}
			}
		}
	}
}
