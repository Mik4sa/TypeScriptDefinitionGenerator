using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using EnvDTE;
using Microsoft.VisualStudio.TextTemplating.VSHost;

namespace TypeScriptDefinitionGenerator
{
	[Guid("d1e92907-20ee-4b6f-ba64-142297def4e4")]
	public sealed class DtsGenerator : BaseCodeGeneratorWithSite
	{
		public const string Name = nameof(DtsGenerator);
		public const string Description = "Automatically generates the .d.ts file based on the C#/VB model class.";

		string originalExt { get; set; }

		public override string GetDefaultExtension()
		{
			if (Options.IncludeOriginalExtension)
			{
				return this.originalExt + Options.GeneratedFileExtension;
			}
			else
			{
				return Options.GeneratedFileExtension;
			}
		}

		protected override byte[] GenerateCode(string inputFileName, string inputFileContent)
		{
			ProjectItem item = Dte.Solution.FindProjectItem(inputFileName);
			this.originalExt = Path.GetExtension(inputFileName);
			if (item != null)
			{
				try
				{
					// Get metadata from our project item
					DefinitionMapData definitionMapData = VSHelpers.GetDefinitionMapData(item);

					string dts = GenerationService.ConvertToTypeScript(item, definitionMapData);
					Telemetry.TrackOperation("FileGenerated");

					// Copy our dts file to the specified paths in the definition map data
					GenerationService.CopyDtsFile(definitionMapData, item, dts);

					// And in the last step write the map file which contains some metadata
					GenerationService.CreateDtsMapFile(item, definitionMapData);

					return Encoding.UTF8.GetBytes(dts);
				}
				catch (Exception ex)
				{
					Telemetry.TrackOperation("FileGenerated", Microsoft.VisualStudio.Telemetry.TelemetryResult.Failure);
					Telemetry.TrackException("FileGenerated", ex);
				}
			}

			return new byte[0];
		}
	}
}
