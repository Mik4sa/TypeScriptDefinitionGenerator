using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using EnvDTE;
using TypeScriptDefinitionGenerator.Helpers;

namespace TypeScriptDefinitionGenerator
{
	internal static class IntellisenseWriter
	{
		private static readonly Regex _whitespaceTrimmer = new Regex(@"^\s+|\s+$|\s*[\r\n]+\s*", RegexOptions.Compiled);

		private static List<string> GetReferences(IEnumerable<IntellisenseObject> objects, ProjectItem sourceItem)
		{
			if (Options.KeepReferencesUnchanged)
			{
				ProjectItem generatedProjectItem = sourceItem.ProjectItems
					.Cast<ProjectItem>()
					.Where(item => GenerationService.GenerateFileName(sourceItem.Name) == item.Name)
					.FirstOrDefault();

				if (generatedProjectItem != null)
				{
					string documentText = VSHelpers.GetDocumentText(generatedProjectItem);

					if (string.IsNullOrWhiteSpace(documentText) == false)
					{
						string pattern = "/// <reference path=\"(.*)\" />\r\n";
						return new Regex(pattern).Matches(documentText)
							.Cast<Match>()
							.Select(m => m.Groups[1].Value)
							.OrderBy(r => r)
							.ToList();
					}
				}
			}

			return objects
					.SelectMany(o => o.References)
					.Where(r => Path.GetFileName(r) != GenerationService.GenerateFileName(sourceItem.Name))
					.Distinct()
					.OrderBy(r => r)
					.ToList();
		}

		public static string WriteTypeScript(IEnumerable<IntellisenseObject> objects, ProjectItem sourceItem)
		{
			var sb = new StringBuilder();

			foreach (var ns in objects.GroupBy(o => o.Namespace))
			{
				List<string> references = GetReferences(objects, sourceItem);

				if (references.Count > 0)
				{
					foreach (string referencePath in references)
					{
						string path = Path.GetFileName(referencePath);

						ProjectItem definitionMapProjectItem = sourceItem.DTE.Solution.FindProjectItem(referencePath);

						if (definitionMapProjectItem != null)
						{
							DefinitionMapData definitionMapData = VSHelpers.GetDefinitionMapData(definitionMapProjectItem.Collection.Parent as ProjectItem);

							if (definitionMapData != null)
							{
								if (string.IsNullOrWhiteSpace(definitionMapData.CustomName) == false)
								{
									path = GenerationService.GetCopyDtsFileName(definitionMapData, definitionMapProjectItem);
								}
							}
						}

						sb.AppendFormat("/// <reference path=\"{0}\" />\r\n", path);
					}

					sb.AppendLine();
				}

				if (!Options.GlobalScope)
				{
					sb.AppendFormat("declare module {0} {{\r\n", ns.Key);
				}

				foreach (IntellisenseObject io in ns)
				{
					if (!string.IsNullOrEmpty(io.Summary))
						sb.AppendLine("\t/** " + _whitespaceTrimmer.Replace(io.Summary, "") + " */");

					if (io.IsEnum)
					{
						sb.AppendLine("\tconst enum " + Utility.CamelCaseClassName(io.Name) + " {");

						foreach (var p in io.Properties)
						{
							WriteTypeScriptComment(p, sb);

							if (p.InitExpression != null)
							{
								sb.AppendLine("\t\t" + Utility.CamelCaseEnumValue(p.Name) + " = " + CleanEnumInitValue(p.InitExpression) + ",");
							}
							else
							{
								sb.AppendLine("\t\t" + Utility.CamelCaseEnumValue(p.Name) + ",");
							}
						}

						sb.AppendLine("\t}");
					}
					else
					{
						string type = Options.ClassInsteadOfInterface ? "\tclass " : "\tinterface ";
						sb.Append(type).Append(Utility.CamelCaseClassName(io.Name)).Append(" ");

						if (!string.IsNullOrEmpty(io.BaseName))
						{
							sb.Append("extends ");

							if (!string.IsNullOrEmpty(io.BaseNamespace) && io.BaseNamespace != io.Namespace)
								sb.Append(io.BaseNamespace).Append(".");

							sb.Append(Utility.CamelCaseClassName(io.BaseName)).Append(" ");
						}

						WriteTSInterfaceDefinition(sb, "\t", io.Properties);
						sb.AppendLine();
					}
				}

				if (!Options.GlobalScope)
				{
					sb.AppendLine("}");
				}
			}

			return sb.ToString();
		}

		private static string CleanEnumInitValue(string value)
		{
			value = value.TrimEnd('u', 'U', 'l', 'L'); //uint ulong long
			if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return value;
			var trimedValue = value.TrimStart('0'); // prevent numbers to be parsed as octal in js.
			if (trimedValue.Length > 0) return trimedValue;
			return "0";
		}


		private static void WriteTypeScriptComment(IntellisenseProperty p, StringBuilder sb)
		{
			if (string.IsNullOrEmpty(p.Summary)) return;
			sb.AppendLine("\t\t/** " + _whitespaceTrimmer.Replace(p.Summary, "") + " */");
		}

		private static void WriteTSInterfaceDefinition(StringBuilder sb, string prefix,
			IEnumerable<IntellisenseProperty> props)
		{
			sb.AppendLine("{");

			foreach (var p in props)
			{
				WriteTypeScriptComment(p, sb);
				sb.AppendFormat("{0}\t{1}: ", prefix, Utility.CamelCasePropertyName(p.NameWithOption));

				if (p.Type.IsKnownType) sb.Append(p.Type.TypeScriptName);
				else
				{
					if (p.Type.Shape == null) sb.Append("any");
					else WriteTSInterfaceDefinition(sb, prefix + "\t", p.Type.Shape);
				}
				if (p.Type.IsArray) sb.Append("[]");

				sb.AppendLine(";");
			}

			sb.Append(prefix).Append("}");
		}
	}
}
