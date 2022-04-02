using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace Windows10PhotoViewerSucksAss
{
	static class XmlDocumentHelper
	{
		public static XmlElement GetDocumentRootNode(XmlDocument doc)
		{
			return (XmlElement)doc.ChildNodes.Cast<XmlNode>().Where(x => x.NodeType == XmlNodeType.Element).FirstOrDefault();
		}

		private static readonly string xsi = "http://www.w3.org/2001/XMLSchema-instance";

		public static void SetupNamespaces(XmlElement root)
		{
			root.SetAttribute("xmlns:xsi", xsi);
		}

		public static bool TryGetElement(XmlElement parent, string elementName, out XmlElement childNode)
		{
			childNode = (XmlElement)parent.ChildNodes.Cast<XmlNode>().FirstOrDefault(x => x.NodeType == XmlNodeType.Element && x.Name == elementName);
			return childNode != null;
		}

		public static XmlElement AddElement(XmlElement parent, string elementName)
		{
			var child = parent.OwnerDocument.CreateElement(elementName);
			parent.AppendChild(child);
			return child;
		}

		public static bool IsNil(XmlElement node)
		{
			// Check for attribute: xsi:nil="true"
			var nilAttribute = node.Attributes.Cast<XmlAttribute>().FirstOrDefault(x => x.Name == "nil" && x.NamespaceURI == xsi);
			if (nilAttribute != null)
			{
				if (String.Equals(nilAttribute.Value, "true", StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			return false;
		}

		public static XmlElement SetNil(XmlElement node)
		{
			if (node == null) return null;
			node.SetAttribute("nil", xsi, "true");
			node.IsEmpty = true;
			return node;
		}

		public static bool TryGetElementValueString(XmlElement parent, string elementName, out string value)
			=> TryGetElementValue(parent, elementName, out value, Parse_String);
		public static XmlElement AddElementValueString(XmlElement parent, string elementName, string value)
			=> AddElementValue(parent, elementName, value, ToString_String);

		public static bool TryGetElementValueInt32(XmlElement parent, string elementName, out int value)
			=> TryGetElementValue(parent, elementName, out value, Parse_Int32);
		public static XmlElement AddElementValueInt32(XmlElement parent, string elementName, int value)
			=> AddElementValue(parent, elementName, value, ToString_Int32);
		
		public static bool TryGetElementValueBool(XmlElement parent, string elementName, out bool value)
			=> TryGetElementValue(parent, elementName, out value, Parse_Bool);
		public static XmlElement AddElementValueBool(XmlElement parent, string elementName, bool value)
			=> AddElementValue(parent, elementName, value, ToString_Bool);
		
		public static bool TryGetElementValueFloat(XmlElement parent, string elementName, out float value)
			=> TryGetElementValue(parent, elementName, out value, Parse_Float);
		public static XmlElement AddElementValueFloat(XmlElement parent, string elementName, float value)
			=> AddElementValue(parent, elementName, value, ToString_Float);
		
		public static bool TryGetElementValueColor(XmlElement parent, string elementName, out Color value)
			=> TryGetElementValue(parent, elementName, out value, Parse_Color);
		public static XmlElement AddElementValueColor(XmlElement parent, string elementName, Color value)
			=> AddElementValue(parent, elementName, value, ToString_Color);

		public static bool TryGetElementValueEnum<T>(XmlElement parent, string elementName, out T value)
			where T : struct
			=> TryGetElementValue(parent, elementName, out value, Enum.TryParse);
		public static XmlElement AddElementValueEnum<T>(XmlElement parent, string elementName, T value)
			where T : struct
			=> AddElementValue(parent, elementName, value, x => x.ToString());

		//public static bool TryGetElementValueFont(XmlNode parent, string elementName, out FontDescriptor value)
		//	=> TryGetElementValue(parent, elementName, out value, Parse_Font);

		public static bool TryGetElementValue<T>(XmlElement parent, string elementName, out T value, TryParseFunc<T> func)
		{
			if (!TryGetElement(parent, elementName, out var childNode))
			{
				value = default;
				return false;
			}
			if (IsNil(childNode))
			{
				value = default;
				return true;
			}
			// The childNode must have either no child nodes, or exactly 1 child node of type "Text", otherwise childNode is not a simple text element.
			if (childNode.ChildNodes.Count > 1)
			{
				value = default;
				return false;
			}
			string textContent;
			if (childNode.ChildNodes.Count == 0)
			{
				// This handles empty tags (<TagName/>) and tags without content (<TagName></TagName>).
				// The XML specification actually requires us to treat these two cases exactly equally.
				textContent = String.Empty;
			}
			else
			{
				var textNode = childNode.ChildNodes[0];
				if (textNode.NodeType != XmlNodeType.Text)
				{
					value = default;
					return false;
				}
				textContent = textNode.Value;
			}
			if (textContent == null)
			{
				value = default;
				return false;
			}
			bool success = func(textContent, out value);
			return success;
		}

		public static XmlElement AddElementValue<T>(XmlElement parent, string elementName, T value, ToStringFunc<T> func)
		{
			var childNode = parent.OwnerDocument.CreateElement(elementName);
			parent.AppendChild(childNode);
			string valueString = func(value);
			if (valueString == null)
			{
				return SetNil(childNode);
			}
			else if (valueString == String.Empty)
			{
				childNode.IsEmpty = true;
				return childNode;
			}
			else
			{
				var textNode = parent.OwnerDocument.CreateTextNode(valueString);
				childNode.AppendChild(textNode);
				return childNode;
			}
		}

		public static bool TryGetElementValueObj(XmlElement parent, string elementName, Type t, out object value)
		{
			var field = typeof(XmlDocumentHelper).GetFields().FirstOrDefault(x => x.FieldType.GetGenericTypeDefinition() == typeof(TryParseFunc<>) && x.FieldType.GenericTypeArguments[0] == t);
			if (field == null) throw new Exception($"Unsupported serialization type: {t.FullName}");
			object try_parse_func = field.GetValue(null);
			var method = typeof(XmlDocumentHelper).GetMethod(nameof(TryGetElementValue)).MakeGenericMethod(t);
			var args = new object[] { parent, elementName, null, try_parse_func };
			bool success = (bool)method.Invoke(null, args);
			value = args[2];
			return success;
		}

		public static void AutoDeserializeSimpleObject(object target, XmlElement element)
		{
			var props = target.GetType().GetProperties();
			foreach (var prop in props)
			{
				if (TryGetElementValueObj(element, prop.Name, prop.PropertyType, out object value))
				{
					prop.SetValue(target, value);
				}
			}
		}

		public static readonly TryParseFunc<string> Parse_String = (string text, out string value) => { value = text; return true; };
		public static readonly ToStringFunc<string> ToString_String = value => value;
		public static readonly TryParseFunc<int> Parse_Int32 = (string text, out int value) => Int32.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
		public static readonly ToStringFunc<int> ToString_Int32 = value => value.ToString(CultureInfo.InvariantCulture);
		public static readonly TryParseFunc<bool> Parse_Bool = (string text, out bool value) => Boolean.TryParse(text, out value);
		public static readonly ToStringFunc<bool> ToString_Bool = value => value.ToString(CultureInfo.InvariantCulture);
		public static readonly TryParseFunc<float> Parse_Float = (string text, out float value) => Single.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
		public static readonly ToStringFunc<float> ToString_Float = value => value.ToString(CultureInfo.InvariantCulture);
		public static readonly TryParseFunc<Color> Parse_Color = TryParseColor;
		public static readonly ToStringFunc<Color> ToString_Color = FormatColor;
		//public static readonly TryParseFunc<FontDescriptor> Parse_Font = TryParseFont;

		private static string FormatColor(Color color)
		{
			if (color.IsEmpty)
			{
				return String.Empty;
			}
			string s = String.Format(CultureInfo.InvariantCulture, "R={0},G={1},B={2}", color.R, color.G, color.B);
			if (color.A != 255)
			{
				s += String.Format(CultureInfo.InvariantCulture, ",A={0}", color.A);
			}
			return s;
		}

		private static bool TryParseColor(string text, out Color result)
		{
			if (text == null)
			{
				result = default;
				return false;
			}
			if (String.IsNullOrWhiteSpace(text))
			{
				result = Color.Empty;
				return true;
			}
			var parts = text.Split(',');
			int? r = null, g = null, b = null, a = null;
			for (int i = 0; i < parts.Length; ++i)
			{
				if (!TryParseColorPart(parts[i], ref r, ref g, ref b, ref a))
				{
					result = default;
					return false;
				}
			}
			if (r == null || g == null || b == null)
			{
				result = default;
				return false;
			}
			if (a == null)
			{
				a = 255;
			}
			result = Color.FromArgb(a.Value, r.Value, g.Value, b.Value);
			return true;
		}

		private static bool TryParseColorPart(string part, ref int? r, ref int? g, ref int? b, ref int? a)
		{
			var parts = part.Split('=');
			if (parts.Length != 2) return false;
			var colorString = parts[0].Trim();
			if (colorString.Length != 1) return false;
			if (!Byte.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte number)) return false;
			switch (colorString[0])
			{
				case 'R':
				case 'r':
					if (r != null) return false;
					r = number;
					break;
				case 'G':
				case 'g':
					if (g != null) return false;
					g = number;
					break;
				case 'B':
				case 'b':
					if (b != null) return false;
					b = number;
					break;
				case 'A':
				case 'a':
					if (a != null) return false;
					a = number;
					break;
				default:
					return false;
			}
			return true;
		}
	}

	delegate bool TryParseFunc<T>(string text, out T value);
	delegate string ToStringFunc<T>(T value);
}
