using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Annotations;
using System.Windows.Media;
using System.Xml;
using System.Xml.Serialization;

namespace Microsoft.Xaml.Behaviors;

internal sealed class Serializer
{
	public class Data
	{
		public class RuntimeOptionsData
		{
			public bool HideNavigation { get; set; }

			public bool HideAnnotationAndInk { get; set; }

			public bool DisableInking { get; set; }

			public bool HideDesignTimeAnnotations { get; set; }

			public bool ShowDesignTimeAnnotationsAtStart { get; set; }
		}

		public class ViewStateData
		{
			public double Zoom { get; set; }

			public Point? Center { get; set; }
		}

		public class Screen
		{
			public ScreenType Type { get; set; }

			public string ClassName { get; set; }

			public string DisplayName { get; set; }

			public string FileName { get; set; }

			public List<Annotation> Annotations { get; set; }

			public Point Position { get; set; }

			public int? VisualTag { get; set; }

			public Screen()
			{
				Annotations = new List<Annotation>();
			}
		}

		public class VisualTag
		{
			public string Name { get; set; }

			public string Color { get; set; }
		}

		public static readonly int CurrentSchemaVersion = 2;

		public static readonly int DefaultSchemaVersion = 1;

		public static readonly int MinValidSchemaVersion = 1;

		[XmlAttribute]
		public int SchemaVersion { get; set; }

		public Guid SketchFlowGuid { get; set; }

		public string StartScreen { get; set; }

		public List<Screen> Screens { get; set; }

		public string SharePointDocumentLibrary { get; set; }

		public string SharePointProjectName { get; set; }

		public int PrototypeRevision { get; set; }

		public string BrandingText { get; set; }

		public RuntimeOptionsData RuntimeOptions { get; set; }

		public List<VisualTag> VisualTags { get; set; }

		public ViewStateData ViewState { get; set; }

		public Data()
		{
			SchemaVersion = DefaultSchemaVersion;
			RuntimeOptions = new RuntimeOptionsData();
			ViewState = new ViewStateData();
			VisualTags = new List<VisualTag>();
			Screens = new List<Screen>();
		}
	}

	private Serializer()
	{
	}

	public static Color HexStringToColor(string value)
	{
		if (value.Length != 8)
		{
			throw new InvalidOperationException("Serializer.HexStringToColor requires input of a 8-character hexadecimal string, but received '" + value + "'.");
		}
		byte a = byte.Parse(value.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
		byte r = byte.Parse(value.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
		byte g = byte.Parse(value.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
		byte b = byte.Parse(value.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
		return Color.FromArgb(a, r, g, b);
	}

	public static string ColorToHexString(Color color)
	{
		string text = color.A.ToString("X2", CultureInfo.InvariantCulture);
		string text2 = color.R.ToString("X2", CultureInfo.InvariantCulture);
		string text3 = color.G.ToString("X2", CultureInfo.InvariantCulture);
		string text4 = color.B.ToString("X2", CultureInfo.InvariantCulture);
		return text + text2 + text3 + text4;
	}

	public static void Serialize(Data data, Stream stream)
	{
		data.SchemaVersion = Data.CurrentSchemaVersion;
		XmlWriterSettings settings = new XmlWriterSettings
		{
			Encoding = Encoding.UTF8,
			Indent = true
		};
		using XmlWriter xmlWriter = XmlWriter.Create(stream, settings);
		new XmlSerializer(typeof(Data)).Serialize(xmlWriter, data);
	}

	public static Data Deserialize(string filePath)
	{
		using FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
		return Deserialize(stream);
	}

	public static Data Deserialize(Stream stream)
	{
		try
		{
			return new XmlSerializer(typeof(Data)).Deserialize(stream) as Data;
		}
		catch (InvalidOperationException)
		{
			return null;
		}
	}

	public static int? GetSchemaVersion(string filePath)
	{
		using FileStream input = new FileStream(filePath, FileMode.Open, FileAccess.Read);
		using XmlReader xmlReader = XmlReader.Create(input);
		while (xmlReader.Read())
		{
			if (xmlReader.NodeType == XmlNodeType.Element && StringComparer.InvariantCultureIgnoreCase.Equals(xmlReader.LocalName, "Data"))
			{
				xmlReader.MoveToAttribute("SchemaVersion");
				break;
			}
		}
		int? result = null;
		if (!xmlReader.EOF && int.TryParse(xmlReader.Value, out var result2))
		{
			result = result2;
		}
		return result;
	}
}
