using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
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
			else
			{
				var textNode = parent.OwnerDocument.CreateTextNode(valueString);
				childNode.AppendChild(textNode);
				return childNode;
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
		//public static readonly TryParseFunc<Color> Parse_Font = TryParseFont;
		//public static readonly TryParseFunc<FontDescriptor> Parse_Font = TryParseFont;
	}

	delegate bool TryParseFunc<T>(string text, out T value);
	delegate string ToStringFunc<T>(T value);
}
