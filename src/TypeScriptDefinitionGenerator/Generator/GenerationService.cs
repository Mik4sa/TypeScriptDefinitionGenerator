using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Windows.Threading;
using EnvDTE;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Newtonsoft.Json;

namespace TypeScriptDefinitionGenerator
{
	[Export(typeof(IWpfTextViewCreationListener))]
	[ContentType("csharp")]
	[ContentType("basic")]
	[TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
	public class GenerationService : IWpfTextViewCreationListener
	{
		private ProjectItem _item;

		[Import]
		public ITextDocumentFactoryService _documentService { get; set; }

		public void TextViewCreated(IWpfTextView textView)
		{
			if (!_documentService.TryGetTextDocument(textView.TextBuffer, out var doc))
				return;

			_item = VSHelpers.GetProjectItem(doc.FilePath);

			if (_item?.ContainingProject == null ||
				!_item.ContainingProject.IsKind(ProjectTypes.DOTNET_Core, ProjectTypes.ASPNET_5, ProjectTypes.WEBSITE_PROJECT))
				return;

			doc.FileActionOccurred += FileActionOccurred;
		}

		private void FileActionOccurred(object sender, TextDocumentFileActionEventArgs e)
		{
			if (e.FileActionType != FileActionTypes.ContentSavedToDisk)
				return;
			_item = VSHelpers.GetProjectItem(e.FilePath);
			Options.ReadOptionOverrides(_item, false);
			string fileName = GenerationService.GenerateFileName(e.FilePath);

			if (File.Exists(fileName))
			{
				DtsPackage.EnsurePackageLoad();
				CreateDtsFile(_item);
			}
		}

		public static string ConvertToTypeScriptWithoutEnums(ProjectItem sourceItem, ref DefinitionMapData definitionMapData, out bool isEmpty)
		{
			try
			{
				// Initialize the definition data if there was no specified
				if (definitionMapData == null)
				{
					definitionMapData = new DefinitionMapData();
				}

				Options.ReadOptionOverrides(sourceItem);
				VSHelpers.WriteOnOutputWindow(string.Format("{0} - Started (no enums)", sourceItem.Name));
				var list = IntellisenseParser.ProcessFile(sourceItem, definitionMapData);
				VSHelpers.WriteOnOutputWindow(string.Format("{0} - Completed", sourceItem.Name));
				return IntellisenseWriter.WriteTypeScriptWithoutEnums(list, sourceItem, out isEmpty);
			}
			catch (Exception ex)
			{
				isEmpty = true;

				VSHelpers.WriteOnOutputWindow(string.Format("{0} - Failure", sourceItem.Name));
				Telemetry.TrackException("ParseFailure", ex);
				return null;
			}
		}

		public static string ConvertToTypeScriptEnumsOnly(ProjectItem sourceItem, ref DefinitionMapData definitionMapData, out bool isEmpty)
		{
			try
			{
				// Initialize the definition data if there was no specified
				if (definitionMapData == null)
				{
					definitionMapData = new DefinitionMapData();
				}

				Options.ReadOptionOverrides(sourceItem);
				VSHelpers.WriteOnOutputWindow(string.Format("{0} - Started (enums only)", sourceItem.Name));
				var list = IntellisenseParser.ProcessFile(sourceItem, definitionMapData);
				VSHelpers.WriteOnOutputWindow(string.Format("{0} - Completed", sourceItem.Name));
				return IntellisenseWriter.WriteTypeScriptEnumsOnly(list, sourceItem, out isEmpty);
			}
			catch (Exception ex)
			{
				isEmpty = true;

				VSHelpers.WriteOnOutputWindow(string.Format("{0} - Failure", sourceItem.Name));
				Telemetry.TrackException("ParseFailure", ex);
				return null;
			}
		}

		public static string GenerateFileName(string sourceFile)
		{
			if (sourceFile.EndsWith(Options.GeneratedFileExtension))
			{
				return sourceFile;
			}
			else if (Options.IncludeOriginalExtension)
			{
				return sourceFile + Options.GeneratedFileExtension;
			}
			else
			{
				return Path.ChangeExtension(sourceFile, Options.GeneratedFileExtension);
			}
		}

		public static void CreateDtsFile(ProjectItem sourceItem)
		{
			string sourceFile = sourceItem.FileNames[1];
			string dtsFile = GenerationService.GenerateFileName(sourceFile);
			string dtsEnumFile = GenerationService.GenerateFileName(sourceFile);

			// Get metadata from our project item
			DefinitionMapData definitionMapData = VSHelpers.GetDefinitionMapData(sourceItem);

			string dts = ConvertToTypeScriptWithoutEnums(sourceItem, ref definitionMapData, out bool isEmpty);
			string dtsEnumOnly = ConvertToTypeScriptEnumsOnly(sourceItem, ref definitionMapData, out bool isEmptyEnum);

			VSHelpers.CheckFileOutOfSourceControl(dtsFile);
			File.WriteAllText(dtsFile, dts);

			if (isEmptyEnum == false)
			{
				File.WriteAllText(dtsEnumFile, dtsEnumOnly);
			}

			if (sourceItem.ContainingProject.IsKind(ProjectTypes.DOTNET_Core, ProjectTypes.ASPNET_5))
			{
				Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
				{
					var dtsItem = VSHelpers.GetProjectItem(dtsFile);

					if (dtsItem != null)
						dtsItem.Properties.Item("DependentUpon").Value = sourceItem.Name;

					if (isEmptyEnum == false)
					{
						var dtsItem2 = VSHelpers.GetProjectItem(dtsEnumFile);

						if (dtsItem2 != null)
							dtsItem2.Properties.Item("DependentUpon").Value = sourceItem.Name;
					}

					Telemetry.TrackOperation("FileGenerated");
				}), DispatcherPriority.ApplicationIdle, null);
			}
			else if (sourceItem.ContainingProject.IsKind(ProjectTypes.WEBSITE_PROJECT))
			{
				sourceItem.ContainingProject.ProjectItems.AddFromFile(dtsFile);

				if (isEmptyEnum == false)
				{
					sourceItem.ContainingProject.ProjectItems.AddFromFile(dtsEnumFile);
				}
			}

			// Also create the definition map data and add it to our project item
			CreateDtsMapFile(sourceItem, definitionMapData);
		}

		public static void CreateDtsMapFile(ProjectItem sourceItem, DefinitionMapData definitionMapData)
		{
			string sourceFile = sourceItem.FileNames[1];
			string dtsFile = GenerationService.GenerateFileName(sourceFile) + ".map";

			VSHelpers.CheckFileOutOfSourceControl(dtsFile);
			File.WriteAllText(dtsFile, JsonConvert.SerializeObject(definitionMapData, Formatting.Indented));

			if (sourceItem.ContainingProject.IsKind(ProjectTypes.DOTNET_Core, ProjectTypes.ASPNET_5))
			{
				Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
				{
					var dtsItem = VSHelpers.GetProjectItem(dtsFile);

					if (dtsItem != null)
						dtsItem.Properties.Item("DependentUpon").Value = sourceItem.Name;

					Telemetry.TrackOperation("FileGenerated");
				}), DispatcherPriority.ApplicationIdle, null);
			}
			else if (sourceItem.ContainingProject.IsKind(ProjectTypes.WEBSITE_PROJECT))
			{
				sourceItem.ContainingProject.ProjectItems.AddFromFile(dtsFile);
			}
			else
			{
				sourceItem.ProjectItems.AddFromFile(dtsFile);
			}
		}

		public static void CreateEnumFile(ProjectItem sourceItem, string content)
		{
			string sourceFile = sourceItem.FileNames[1];
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(sourceFile);

			while (fileNameWithoutExtension.Contains("."))
			{
				fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileNameWithoutExtension);
			}

			string dtsFile = Path.Combine(Path.GetDirectoryName(sourceFile), fileNameWithoutExtension += "Enum.ts");

			VSHelpers.CheckFileOutOfSourceControl(dtsFile);
			File.WriteAllText(dtsFile, content);

			if (sourceItem.ContainingProject.IsKind(ProjectTypes.DOTNET_Core, ProjectTypes.ASPNET_5))
			{
				Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
				{
					var dtsItem = VSHelpers.GetProjectItem(dtsFile);

					if (dtsItem != null)
						dtsItem.Properties.Item("DependentUpon").Value = sourceItem.Name;

					Telemetry.TrackOperation("FileGenerated");
				}), DispatcherPriority.ApplicationIdle, null);
			}
			else if (sourceItem.ContainingProject.IsKind(ProjectTypes.WEBSITE_PROJECT))
			{
				sourceItem.ContainingProject.ProjectItems.AddFromFile(dtsFile);
			}
			else
			{
				sourceItem.ProjectItems.AddFromFile(dtsFile);
			}
		}

		public static string GetCopyDtsFileName(DefinitionMapData definitionMapData, ProjectItem projectItem, bool isEnumDefinition)
		{
			string sourceFile = string.IsNullOrWhiteSpace(definitionMapData.CustomName) ? projectItem.Name : definitionMapData.CustomName;

			if (isEnumDefinition)
			{
				sourceFile += "Enum";
			}

			return GenerationService.GenerateFileName(sourceFile);
		}

		public static void CopyDtsFile(DefinitionMapData definitionMapData, ProjectItem projectItem, string dts, bool isEnumDefinition)
		{
			// There might be paths where this file should be copied to
			foreach (string copyPath in definitionMapData.CopyPaths)
			{
				// Ignore empty paths
				if (string.IsNullOrWhiteSpace(copyPath))
					continue;

				// Get the path from our project item and combine it with the target path and target name
				string filePath = Path.GetFullPath(Path.Combine(
					Path.GetDirectoryName(projectItem.FileNames[1]),
					copyPath,
					GenerationService.GetCopyDtsFileName(definitionMapData, projectItem, isEnumDefinition)));

				// Try to write our definition file to the new path too
				try
				{
					File.WriteAllText(filePath, dts);
					VSHelpers.WriteOnOutputWindow($"File written to \"{filePath}\"");
				}
				catch (Exception ex)
				{
					VSHelpers.WriteOnOutputWindow($"Could not write file to \"{filePath}\"{Environment.NewLine}" +
						$"Reason: {ex.Message}");
				}
			}
		}
	}
}
