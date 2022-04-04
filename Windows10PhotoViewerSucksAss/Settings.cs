using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
			Settings.AppDataFolderName = appDataFolderName;
		}

		private static string AppDataFolderName;
		private static readonly SettingsSaveManager _manager = new SettingsSaveManager();

		private static string GetFullSettingsFilePath()
		{
			string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			return Path.Combine(appData, AppDataFolderName, "settings.xml");
		}

		public static void Load()
		{
			Instance = LoadInternal() ?? new Settings();
		}

		private static Settings LoadInternal()
		{
			try
			{
				var path = GetFullSettingsFilePath();
				using (var fileStream = FileIO.OpenRead(out int error, path))
				{
					if (fileStream == null)
					{
						// File not found etc -- we don't actually care about the exact error.
						return null;
					}

					var result = Deserialize(fileStream);
					return result;
				}
			}
			catch (Exception ex)
			{
				// We don't actually care.
				Debug.WriteLine(ex);
				return null;
			}
		}

		public static void QueueSave()
		{
			var tmp = Instance;
			Debug.Assert(tmp != null);
			QueueSaveInternal(tmp);
		}

		private static void QueueSaveInternal(Settings value)
		{
			byte[] bytes;
			using (var stream = new MemoryStream())
			{
				Serialize(stream, value);
				bytes = stream.ToArray();
			}
			
			string saveDestination = GetFullSettingsFilePath();

			_manager.QueueSave(() =>
			{
				var dir = Path.GetDirectoryName(saveDestination);
				Directory.CreateDirectory(dir);
				File.WriteAllBytes(saveDestination, bytes);
			});
		}

		public static void WaitSaveCompleted()
		{
			_manager.WaitSaveCompleted();
		}

		
		public static Settings Instance { get; private set; }

		//    _   _                          _   _   _                 
		//   | | | |___  ___ _ __   ___  ___| |_| |_(_)_ __   __ _ ___ 
		//   | | | / __|/ _ \ '__| / __|/ _ \ __| __| | '_ \ / _` / __|
		//   | |_| \__ \  __/ |    \__ \  __/ |_| |_| | | | | (_| \__ \
		//    \___/|___/\___|_|    |___/\___|\__|\__|_|_| |_|\__, |___/
		//                                                   |___/     

		public Color ImageBackColor { get; set; }
		public int WindowWidth { get; set; }
		public int WindowHeight { get; set; }
		public bool SortCaseSensitive { get; set; }
		public FontDescriptor ApplicationFont { get; set; }
		public int OverviewControlWidth { get; set; } = -1;
		public int SplitterWidth { get; set; } = -1;
		public MouseWheelMode MouseWheelMode { get; set; }
		public bool UseCurrentImageAsWindowIcon { get; set; }
		public Color FileListBackColor { get; set; }
		public Color FileListForeColor { get; set; }
		public Color FileListForeColorError { get; set; }

		public static Settings Deserialize(Stream stream)
		{
			using (var reader = new StreamReader(stream))
			{
				var doc = new XmlDocument();
				doc.LoadXml(reader.ReadToEnd());

				var root = GetDocumentRootElement(doc);
				if (root == null) return null;

				// Root node is Settings.
				var settings = new Settings();
				{ if (TryGetElementValueColor_MaybeInt32Fallback(root, "Color", out var tmp)) settings.ImageBackColor = tmp; } // Legacy property name
				{ if (TryGetElementValueColor_MaybeInt32Fallback(root, "ImageBackColor", out var tmp)) settings.ImageBackColor = tmp; }
				{ if (TryGetElementValueInt32(root, "WindowWidth", out var tmp)) settings.WindowWidth = tmp; }
				{ if (TryGetElementValueInt32(root, "WindowHeight", out var tmp)) settings.WindowHeight = tmp; }
				{ if (TryGetElementValueBool(root, "SortCaseSensitive", out var tmp)) settings.SortCaseSensitive = tmp; }
				{ if (TryGetElement(root, "ApplicationFont", out var tmp)) settings.ApplicationFont = DeserializeFontDescriptor(tmp); }
				{ if (TryGetElementValueInt32(root, "OverviewControlWidth", out var tmp)) settings.OverviewControlWidth = tmp; }
				{ if (TryGetElementValueInt32(root, "SplitterWidth", out var tmp)) settings.SplitterWidth = tmp; }
				{ if (TryGetElementValueEnum<MouseWheelMode>(root, "MouseWheelMode", out var tmp)) settings.MouseWheelMode = tmp; }
				{ if (TryGetElementValueBool(root, "UseCurrentImageAsWindowIcon", out var tmp)) settings.UseCurrentImageAsWindowIcon = tmp; }
				{ if (TryGetElementValueColor_MaybeInt32Fallback(root, "FileListBackColor", out var tmp)) settings.FileListBackColor = tmp; }
				{ if (TryGetElementValueColor_MaybeInt32Fallback(root, "FileListForeColor", out var tmp)) settings.FileListForeColor = tmp; }
				{ if (TryGetElementValueColor_MaybeInt32Fallback(root, "FileListForeColorError", out var tmp)) settings.FileListForeColorError = tmp; }

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
				var root = CreateDocumentRootElementAndSetupNamespaces(doc, "Settings");

				SerializeSettings(root, settings);

				doc.Save(writer);
			}
		}

		private static XmlElement SerializeSettings(XmlElement target, Settings settings)
		{
			if (settings == null) return SetNil(target);

			AddElementValueColor(target, "ImageBackColor", settings.ImageBackColor);
			AddElementValueInt32(target, "WindowWidth", settings.WindowWidth);
			AddElementValueInt32(target, "WindowHeight", settings.WindowHeight);
			AddElementValueBool(target, "SortCaseSensitive", settings.SortCaseSensitive);
			SerializeFontDescriptor(AddElement(target, "ApplicationFont"), settings.ApplicationFont);
			AddElementValueInt32(target, "OverviewControlWidth", settings.OverviewControlWidth);
			AddElementValueInt32(target, "SplitterWidth", settings.SplitterWidth);
			AddElementValueEnum(target, "MouseWheelMode", settings.MouseWheelMode);
			AddElementValueBool(target, "UseCurrentImageAsWindowIcon", settings.UseCurrentImageAsWindowIcon);
			AddElementValueColor(target, "FileListBackColor", settings.FileListBackColor);
			AddElementValueColor(target, "FileListForeColor", settings.FileListForeColor);
			AddElementValueColor(target, "FileListForeColorError", settings.FileListForeColorError);

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

		private static bool TryGetElementValueColor_MaybeInt32Fallback(XmlElement parent, string elementName, out Color color)
		{
			if (TryGetElementValueColor(parent, elementName, out color))
			{
				return true;
			}
			if (TryGetElementValueInt32(parent, elementName, out int argb))
			{
				if (argb == 0)
				{
					color = Color.Empty;
					return true;
				}
				color = Color.FromArgb(argb);
				return true;
			}
			return false;
		}
	}
}
