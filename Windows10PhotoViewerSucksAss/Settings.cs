﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace Windows10PhotoViewerSucksAss
{
	using static XmlDocumentHelper;

	public class Settings
	{
		public static void Initialize(string appDataFolderName)
		{
			Debug.Assert(_manager == null);
			_manager = new SettingsManager(appDataFolderName);
		}

		public static void Load()
		{
			Instance = GetManager().Load(Deserialize);
		}

		public static void QueueSave()
		{
			var tmp = Instance;
			Debug.Assert(tmp != null);
			GetManager().QueueSave(tmp, Serialize);
		}

		public static void WaitSaveCompleted()
		{
			GetManager().WaitSaveCompleted();
		}

		private static SettingsManager GetManager()
		{
			var tmp = _manager;
			Debug.Assert(tmp != null);
			return tmp;
		}

		private static SettingsManager _manager;
		public static Settings Instance { get; private set; }

		//    _   _                          _   _   _                 
		//   | | | |___  ___ _ __   ___  ___| |_| |_(_)_ __   __ _ ___ 
		//   | | | / __|/ _ \ '__| / __|/ _ \ __| __| | '_ \ / _` / __|
		//   | |_| \__ \  __/ |    \__ \  __/ |_| |_| | | | | (_| \__ \
		//    \___/|___/\___|_|    |___/\___|\__|\__|_|_| |_|\__, |___/
		//                                                   |___/     

		public int Color { get; set; }
		public int WindowWidth { get; set; }
		public int WindowHeight { get; set; }
		public bool SortCaseSensitive { get; set; }
		public FontDescriptor ApplicationFont { get; set; }
		public int OverviewControlWidth { get; set; } = -1;
		public int SplitterWidth { get; set; } = -1;
		public string MouseWheelMode { get; set; }
		public bool UseCurrentImageAsWindowIcon { get; set; }
		public int FileListBackColor { get; set; }
		public int FileListForeColor { get; set; }
		public int FileListForeColorError { get; set; }

		public static Settings Deserialize(Stream stream)
		{
			using (var reader = new StreamReader(stream))
			{
				var doc = new XmlDocument();
				doc.LoadXml(reader.ReadToEnd());

				var root = GetDocumentRootNode(doc);
				if (root == null) return null;

				// Root node is Settings.
				var settings = new Settings();
				{ if (TryGetElementValueInt32(root, "Color", out var tmp)) settings.Color = tmp; }
				{ if (TryGetElementValueInt32(root, "WindowWidth", out var tmp)) settings.WindowWidth = tmp; }
				{ if (TryGetElementValueInt32(root, "WindowHeight", out var tmp)) settings.WindowHeight = tmp; }
				{ if (TryGetElementValueBool(root, "SortCaseSensitive", out var tmp)) settings.SortCaseSensitive = tmp; }
				{ if (TryGetElement(root, "ApplicationFont", out var tmp)) settings.ApplicationFont = DeserializeFontDescriptor(tmp); }
				{ if (TryGetElementValueInt32(root, "OverviewControlWidth", out var tmp)) settings.OverviewControlWidth = tmp; }
				{ if (TryGetElementValueInt32(root, "SplitterWidth", out var tmp)) settings.SplitterWidth = tmp; }
				{ if (TryGetElementValueString(root, "MouseWheelMode", out var tmp)) settings.MouseWheelMode = tmp; }
				{ if (TryGetElementValueBool(root, "UseCurrentImageAsWindowIcon", out var tmp)) settings.UseCurrentImageAsWindowIcon = tmp; }
				{ if (TryGetElementValueInt32(root, "FileListBackColor", out var tmp)) settings.FileListBackColor = tmp; }
				{ if (TryGetElementValueInt32(root, "FileListForeColor", out var tmp)) settings.FileListForeColor = tmp; }
				{ if (TryGetElementValueInt32(root, "FileListForeColorError", out var tmp)) settings.FileListForeColorError = tmp; }

				return settings;
			}
		}

		private static FontDescriptor DeserializeFontDescriptor(XmlElement node)
		{
			if (IsNil(node)) return null;

			if (!TryGetElementValueString(node, "FontFamily", out var family)) return null;
			if (!TryGetElementValueFloat(node, "Size", out var size)) return null;
			if (!TryGetElementValueInt32(node, "Style", out var style)) return null;

			var result = new FontDescriptor();
			result.FontFamily = family;
			result.Size = size;
			result.Style = style;
			return result;
		}

		public static void Serialize(Stream stream, Settings settings)
		{
			using (var writer = new StreamWriter(stream))
			{
				var doc = new XmlDocument();
				var root = doc.CreateElement("Settings");
				SetupNamespaces(root);

				doc.AppendChild(root);
				SerializeSettings(root, settings);

				doc.Save(writer);
			}
		}

		private static XmlElement SerializeSettings(XmlElement target, Settings settings)
		{
			if (settings == null) return SetNil(target);

			AddElementValueInt32(target, "Color", settings.Color);
			AddElementValueInt32(target, "WindowWidth", settings.WindowWidth);
			AddElementValueInt32(target, "WindowHeight", settings.WindowHeight);
			AddElementValueBool(target, "SortCaseSensitive", settings.SortCaseSensitive);
			SerializeFontDescriptor(AddElement(target, "ApplicationFont"), settings.ApplicationFont);
			AddElementValueInt32(target, "OverviewControlWidth", settings.OverviewControlWidth);
			AddElementValueInt32(target, "SplitterWidth", settings.SplitterWidth);
			AddElementValueString(target, "MouseWheelMode", settings.MouseWheelMode);
			AddElementValueBool(target, "UseCurrentImageAsWindowIcon", settings.UseCurrentImageAsWindowIcon);
			AddElementValueInt32(target, "FileListBackColor", settings.FileListBackColor);
			AddElementValueInt32(target, "FileListForeColor", settings.FileListForeColor);
			var test = AddElementValueInt32(target, "FileListForeColorError", settings.FileListForeColorError);
			SetNil(test);

			return target;
		}

		private static XmlElement SerializeFontDescriptor(XmlElement target, FontDescriptor fontDescriptor)
		{
			if (fontDescriptor == null) return SetNil(target);

			AddElementValueString(target, "FontFamily", fontDescriptor.FontFamily);
			AddElementValueFloat(target, "Size", fontDescriptor.Size);
			AddElementValueInt32(target, "Style", fontDescriptor.Style);

			return target;
		}
	}
}
