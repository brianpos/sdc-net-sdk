using Hl7.Fhir.ElementModel;
using Hl7.Fhir.MappingLanguage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Specification;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using static Hl7.Fhir.MappingLanguage.StructureMapUtilitiesAnalyze;
using static Hl7.Fhir.Model.StructureMap;
using Task = System.Threading.Tasks.Task;

namespace Hl7.Fhir.StructuredDataCapture
{
    public class QuestionnaireResponse_Extract_StructureMap
    {
        public QuestionnaireResponse_Extract_StructureMap(Func<Canonical, StructureMap> resolveStructureMap, IResourceResolver source)
        {
            ResolveStructureMap = resolveStructureMap;
            Source = source;
        }
        public Func<Canonical, StructureMap> ResolveStructureMap { get; private set; }
        public IResourceResolver Source { get; private set; }

        internal Task<IEnumerable<Bundle.EntryComponent>> Extract(Canonical smURL, QuestionnaireResponse qr, OperationOutcome outcome)
        {
            // retrieve the StructureMap
            var sm = ResolveStructureMap(smURL);
            if (sm == null)
            {
                System.Diagnostics.Trace.WriteLine($"StructureMap {smURL} not found");
                outcome.Issue.Add(new OperationOutcome.IssueComponent()
                {
                    Code = OperationOutcome.IssueType.NotFound,
                    Severity = OperationOutcome.IssueSeverity.Error,
                    Details = new CodeableConcept(null, null, $"StructureMap {smURL} not found")
                });
            }

            if (!outcome.Success)
            {
                outcome.SetAnnotation(HttpStatusCode.BadRequest);
                return Task.FromResult<IEnumerable<Bundle.EntryComponent>>(null);
            }

			// execute the StructureMap extraction
			try
			{
                var worker = new MappingWorker(ResolveStructureMap, Source);

                // Scan the map for required StructureDefinitions for target types
                var mapCanonicals = StructureMapUtilitiesExecute.getCanonicalTypeMapping(worker, sm);

                IStructureDefinitionSummaryProvider provider = new StructureDefinitionSummaryProvider(
                    Source,
                    (string name, out string canonical) =>
                    {
                        // first assume it's a FHIR resource type and use that core content
                        if (ModelInfo.FhirTypeNameToFhirType(name).HasValue)
                        {
                            canonical = ModelInfo.CanonicalUriForFhirCoreType(name)?.Value;
                            return true;
                        }

                        // non FHIR types
                        if (mapCanonicals.ContainsKey(name))
                        {
                            canonical = mapCanonicals[name];
                            return true;
                        }

                        canonical = null;
                        return false;
                    });
                var engine = new StructureMapUtilitiesExecute(worker, null, provider);

                var tmi = provider.Provide("http://hl7.org/fhir/StructureDefinition/Bundle");

                GroupComponent g = sm.Group.First();
                var gt = g.Input.FirstOrDefault(i => i.Mode == StructureMapInputMode.Target);
                var targetType = sm.Structure.FirstOrDefault(s => s.Mode == StructureMapModelMode.Target && s.Alias == gt.Type);
                if (targetType != null)
                {
                    // narrow this list down to the type
                    tmi = provider.Provide(targetType.Url);
                }

                // Check that the source parameter is of the correct type too
                var gs = g.Input.FirstOrDefault(i => i.Mode == StructureMapInputMode.Source);
                var sourceType = sm.Structure.FirstOrDefault(s => s.Mode == StructureMapModelMode.Source && s.Alias == gs.Type);
                if (sourceType != null)
                {
                    // narrow this list down to the type
                    var source = Source.ResolveByCanonicalUri(sourceType.Url) as StructureDefinition;
                }

                var target = ElementNode.Root(provider, tmi.TypeName);
                engine.transform(null, qr.ToTypedElement(), sm, target);
                var result = target.ToPoco<Resource>();
                if (result is Bundle b)
                {
                    return Task.FromResult(b.Entry.AsEnumerable());
                }
                List<Bundle.EntryComponent> entries = new List<Bundle.EntryComponent>();
                entries.Add(new Bundle.EntryComponent() { Resource = result });
                return Task.FromResult(entries.AsEnumerable());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
                outcome.Issue.Add(new OperationOutcome.IssueComponent()
                {
                    Code = OperationOutcome.IssueType.Exception,
                    Severity = OperationOutcome.IssueSeverity.Error,
                    Details = new CodeableConcept(null, null, $"Transform error: {ex.Message}")
                });
                return Task.FromResult<IEnumerable<Bundle.EntryComponent>>(null);
            }
        }
    }

    public class MappingWorker : IWorkerContext
    {
        public Func<Canonical, StructureMap> ResolveStructureMap { get; private set; }
        IResourceResolver _source;

        public MappingWorker(Func<Canonical, StructureMap> resolveStructureMap, IResourceResolver source)
        {
            ResolveStructureMap = resolveStructureMap;
            _source = source;
        }

        public ValueSet.ExpansionComponent expandVS(ValueSet vs, bool v1, bool v2)
        {
            throw new NotImplementedException();
        }

        public T fetchResource<T>(string url) where T : Resource
        {
            var result = _source.ResolveByCanonicalUri(url);
            if (result is T value)
                return value;
            var result2 = _source.ResolveByUri(url);
            if (result2 is T value2)
                return value2;
            return null;
        }

        public T fetchResourceWithException<T>(string url) where T : Resource
        {
            var result = _source.ResolveByCanonicalUri(url);
            if (result is T value)
                return value;
            var result2 = _source.ResolveByUri(url);
            if (result2 is T value2)
                return value2;
            throw new FHIRException();
        }

        public StructureDefinition fetchTypeDefinition(string code)
        {
            var uri = ModelInfo.CanonicalUriForFhirCoreType(code);
            var result = _source.ResolveByCanonicalUri(uri);
            if (result is StructureDefinition value)
                return value;
            var result2 = _source.ResolveByUri(uri);
            if (result2 is StructureDefinition value2)
                return value2;
            // return null;
            throw new NotImplementedException();
        }

        public string getOverrideVersionNs()
        {
            return null;
            // throw new NotImplementedException();
        }

        public StructureMap getTransform(string value)
        {
            // TODO: BRIAN unclear if this is a canonical or the actual ID of the resource
            return ResolveStructureMap(new Canonical(value));
        }

        public IEnumerable<StructureMap> listTransforms(string canonicalUrlTemplate)
        {
            throw new NotImplementedException();
        }

        public string oid2Uri(string code)
        {
            throw new NotImplementedException();
        }

        public ValidationResult validateCode(TerminologyServiceOptions terminologyServiceOptions, string system, string code, string display)
        {
            // TODO: BRIAN use the terminology service to handle this properly
            return new ValidationResult() { Display = display };
        }
    }
}
