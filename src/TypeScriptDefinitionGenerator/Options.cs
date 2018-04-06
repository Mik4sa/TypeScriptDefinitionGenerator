using System;
using System.ComponentModel;
using System.IO;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json;

namespace TypeScriptDefinitionGenerator
{
	public class OptionsDialogPage : DialogPage
	{
		internal const bool _defCamelCaseEnumerationValues = false;
		internal const bool _defCamelCasePropertyNames = true;
		internal const bool _defCamelCaseTypeNames = false;
		internal const bool _defClassInsteadOfInterface = false;
		internal const bool _defGlobalScope = false;
		internal const string _defModuleName = "Server.Dtos";
		internal const bool _defIncludeOriginalExtension = true;
		internal const string _defGeneratedFileExtension = ".d.ts";
		internal const bool _defAssumeExternalType = false;
		internal const bool _defKeepReferencesUnchanged = false;

		[Category("Casing")]
		[DisplayName("Camel case enum values")]
		[DefaultValue(_defCamelCaseEnumerationValues)]
		public bool CamelCaseEnumerationValues { get; set; } = _defCamelCaseEnumerationValues;

		[Category("Casing")]
		[DisplayName("Camel case property names")]
		[DefaultValue(_defCamelCasePropertyNames)]
		public bool CamelCasePropertyNames { get; set; } = _defCamelCasePropertyNames;

		[Category("Casing")]
		[DisplayName("Camel case type names")]
		[DefaultValue(_defCamelCaseTypeNames)]
		public bool CamelCaseTypeNames { get; set; } = _defCamelCaseTypeNames;

		[Category("Settings")]
		[DisplayName("Default Module name")]
		[Description("Set the top-level module name for the generated .d.ts file. Default is \"Server.Dtos\"")]
		public string DefaultModuleName { get; set; } = _defModuleName;

		[Category("Settings")]
		[DisplayName("Class instead of Interface")]
		[Description("Controls whether to generate a class or an interface: default is an Interface")]
		[DefaultValue(_defClassInsteadOfInterface)]
		public bool ClassInsteadOfInterface { get; set; } = _defClassInsteadOfInterface;

		[Category("Settings")]
		[DisplayName("Generate in global scope")]
		[Description("Controls whether to generate types in Global scope or wrapped in a module")]
		[DefaultValue(_defGlobalScope)]
		public bool GlobalScope { get; set; } = _defGlobalScope;

		[Category("Settings")]
		[DisplayName("Assume external type")]
		[Description("Assume that the external types are existing (better performance while saving files, may lead to incorrect generations)")]
		[DefaultValue(_defAssumeExternalType)]
		public bool AssumeExternalType { get; set; } = _defAssumeExternalType;

		[Category("Settings")]
		[DisplayName("Keep references unchanged")]
		[Description("Won't modify the reference part of an existing typescript definition file")]
		[DefaultValue(_defKeepReferencesUnchanged)]
		public bool KeepReferencesUnchanged { get; set; } = _defKeepReferencesUnchanged;


		[Category("Compatibilty")]
		[DisplayName("Include original extension in filename")]
		[DefaultValue(_defIncludeOriginalExtension)]
		public bool IncludeOriginalExtension { get; set; } = _defIncludeOriginalExtension;

		[Category("Compatibilty")]
		[DisplayName("File extension to append to the generated filename")]
		[DefaultValue(_defGeneratedFileExtension)]
		public string GeneratedFileExtension { get; set; } = _defGeneratedFileExtension;
	}

	public class Options
	{
		const string OVERRIDE_FILE_NAME = "tsdefgen.json";
		static OptionsOverride overrides { get; set; } = null;
		static public bool CamelCaseEnumerationValues
		{
			get
			{
				return overrides != null ? overrides.CamelCaseEnumerationValues : DtsPackage.Options.CamelCaseEnumerationValues;
			}
		}

		static public bool CamelCasePropertyNames
		{
			get
			{
				return overrides != null ? overrides.CamelCasePropertyNames : DtsPackage.Options.CamelCasePropertyNames;
			}
		}

		static public bool CamelCaseTypeNames
		{
			get
			{
				return overrides != null ? overrides.CamelCaseTypeNames : DtsPackage.Options.CamelCaseTypeNames;
			}
		}
		//todo:设置为服务器命名空间
		static public string DefaultModuleName
		{
			get
			{
				return overrides != null ? overrides.DefaultModuleName : DtsPackage.Options.DefaultModuleName;
			}
		}

		static public bool ClassInsteadOfInterface
		{
			get
			{
				return overrides != null ? overrides.ClassInsteadOfInterface : DtsPackage.Options.ClassInsteadOfInterface;
			}
		}

		static public bool GlobalScope
		{
			get
			{
				return overrides != null ? overrides.GlobalScope : DtsPackage.Options.GlobalScope;
			}
		}

		static public bool AssumeExternalType
		{
			get
			{
				return overrides != null ? overrides.AssumeExternalType : DtsPackage.Options.AssumeExternalType;
			}
		}

		static public bool KeepReferencesUnchanged
		{
			get
			{
				return overrides != null ? overrides.KeepReferencesUnchanged : DtsPackage.Options.KeepReferencesUnchanged;
			}
		}

		static public bool IncludeOriginalExtension
		{
			get
			{
				return overrides != null ? overrides.IncludeOriginalExtension : DtsPackage.Options.IncludeOriginalExtension;
			}
		}

		static public string GeneratedFileExtension
		{
			get
			{
				return overrides != null ? overrides.GeneratedFileExtension : DtsPackage.Options.GeneratedFileExtension;
			}
		}

		public static void ReadOptionOverrides(ProjectItem sourceItem, bool display = true)
		{
			Project proj = sourceItem.ContainingProject;

			string jsonName = "";

			foreach (ProjectItem item in proj.ProjectItems)
			{
				if (item.Name.ToLower() == OVERRIDE_FILE_NAME.ToLower())
				{
					jsonName = item.FileNames[0];
					break;
				}
			}

			if (!string.IsNullOrEmpty(jsonName))
			{
				// it has been modified since last read - so read again
				try
				{
					overrides = JsonConvert.DeserializeObject<OptionsOverride>(File.ReadAllText(jsonName));
					if (display)
					{
						VSHelpers.WriteOnOutputWindow(string.Format("Override file processed: {0}", jsonName));
					}
					else
					{
						System.Diagnostics.Debug.WriteLine(string.Format("Override file processed: {0}", jsonName));
					}
				}
				catch (Exception e) when (e is Newtonsoft.Json.JsonReaderException || e is Newtonsoft.Json.JsonSerializationException)
				{
					overrides = null; // incase the read fails
					VSHelpers.WriteOnOutputWindow(string.Format("Error in Override file: {0}", jsonName));
					VSHelpers.WriteOnOutputWindow(e.Message);
					throw;
				}
			}
			else
			{
				if (display)
				{
					VSHelpers.WriteOnOutputWindow("Using Global Settings");
				}
				else
				{
					System.Diagnostics.Debug.WriteLine("Using Global Settings");
				}
				overrides = null;
			}
		}

	}

	internal class OptionsOverride
	{
		//        [JsonRequired]
		public bool CamelCaseEnumerationValues { get; set; } = OptionsDialogPage._defCamelCaseEnumerationValues;

		//        [JsonRequired]
		public bool CamelCasePropertyNames { get; set; } = OptionsDialogPage._defCamelCasePropertyNames;

		//        [JsonRequired]
		public bool CamelCaseTypeNames { get; set; } = OptionsDialogPage._defCamelCaseTypeNames;

		//        [JsonRequired]
		public string DefaultModuleName { get; set; } = OptionsDialogPage._defModuleName;

		//        [JsonRequired]
		public bool ClassInsteadOfInterface { get; set; } = OptionsDialogPage._defClassInsteadOfInterface;

		//        [JsonRequired]
		public bool GlobalScope { get; set; } = OptionsDialogPage._defGlobalScope;

		//        [JsonRequired]
		public bool AssumeExternalType { get; set; } = OptionsDialogPage._defAssumeExternalType;

		//        [JsonRequired]
		public bool KeepReferencesUnchanged { get; set; } = OptionsDialogPage._defKeepReferencesUnchanged;

		//        [JsonRequired]
		public bool IncludeOriginalExtension { get; set; } = OptionsDialogPage._defIncludeOriginalExtension;

		//        [JsonRequired]
		public string GeneratedFileExtension { get; set; } = OptionsDialogPage._defGeneratedFileExtension;

	}

}
