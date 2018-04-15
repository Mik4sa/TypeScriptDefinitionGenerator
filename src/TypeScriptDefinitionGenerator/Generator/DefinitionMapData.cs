using System.Collections.Generic;

namespace TypeScriptDefinitionGenerator
{
	public class DefinitionMapData
	{
		public string CustomName { get; set; }

		public List<string> CopyPaths { get; set; } = new List<string>();

		public List<ReferenceMetadata> ReferenceMetadata { get; set; } = new List<ReferenceMetadata>();
	}

	public class ReferenceMetadata
	{
		public string TypeName { get; set; }

		public string ProjectName { get; set; }

		public string ProjectItemName { get; set; }
	}
}
