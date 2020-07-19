using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Windows10PhotoViewerSucksAss
{
	public static class FileAssociationBullshit
	{
		// I don't exactly know what a Progid is. MSDN isn't clear about this.
		// Something like "Paint.Picture" is definitely a Progid. Progids can also have version numbers, like "Paint.Picture.1" or "Paint.Picture.1.1".
		// However, it doesn't explicitly say whether something like "jpegfile" or "AppX43asdfasdf" is also a Progid, or if it's something different. Sure looks the same.
		// This program uses a GUID as Progid. That doesn't seem standard practice, but it avoids dealing with naming and conflicts and stuff. Not sure if it has any implications.
		/// <summary>
		/// Progid used by this application in the registry. A key with this name will be created in "HKCU\software\classes".
		/// </summary>
		public static readonly string ThisApplicationProgid = "{825D77E8-4CF4-45F6-B980-B94D56CA69BD}";

		/// <summary>
		/// Don't change the contents. Will break everything.
		/// </summary>
		public static readonly FileAssocationExtensionInfo[] FileAssocationExtensionInfo = new FileAssocationExtensionInfo[]
		{
			new FileAssocationExtensionInfo(".jpg", "jpegfile", "image"),
			new FileAssocationExtensionInfo(".jpe", "jpegfile", "image"),
			new FileAssocationExtensionInfo(".jpeg", "jpegfile", "image"),
			new FileAssocationExtensionInfo(".jfif", "pjpegfile", "image"),
			new FileAssocationExtensionInfo(".png", "pngfile", "image"),
			new FileAssocationExtensionInfo(".gif", "giffile", "image"),
			new FileAssocationExtensionInfo(".bmp", "Paint.Picture", "image"),
		};
		

		// It's retarded.

		public static void CheckFileAssociations(TextWriter Issues, string ApplicationPath, string FriendlyAppName, bool Install)
		{
			// Info: The default verb is the default value of the "shell" key. If it's not set, it takes the first subkey alphabetically, which is often "open".

			Issues.WriteLine2("===========================================================================================================================");
			Issues.WriteLine2("File Handler Verbs:");

			CheckFileHandlerVerbs(Issues, "jpegfile");
			CheckFileHandlerVerbs(Issues, "pjpegfile");
			CheckFileHandlerVerbs(Issues, "pngfile");
			CheckFileHandlerVerbs(Issues, "Paint.Picture");

			Issues.WriteLine2("===========================================================================================================================");
			Issues.WriteLine2("Extensions (System Defaults):");

			foreach (var Info in FileAssocationExtensionInfo)
			{
				CheckFileExtension_VerifySystemDefaults(Issues, Info.ExpectedPerceivedType, Info.Extension, Info.ExpectedSystemFileHandler);
			}

			if (ApplicationPath != null)
			{
				Issues.WriteLine2("===========================================================================================================================");
				if (ApplicationPath.IndexOf('"') != -1) throw new ArgumentOutOfRangeException(nameof(ApplicationPath));
				string CommandLine = $"\"{ApplicationPath}\" \"%1\"";
				Issues.WriteLine2($"HKCU Progid: -- Expected Command Line: {CommandLine}");

				// Should probably hash the current file name.

				// Should probably build this automatically.
				CheckProgidKey(Issues, Install, ThisApplicationProgid, CommandLine, FriendlyAppName);
			}
			else if (Install)
			{
				throw new ArgumentNullException(nameof(ApplicationPath));
			}

			Issues.WriteLine2("===========================================================================================================================");
			Issues.WriteLine2("Extensions (HKCU Progid Registered):");

			foreach (var Info in FileAssocationExtensionInfo)
			{
				CheckFileExtension_ContainsProgid(Issues, Install, ThisApplicationProgid, Info.Extension);
			}

			Issues.WriteLine2("===========================================================================================================================");
			Issues.WriteLine2("User Choice:");

			foreach (var Info in FileAssocationExtensionInfo)
			{
				CheckUserChoice(Issues, Info.Extension, ThisApplicationProgid);
			}
		}


		private static void CheckUserChoice(TextWriter Issues, string Extension, string Progid)
		{
			// Check whether a program *other* than Progid is written in the UserChoice key of the extension.
			// If that key doesn't exist, or if no user choice is available, that's also OK. The point here is to make sure that Windows asks
			// the user which program it should use the next time any file with that extension is opened.
			var Key = Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{Extension}");
			if (Key == null)
			{
				return;
			}

			var Key_UserChoice = Key.OpenSubKey("userchoice");
			if (Key_UserChoice == null)
			{
				return;
			}

			string CurrentProgid = Key_UserChoice.GetValue("progid") as string;
			if (CurrentProgid == null || Util.EqualsOICSafe(CurrentProgid, Progid))
			{
				return;
			}

			Issues.WriteLine2($"UserChoice for {Extension} is \"{CurrentProgid}\", but \"{Progid}\" was expected.");
		}


		private static void CheckFileExtension_VerifySystemDefaults(TextWriter Issues, string ExpectedPerceivedType, string Extension, string ExpectedSystemProgid)
		{
			// The standard image formats have a direct association in the (default value) of their extension, and then the same thing
			// in the Progid. Both are in HKLM only. E.g. ".jpg" -> default value is "jpegfile", "OpenWithProgids" contains "jpegfile" (empty string).
			var Key = Registry.LocalMachine.OpenSubKey($@"software\classes\{Extension}");
			if (Key == null)
			{
				Issues.WriteLine2($"Extension key {Extension} missing in HKLM.");
				return;
			}
			var DefaultValue = Key.GetValue(null) as string;
			if (!Util.EqualsOICSafe(DefaultValue, ExpectedSystemProgid))
			{
				Issues.WriteLine2($"Extension key {Extension}'s (HKLM) default value is {DefaultValue}, but {ExpectedSystemProgid} was expected.");
			}
			var Key_OpenWithProgids = Key.OpenSubKey("openwithprogids");
			string[] OpenWithProgids = Key_OpenWithProgids?.GetValueNames();
			if (OpenWithProgids == null)
			{
				Issues.WriteLine2($"{Extension} (HKLM) doesn't contain OpenWithProgids, but it should.");
			}
			else
			{
				bool ContainsOpenWithProgid = OpenWithProgids.Any(x => Util.EqualsOICSafe(x, ExpectedSystemProgid));
				if (!ContainsOpenWithProgid)
				{
					Issues.WriteLine2($"OpenWithProgids in {Extension} (HKLM) doesn't contain {ExpectedSystemProgid}. Available entries: {string.Join(",", OpenWithProgids)}");
				}
			}

			CheckPerceivedType(Issues, ExpectedPerceivedType, Key, Extension);
		}

		private static void CheckFileHandlerVerbs(TextWriter Issues, string FileHandlerName)
		{
			// None of the image extension keys (".jpg" etc), nor file handlers ("jpegfile" etc) should have an "open" verb, except giffile.
			var Key_HKLM = Registry.LocalMachine.OpenSubKey($@"software\classes\{FileHandlerName}");
			if (Key_HKLM == null)
			{
				Issues.WriteLine2($"{FileHandlerName} not found in HKLM.");
			}
			else
			{
				CheckFileHandlerVerbsInternal(Issues, FileHandlerName, Key_HKLM);
			}

			var Key_HKCU = Registry.CurrentUser.OpenSubKey($@"software\classes\{FileHandlerName}");
			if (Key_HKCU != null) // This one's optional
			{
				CheckFileHandlerVerbsInternal(Issues, FileHandlerName, Key_HKCU);
			}
		}

		private static void CheckFileHandlerVerbsInternal(TextWriter Issues, string FileHandlerName, RegistryKey Key)
		{
			var Key_Shell = Key.OpenSubKey("shell");
			if (Key_Shell == null)
			{
				return; // No shell - no verbs. That's is OK in this case.
			}
			string DefaultVerb = Key_Shell.GetValue(string.Empty) as string;
			if (DefaultVerb != null)
			{
				Issues.WriteLine2($"{FileHandlerName} has explicit default verb \"{DefaultVerb}\", which is unexpected.");
			}

			var Key_Open = Key_Shell.OpenSubKey("open");
			if (Key_Open != null)
			{
				Issues.WriteLine2($"Key {Key} has open verb, even though it shouldn't.");
			}
		}

		private static void CheckProgidKey(TextWriter Issues, bool Install, string Progid, string ExpectedCommand, string FriendlyAppName)
		{
			// Check that the progid key exists in HKCU classes, and that it has a valid open verb as its only verb.
			var Key = Registry.CurrentUser.OpenSubKey($@"software\classes\{Progid}", writable: Install);
			if (Key == null)
			{
				Issues.WriteLine2($"ProgidKey {Progid} doesn't exist in HKCU.");
				if (Install)
				{
					Key = Registry.CurrentUser.CreateSubKey($@"software\classes\{Progid}");
					Issues.WriteLine2($"Created Progid key for {Progid} in HKCU.");
				}
				else
				{
					return;
				}
			}

			Debug.Assert(Key != null);

			var Key_Shell = Key.OpenSubKey("shell", writable: Install);
			if (Key_Shell == null)
			{
				Issues.WriteLine2($"ProgidKey {Progid} doesn't have shell in HKCU.");
				if (Install)
				{
					Key_Shell = Key.CreateSubKey("shell");
					Issues.WriteLine2($"shell subkey created in {Progid}.");
				}
				else
				{
					return;
				}
			}

			Debug.Assert(Key_Shell != null);

			foreach (string ShellSubKey in Key_Shell.GetSubKeyNames())
			{
				if (!Util.EqualsOICSafe(ShellSubKey, "open"))
				{
					Issues.WriteLine2($"ProgidKey {Progid} contains verb {ShellSubKey}, which isn't expected.");
					if (Install)
					{
						Key_Shell.DeleteSubKeyTree(ShellSubKey);
						Issues.WriteLine2($"Verb \"{ShellSubKey}\" deleted in Progid {Progid}.");
					}
				}
			}

			var Key_Open = Key_Shell.OpenSubKey("open", writable: Install);
			if (Key_Open == null)
			{
				Issues.WriteLine2($"ProgidKey {Progid} doesn't have an open verb.");
				if (Install)
				{
					Key_Open = Key_Shell.CreateSubKey("open");
					Issues.WriteLine2($"Created open verb in ProgidKey {Progid}");
				}
				else
				{
					return;
				}
			}

			Debug.Assert(Key_Open != null);

			string CurrentFriendlyAppName = Key_Open.GetValue("FriendlyAppName") as string;
			if (CurrentFriendlyAppName != FriendlyAppName)
			{
				Issues.WriteLine2($"ProgidKey {Progid}'s open verb has incorrect FriendlyAppName \"{CurrentFriendlyAppName}\"; expected \"{FriendlyAppName}\".");
				if (Install)
				{
					if (FriendlyAppName != null)
					{
						Key_Open.SetValue("FriendlyAppName", FriendlyAppName, RegistryValueKind.String);
						Issues.WriteLine2($"FriendlyAppName for {Progid} set to \"{FriendlyAppName}\".");
					}
					else
					{
						Key_Open.DeleteValue("FriendlyAppName");
						Issues.WriteLine2($"FriendlyAppName for {Progid} removed.");
					}
				}
			}

			var Key_Command = Key_Open.OpenSubKey("command", writable: Install);
			if (Key_Command == null)
			{
				Issues.WriteLine2($"ProgidKey {Progid}'s open verb doesn't have a command subkey.");
				if (Install)
				{
					Key_Command = Key_Open.CreateSubKey("command");
					Issues.WriteLine2($"Created command in {Progid}'s open verb.");
				}
				else
				{
					return;
				}
			}

			Debug.Assert(Key_Command != null);

			string Command = Key_Command.GetValue(null) as string;
			if (!Util.EqualsOICSafe(Command, ExpectedCommand))
			{
				Issues.WriteLine2($"ProgidKey {Progid}'s open verb command is {{{Command}}}, but {{{ExpectedCommand}}} was expected.");
				if (Install)
				{
					Key_Command.SetValue(null, ExpectedCommand);
					Issues.WriteLine2($"Changed command in {Progid}'s open verb to {{{ExpectedCommand}}}.");
				}
			}
		}

		private static void CheckFileExtension_ContainsProgid(TextWriter Issues, bool Install, string ThisApplicationProgid, string Extension)
		{
			// For our association, we want to add our progid to the HCKU extension's OpenWithProgids.
			// So HKCU\software\classes\.jpg\openwithprogids
			//  -> "Progid" : (empty string)
			// Putting it in "jpegfile" doesn't work.
			// Using a custom verb has issues with the open with menu and is awkward. So this is probably still a better solution.
			var Key = Registry.CurrentUser.OpenSubKey($@"software\classes\{Extension}", writable: Install);
			if (Key == null)
			{
				Issues.WriteLine2($"HKCU key for {Extension} doesn't exist.");
				if (Install)
				{
					Key = Registry.CurrentUser.CreateSubKey($@"software\classes\{Extension}");
					Issues.WriteLine2($"Created extension \"{Extension}\" in HKCU.");
				}
				else
				{
					return;
				}
			}

			Debug.Assert(Key != null);

			var Key_OpenWithProgids = Key.OpenSubKey("openwithprogids", writable: Install);
			if (Key_OpenWithProgids == null)
			{
				Issues.WriteLine2($"HKCU key for {Extension} doesn't have OpenWithProgids.");
				if (Install)
				{
					Key_OpenWithProgids = Key.CreateSubKey("OpenWithProgids");
					Issues.WriteLine2($"Created OpenWithProgids in \"{Extension}\".");
				}
				else
				{
					return;
				}
			}

			Debug.Assert(Key_OpenWithProgids != null);

			bool HaveProgid = Key_OpenWithProgids.GetValueNames().Any(x => Util.EqualsOICSafe(x, ThisApplicationProgid));
			if (!HaveProgid)
			{
				Issues.WriteLine2($"HKCU key for {Extension} doesn't contain Progid {ThisApplicationProgid} in OpenWithProgids.");
				if (Install)
				{
					Key_OpenWithProgids.SetValue(ThisApplicationProgid, string.Empty, RegistryValueKind.String);
					Issues.WriteLine2($"Added Progid in \"{Extension}\": \"{ThisApplicationProgid}\".");
				}
			}
		}

		private static void CheckPerceivedType(TextWriter Issues, string ExpectedPerceivedType, RegistryKey Key_HKLM, string Extension)
		{
			// HKLM extension keys (".jpg" etc) should have perceived type "image".
			string PerceivedType = Key_HKLM.GetValue("perceivedtype") as string;
			if (PerceivedType == null)
			{
				Issues.WriteLine2($"Perceived type in \"{Extension}\" is missing (expected \"{ExpectedPerceivedType}\")");
			}
			else if (!Util.EqualsOICSafe(PerceivedType, ExpectedPerceivedType))
			{
				Issues.WriteLine2($"Perceived type in \"{Extension}\" ({PerceivedType}) seems wrong (expected \"{ExpectedPerceivedType}\")");
			}
		}
	}

	public struct FileAssocationExtensionInfo
	{
		public readonly string Extension;
		public readonly string ExpectedSystemFileHandler;
		public readonly string ExpectedPerceivedType;

		public FileAssocationExtensionInfo(string Extension, string ExpectedSystemFileHandler, string ExpectedPerceivedType)
		{
			this.Extension = Extension;
			this.ExpectedSystemFileHandler = ExpectedSystemFileHandler;
			this.ExpectedPerceivedType = ExpectedPerceivedType;
		}
	}
}
