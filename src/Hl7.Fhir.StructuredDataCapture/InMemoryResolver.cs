using Hl7.Fhir.Model;
using Hl7.Fhir.Specification.Source;
using System;
using System.Collections.Generic;
using System.Resources;

namespace Hl7.Fhir.StructuredDataCapture
{
	public class InMemoryResolver : IResourceResolver
	{
		Dictionary<string, StructureDefinition> _structureDefinitions = new();

		public void Add(StructureDefinition definition)
		{
			string canonicalUrl = definition.Url;
			if (!string.IsNullOrEmpty(canonicalUrl))
			{
				if (!_structureDefinitions.ContainsKey(canonicalUrl))
					_structureDefinitions.Add(canonicalUrl, definition);
				else
					_structureDefinitions[canonicalUrl] = definition;
			}
		}

		public void Add(Bundle bundle)
		{
			foreach (var resource in bundle.GetResources())
			{
				if (resource is StructureDefinition sd)
					Add(sd);
			}
		}

		public Resource ResolveByCanonicalUri(string uri)
		{
            if (_structureDefinitions.ContainsKey(uri))
				return _structureDefinitions[uri];
            return null;
		}

		public Resource ResolveByUri(string uri)
		{
			return null;
		}
	}
}
