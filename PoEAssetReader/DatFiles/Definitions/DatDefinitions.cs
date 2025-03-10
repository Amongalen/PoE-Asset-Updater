using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;

namespace PoEAssetReader.DatFiles.Definitions
{
	public class DatDefinitions
	{
		#region Consts

		private static string ApplicationVersion => Assembly.GetAssembly(typeof(DatDefinitions)).GetName().Version.ToString();

		#endregion

		private DatDefinitions(FileDefinition[] fileDefinitions)
		{
			FileDefinitions = fileDefinitions;
		}

		#region Properties

		public FileDefinition[] FileDefinitions
		{
			get;
		}

		#endregion

		#region Public Methods

		public static DatDefinitions ParseJson(string definitionsFileName)
		{
			using StreamReader streamReader = new StreamReader(definitionsFileName);
			using JsonReader reader = new JsonTextReader(streamReader);
			List<FileDefinition> fileDefinitions = new List<FileDefinition>();
			var jsonFileDefinitions = JArray.Load(reader);
			foreach(JObject jsonFileDefinition in jsonFileDefinitions.Children())
			{
				string name = jsonFileDefinition.Value<string>("name");
				List<FieldDefinition> fields = new List<FieldDefinition>();
				foreach(JObject jsonFieldDefinition in jsonFileDefinition.Values("fields"))
				{
					string id = jsonFieldDefinition.Value<string>("id");
					string dataType = jsonFieldDefinition.Value<string>("type");
					fields.Add(new FieldDefinition(id, TypeDefinition.Parse(dataType), null, null));
				}
				fileDefinitions.Add(new FileDefinition(name, fields.ToArray()));
			}
			return new DatDefinitions(fileDefinitions.ToArray());
		}

		public static DatDefinitions ParseLocalPyPoE(string fileName) => ParsePyPoE(File.ReadAllText(fileName));

		public static DatDefinitions ParsePyPoE()
		{
			using WebClient wc = new WebClient();

			wc.Headers[HttpRequestHeader.UserAgent] = "PoEOverlayAssetReader/" + ApplicationVersion;
			try
			{
				return ParsePyPoE(wc.DownloadString("https://raw.githubusercontent.com/brather1ng/PyPoE/dev/PyPoE/poe/file/specification/data/stable.py"));
			}
			catch(Exception ex)
			{
				throw new Exception($"Failed to download definitions from PyPoE's GitHub.", ex);
			}
		}

		public static DatDefinitions ParsePyPoE(string definitionsFileContent)
		{
			string[] lines = definitionsFileContent.Split("\r\n".ToCharArray());
			List<FileDefinition> fileDefinitions = new List<FileDefinition>();
			for(int i = 0; i < lines.Length; i++)
			{
				string line = lines[i];
				if(line.EndsWith("': File("))
				{
					string name = line.Split("'")[1];
					List<FieldDefinition> fields = new List<FieldDefinition>();
					for(i++; i < lines.Length; i++)
					{
						line = lines[i];
						if(line.EndsWith("Field("))
						{
							bool findEnd = false;
							line = FindLine(ref lines, ref i, s => s.Trim().StartsWith("name='"));
							string id = line.Split("'")[1];

							line = FindLine(ref lines, ref i, s => s.Trim().StartsWith("type='"));
							string dataType = line.Split("'")[1];

							// Find the ref dat file or closing tag for this Field.
							line = FindLine(ref lines, ref i, s => IsRefDatFileName(s) || IsEndOfSection(s));
							string refDatFileName = null;
							string refDatFieldID = null;
							if(IsRefDatFileName(line))
							{
								refDatFileName = line.Split("'")[1];

								line = FindLine(ref lines, ref i, s => IsRefDataFieldID(s) || IsEndOfSection(s));
								if(IsRefDataFieldID(line))
								{
									refDatFieldID = line.Split("'")[1];
									findEnd = true;
								}
							}

							fields.Add(new FieldDefinition(id, TypeDefinition.Parse(dataType), refDatFileName, refDatFieldID));

							if(findEnd)
							{
								FindLine(ref lines, ref i, IsEndOfSection);
							}
						}
						else if(IsEndOfSection(line))
						{
							break;
						}
					}
					fileDefinitions.Add(new FileDefinition(name, fields.ToArray()));
				}
			}
			return new DatDefinitions(fileDefinitions.ToArray());

			// Nested Method(s)
			static string FindLine(ref string[] lines, ref int idx, Predicate<string> predicate)
			{
				for(; idx < lines.Length; idx++)
				{
					string line = lines[idx];
					if(predicate(line))
					{
						return line;
					}
				}
				return string.Empty;
			}

			static bool IsEndOfSection(string input)
			{
				return input.Trim().EndsWith("),");
			}

			static bool IsRefDatFileName(string input)
			{
				return input.Trim().StartsWith("key='");
			}

			static bool IsRefDataFieldID(string input)
			{
				return input.Trim().StartsWith("key_id='");
			}
		}

		#endregion
	}
}
