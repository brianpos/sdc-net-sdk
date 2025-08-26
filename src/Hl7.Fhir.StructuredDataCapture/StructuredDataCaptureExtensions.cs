using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Hl7.Fhir.StructuredDataCapture
{
	public static class StructuredDataCaptureExtensions
	{
		public static string ToFhirUrnUuid(this Guid me)
		{
			return "urn:uuid:" + me.ToString("D");
		}

		//
		// Summary:
		//     Convert the FhirDateTime to a DateTimeOffset, but without forcing the timezone
		//     offset to some arbitrary zone instead retaining the offset included in the value
		//     (this routine will set to UTC if the content does not contain a zone itself)
		//
		//
		// Parameters:
		//   me:
		public static DateTimeOffset? ToDateTimeOffsetRetainTimezone(this FhirDateTime me)
		{
			if (me.Value.IndexOf(":") == 2 || me.Value.IndexOf(":") == 3)
			{
				return null;
			}

			if (!me.Value.Contains("T") && me.Value.Length <= 10)
			{
				return XmlConvert.ToDateTimeOffset(me.Value + "Z");
			}

			return XmlConvert.ToDateTimeOffset(me.Value);
		}

		public static IEnumerable<Canonical> CqfLibrary(this Questionnaire q)
		{
			var result = q.GetExtensions("http://hl7.org/fhir/StructureDefinition/cqf-library");
			return result.Where(e => e.Value is Canonical).Select(e => e.Value as Canonical);
		}

		public static Expression InitialExpression(this Questionnaire.ItemComponent item)
		{
			var result = item.GetExtensionValue<Expression>("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-initialExpression");
			return result;
		}

		/// <summary>
		/// observationLinkPeriod
		/// http://build.fhir.org/ig/HL7/sdc/StructureDefinition-sdc-questionnaire-observationLinkPeriod.html
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public static Duration ObservationLinkPeriod(this Questionnaire.ItemComponent item)
		{
			var result = item.GetExtensionValue<Duration>("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-observationLinkPeriod");
			if (result == null) // also check the older name for this extension
				result = item.GetExtensionValue<Duration>("http://hl7.org/fhir/StructureDefinition/questionnaire-observationLinkPeriod");
			return result;
		}

		/// <summary>
		/// https://hl7.org/fhir/extension-questionnaire-unitoption.html
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public static IEnumerable<Coding> UnitOptions(this Questionnaire.ItemComponent item)
		{
			var result = item.GetExtensions("http://hl7.org/fhir/StructureDefinition/questionnaire-unitOption");
			return result.Where(e => e.Value is Coding).Select(e => e.Value as Coding);
		}

		/// <summary>
		/// https://hl7.org/fhir/extension-questionnaire-unit.html
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public static Coding Unit(this Questionnaire.ItemComponent item)
		{
			var result = item.GetExtensionValue<Coding>("http://hl7.org/fhir/StructureDefinition/questionnaire-unit");
			return result;
		}

		/// <summary>
		/// http://hl7.org/fhir/R4/extension-questionnaire-unitvalueset.html
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static string UnitValueSet(this Questionnaire.ItemComponent me)
		{
			var result = me.GetExtensionValue<Canonical>("http://hl7.org/fhir/StructureDefinition/questionnaire-unitValueSet")?.Value;
			return result;
		}

		/// <summary>
		/// https://www.hl7.org/fhir/extension-mimetype.html
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public static IEnumerable<string> MimeTypes(this Questionnaire.ItemComponent item)
		{
			var result = item.GetExtensions("http://hl7.org/fhir/StructureDefinition/mimeType");
			return result.Select(e => (e.Value as FhirString)?.Value ?? (e.Value as Code)?.Value).SkipWhile(s => string.IsNullOrEmpty(s));
		}

		/// <summary>
		/// http://hl7.org/fhir/extension-maxsize.html
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public static decimal? MaxSize(this Questionnaire.ItemComponent item)
		{
			var result = item.GetExtensionValue<FhirDecimal>("http://hl7.org/fhir/StructureDefinition/maxSize")?.Value;
			return result;
		}

		/// <summary>
		/// https://hl7.org/fhir/extension-minlength.html
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public static int? MinLength(this Questionnaire.ItemComponent item)
		{
			var result = item.GetExtensionValue<Integer>("http://hl7.org/fhir/StructureDefinition/minLength")?.Value;
			return result;
		}

		/// <summary>
		/// <a href="https://hl7.org/fhir/extension-questionnaire-minoccurs.html">https://hl7.org/fhir/extension-questionnaire-minoccurs.html</a>
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public static int? MinOccurs(this Questionnaire.ItemComponent item)
		{
			var result = item.GetExtensionValue<Integer>("http://hl7.org/fhir/StructureDefinition/questionnaire-minOccurs")?.Value;
			return result;
		}

		/// <summary>
		/// https://hl7.org/fhir/extension-questionnaire-maxoccurs.html
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public static int? MaxOccurs(this Questionnaire.ItemComponent item)
		{
			var result = item.GetExtensionValue<Integer>("http://hl7.org/fhir/StructureDefinition/questionnaire-maxOccurs")?.Value;
			return result;
		}

		/// <summary>
		/// http://hl7.org/fhir/StructureDefinition/minValue
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public static Base MinValue(this Questionnaire.ItemComponent item)
		{
			var result = item.GetExtension("http://hl7.org/fhir/StructureDefinition/minValue");
			return result?.Value;
		}

		/// <summary>
		/// http://hl7.org/fhir/StructureDefinition/maxValue
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public static Base MaxValue(this Questionnaire.ItemComponent item)
		{
			var result = item.GetExtension("http://hl7.org/fhir/StructureDefinition/maxValue");
			return result?.Value;
		}

		/// <summary>
		/// http://hl7.org/fhir/uv/sdc/STU3/StructureDefinition-sdc-questionnaire-minQuantity.html
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public static Quantity MinQuantity(this Questionnaire.ItemComponent item)
		{
			var result = item.GetExtensionValue<Quantity>("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-minQuantity");
			return result;
		}

		/// <summary>
		/// http://hl7.org/fhir/uv/sdc/STU3/StructureDefinition-sdc-questionnaire-maxQuantity.html
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public static Quantity MaxQuantity(this Questionnaire.ItemComponent item)
		{
			var result = item.GetExtensionValue<Quantity>("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-maxQuantity");
			return result;
		}

		/// <summary>
		/// http://hl7.org/fhir/StructureDefinition/maxDecimalPlaces
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public static int? MaxDecimalPlaces(this Questionnaire.ItemComponent item)
		{
			var result = item.GetIntegerExtension("http://hl7.org/fhir/StructureDefinition/maxDecimalPlaces");
			return result;
		}

		/// <summary>
		/// http://build.fhir.org/ig/HL7/sdc/StructureDefinition-sdc-questionnaire-sourceQueries.html
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static IEnumerable<ResourceReference> SourceQueries(this Questionnaire me)
		{
			var result = me.GetExtensions("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-sourceQueries");
			return result.Where(e => e.Value is ResourceReference).Select(e => 
			{
				var result = e.Value as ResourceReference;
				result.SetAnnotation(e);
				return result;
			});
		}

		/// <summary>
		/// Class used to contain the complex extension details from the Launch Context
		/// http://build.fhir.org/ig/HL7/sdc/StructureDefinition-sdc-questionnaire-launchContext.html
		/// </summary>
		public class LaunchContext
		{
			public Extension sourceExtension { get; set; }
			public string Name { get; set; }

			public Code Type { get; set; }

			public string Description { get; set; }
		}

		/// <summary>
		/// http://build.fhir.org/ig/HL7/sdc/StructureDefinition-sdc-questionnaire-launchContext.html
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static IEnumerable<LaunchContext> LaunchContexts(this Questionnaire me)
		{
			List<LaunchContext> results = new List<LaunchContext>();
			var extensions = me.GetExtensions("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-launchContext");
			foreach (var e in extensions)
			{
				var lc = new LaunchContext()
				{
					sourceExtension = e,
					Name = e.GetExtensionValue<Coding>("name")?.Code ?? e.GetExtensionValue<Id>("name")?.Value ?? e.GetStringExtension("name"),
					Type = e.GetExtensionValue<Code>("type"),
					Description = e.GetStringExtension("description")
				};
				results.Add(lc);
			}
			return results;
		}

		/// <summary>
		/// http://hl7.org/fhir/R4/extension-variable.html
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static IEnumerable<Expression> Variables(this Questionnaire me)
		{
			var result = me.GetExtensions("http://hl7.org/fhir/StructureDefinition/variable");
			return result.Where(e => e.Value is Expression).Select(e => e.Value as Expression);
		}

		/// <summary>
		/// http://hl7.org/fhir/R4/extension-variable.html
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static IEnumerable<Expression> Variables(this Questionnaire.ItemComponent me)
		{
			var result = me.GetExtensions("http://hl7.org/fhir/StructureDefinition/variable");
			return result.Where(e => e.Value is Expression).Select(e => e.Value as Expression);
		}

		/// <summary>
		/// http://build.fhir.org/ig/HL7/sdc/StructureDefinition-sdc-questionnaire-itemPopulationContext.html
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static Expression ItemPopulationContext(this Questionnaire me)
		{
			var result = me.GetExtensionValue<Expression>("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-itemPopulationContext");
			return result;
		}

		/// <summary>
		/// http://build.fhir.org/ig/HL7/sdc/StructureDefinition-sdc-questionnaire-itemPopulationContext.html
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static Expression ItemPopulationContext(this Questionnaire.ItemComponent me)
		{
			var result = me.GetExtensionValue<Expression>("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-itemPopulationContext");
			return result;
		}

		/// <summary>
		/// http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-sourceStructureMap
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static Canonical SourceStructureMap(this Questionnaire me)
		{
			var result = me.GetExtensionValue<Canonical>("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-sourceStructureMap");
			return result;
		}

		/// <summary>
		/// http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-targetStructureMap
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static IEnumerable<Canonical> TargetStructureMap(this Questionnaire me)
		{
			var result = me.GetExtensions("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-targetStructureMap");
			return result.Where(e => e.Value is Canonical).Select(e => e.Value as Canonical);
		}

		/// <summary>
		/// http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-observation-extract-category
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static IEnumerable<CodeableConcept> ObservationExtractCategory(this Questionnaire me)
		{
			var result = me.GetExtensions("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-observation-extract-category");
			return result.Where(e => e.Value is CodeableConcept).Select(e => e.Value as CodeableConcept);
		}

		/// <summary>
		/// http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-observation-extract-category
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static IEnumerable<CodeableConcept> ObservationExtractCategory(this Questionnaire.ItemComponent me)
		{
			var result = me.GetExtensions("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-observation-extract-category");
			return result.Where(e => e.Value is CodeableConcept).Select(e => e.Value as CodeableConcept);
		}

		/// <summary>
		/// http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-observationExtract
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static bool? ObservationExtract(this Questionnaire me)
		{
			var result = me.GetBoolExtension("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-observationExtract");
			return result;
		}

		/// <summary>
		/// http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-observationExtract
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static bool? ObservationExtract(this Questionnaire.ItemComponent me)
		{
			var result = me.GetBoolExtension("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-observationExtract");
			return result;
		}

		/// <summary>
		/// http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-observationExtract
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static bool? ObservationExtract(this Coding me)
		{
			var result = me.GetBoolExtension("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-observationExtract");
			return result;
		}

		/// <summary>
		/// http://hl7.org/fhir/R4/extension-questionnaire-optionexclusive.html
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static bool? OptionExclusive(this Questionnaire.AnswerOptionComponent me)
		{
			var result = me.GetBoolExtension("http://hl7.org/fhir/StructureDefinition/questionnaire-optionExclusive");
			return result;
		}

		/// <summary>
		/// http://hl7.org/fhir/uv/sdc/STU3/StructureDefinition-sdc-questionnaire-shortText.html
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static string ShortText(this Questionnaire.ItemComponent me)
		{
			var result = me.GetStringExtension("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-shortText");
			return result;
		}

		/// <summary>
		/// http://hl7.org/fhir/extension-regex.html
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static string Regex(this Questionnaire.ItemComponent me)
		{
			var result = me.GetStringExtension("http://hl7.org/fhir/StructureDefinition/regex");
			return result;
		}

		/// <summary>
		/// http://hl7.org/fhir/extension-entryformat.html
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static string EntryFormat(this Questionnaire.ItemComponent me)
		{
			var result = me.GetStringExtension("http://hl7.org/fhir/StructureDefinition/entryFormat");
			return result;
		}

		/// <summary>
		/// https://hl7.org/fhir/extensions/StructureDefinition-questionnaire-referenceResource.html
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static IEnumerable<string> ReferenceResource(this Questionnaire.ItemComponent me)
		{
			var result = me.GetExtensions("http://hl7.org/fhir/StructureDefinition/questionnaire-referenceResource");
			return result.Select(e => (e.Value as Code)?.Value).SkipWhile(s => string.IsNullOrEmpty(s));
		}

		/// <summary>
		/// Class used to contain the complex extension details from the Launch Context
		/// http://build.fhir.org/ig/HL7/sdc/StructureDefinition-sdc-questionnaire-launchContext.html
		/// </summary>
		public class QuestionnaireInvariant
		{
			public Extension sourceExtension { get; set; }
			public string Key { get; set; }

			public string Requirements { get; set; }

			public OperationOutcome.IssueSeverity? Severity { get; set; }

			public string Expression { get; set; }

			public string Human { get; set; }

			public IEnumerable<string> Location { get; set; }
		}

		/// <summary>
		/// http://hl7.org/fhir/R4/extension-questionnaire-constraint.html
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static IEnumerable<QuestionnaireInvariant> Constraints(this Questionnaire me)
		{
			return ConstraintExtensions(me);
		}

		/// <summary>
		/// http://hl7.org/fhir/R4/extension-questionnaire-constraint.html
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static IEnumerable<QuestionnaireInvariant> Constraints(this Questionnaire.ItemComponent me)
		{
			return ConstraintExtensions(me);
		}

		public static IEnumerable<QuestionnaireInvariant> ConstraintExtensions(IExtendable me)
		{
			List<QuestionnaireInvariant> results = new List<QuestionnaireInvariant>();
			var extensions = me.GetExtensions("http://hl7.org/fhir/StructureDefinition/questionnaire-constraint");
			foreach (var e in extensions)
			{
				var invariant = new QuestionnaireInvariant()
				{
					sourceExtension = e,
					Key = e.GetExtensionValue<Id>("key")?.Value ?? e.GetStringExtension("key"),
					Requirements = e.GetStringExtension("requirements"),
					Expression = e.GetStringExtension("expression"),
					Human = e.GetStringExtension("human"),
					Location = e.GetExtensions("location").Select(e => (e.Value as FhirString)?.Value).SkipWhile(s => string.IsNullOrEmpty(s)),
				};
				var severity = e.GetExtensionValue<Code>("severity")?.Value ?? e.GetStringExtension("severity");
				if (!string.IsNullOrEmpty(severity))
					invariant.Severity = Hl7.Fhir.Utility.EnumUtility.ParseLiteral<OperationOutcome.IssueSeverity>(severity);
				results.Add(invariant);
			}
			return results;
		}

		/// <summary>
		/// https://hl7.org/fhir/extensions/StructureDefinition-questionnaire-referenceProfile.html
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static IEnumerable<string> referenceProfile(this Questionnaire.ItemComponent me)
		{
			var result = me.GetExtensions("http://hl7.org/fhir/StructureDefinition/questionnaire-referenceProfile");
			return result.Select(e => (e.Value as Canonical)?.Value).SkipWhile(s => string.IsNullOrEmpty(s));
		}

		public class AllocateId
		{
			/// <summary>
			/// The extension that this data was read from
			/// </summary>
			public Extension Source { get; set; }

			public string Value { get; set; }
		}

		/// <summary>
		/// http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-allocateId
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static IEnumerable<AllocateId> allocateId(this Questionnaire me)
		{
			var result = me.GetExtensions("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-allocateId")
						.Union(me.GetExtensions("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-extractAllocateId"));
			return result.Select(e => new AllocateId() { Source = e, Value = (e.Value as FhirString)?.Value }).SkipWhile(s => string.IsNullOrEmpty(s.Value));
		}

		/// <summary>
		/// http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-allocateId
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static IEnumerable<AllocateId> allocateId(this Questionnaire.ItemComponent me)
		{
			var result = me.GetExtensions("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-allocateId")
						.Union(me.GetExtensions("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-extractAllocateId"));
			return result.Select(e => new AllocateId() { Source = e, Value = (e.Value as FhirString)?.Value }).SkipWhile(s => string.IsNullOrEmpty(s.Value));
		}

		/// <summary>
		/// http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-itemExtractionContext
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static IEnumerable<DefinitionExtractDetail> ItemExtractionContext(this Questionnaire me)
		{
			return me.ItemExtractionContextInternal();
		}

		private static IEnumerable<DefinitionExtractDetail> ItemExtractionContextInternal(this IExtendable me)
		{
			List<DefinitionExtractDetail> result = new();
			var contexts = me.GetExtensions("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-itemExtractionContext");
			foreach (var ext in contexts)
			{
				switch (ext.Value)
				{
					case FhirString fs:
						if (!string.IsNullOrEmpty(fs.Value))
							result.Add(new DefinitionExtractDetail() { Source = ext, Definition = fs.Value });
						break;
					case Canonical c:
						if (!string.IsNullOrEmpty(c.Value))
							result.Add(new DefinitionExtractDetail() { Source = ext, Definition = c.Value });
						break;
					case Code code:
						if (!string.IsNullOrEmpty(code.Value))
						{
							string profileUrl = ModelInfo.CanonicalUriForFhirCoreType(code.Value);
							if (!string.IsNullOrEmpty(profileUrl))
								result.Add(new DefinitionExtractDetail() { Source = ext, Definition = profileUrl });
						}
						break;
				}
			}
			return result;
		}

		/// <summary>
		/// http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-itemExtractionContext
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static IEnumerable<DefinitionExtractDetail> ItemExtractionContext(this Questionnaire.ItemComponent me)
		{
			return me.ItemExtractionContextInternal();
		}

		public class DefinitionExtractDetail : BundleEntryExtractDetail
		{
			// Canonical URL for the resource/profile to set this extract details on
			public string Definition { get; set; }
		}

		public class BundleEntryExtractDetail
		{
			/// <summary>
			/// The extension that this data was read from
			/// </summary>
			public Extension Source { get; set; }

			// fhirpath expression to populate bundle.entry.fullUrl value
			public string FullUrl { get; set; }

			// fhirpath expression to populate bundle.entry.request.ifNoneMatch (must return a string)
			public string IfNoneMatch { get; set; }

			// fhirpath expression to populate bundle.entry.request.ifModifiedSince (must return an instant)
			public string IfModifiedSince { get; set; }

			// fhirpath expression to populate bundle.entry.request.ifMatch (must return a string)
			public string IfMatch { get; set; }

			// fhirpath expression to populate bundle.entry.request.ifNoneExist (must return a string)
			public string IfNoneExist { get; set; }
		}

		/// <summary>
		/// http://hl7.org/fhir/R4/extension-questionnaire-constraint.html
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static IEnumerable<DefinitionExtractDetail> ItemExtractToDefinition(this Questionnaire me)
		{
			return me.ItemExtractToDefinitionInternal().Union(me.ItemExtractionContextInternal()).Where(resourceProfile => !string.IsNullOrEmpty(resourceProfile.Definition));
		}

		/// <summary>
		/// http://hl7.org/fhir/R4/extension-questionnaire-constraint.html
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static IEnumerable<DefinitionExtractDetail> ItemExtractToDefinition(this Questionnaire.ItemComponent me)
		{
			return me.ItemExtractToDefinitionInternal().Union(me.ItemExtractionContextInternal()).Where(resourceProfile => !string.IsNullOrEmpty(resourceProfile.Definition));
		}

		private static IEnumerable<DefinitionExtractDetail> ItemExtractToDefinitionInternal(this IExtendable me)
		{
			List<DefinitionExtractDetail> results = new List<DefinitionExtractDetail>();
			var extensions = me.GetExtensions("http://hl7.org/fhir/StructureDefinition/sdc-questionnaire-itemExtractRequestDetails")
				.Union(me.GetExtensions("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-definitionExtract"));
			foreach (var e in extensions)
			{
				var value = new DefinitionExtractDetail()
				{
					Source = e,
					Definition = e.GetExtensionValue<FhirUri>("definition")?.Value ?? e.GetExtensionValue<Canonical>("definition")?.Value ?? e.GetStringExtension("definition"),
					// Template = e.GetStringExtension("template") ?? e.GetExtensionValue<ResourceReference>("template")?.Reference,
					FullUrl = e.GetStringExtension("fullUrl"),
					IfNoneMatch = e.GetStringExtension("ifNoneMatch"),
					IfModifiedSince = e.GetStringExtension("ifModifiedSince"),
					IfMatch = e.GetStringExtension("ifMatch"),
					IfNoneExist = e.GetStringExtension("ifNoneExist")
				};
				results.Add(value);
			}
			return results;
		}

		/// <summary>
		/// Class used to contain the complex extension details from the Definition Extract
		/// http://build.fhir.org/ig/HL7/sdc
		/// </summary>
		public class DefinitionExtract
		{
			/// <summary>
			/// The extension that this data was read from
			/// </summary>
			public Extension Source { get; set; }

			public string Definition { get; set; }
			public DataType FixedValue { get; set; }
			public Expression Expression { get; set; }
		}

		/// <summary>
		/// http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-definitionExtractValue
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static IEnumerable<DefinitionExtract> DefinitionExtractValues(this Questionnaire me)
		{
			return me.DefinitionExtractValuesInternal();
		}

		/// <summary>
		/// http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-definitionExtractValue
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static IEnumerable<DefinitionExtract> DefinitionExtractValues(this Questionnaire.ItemComponent me)
		{
			return me.DefinitionExtractValuesInternal();
		}

		private static IEnumerable<DefinitionExtract> DefinitionExtractValuesInternal(this IExtendable me)
		{
			List<DefinitionExtract> results = new List<DefinitionExtract>();
			// also some older extensions that are still in use
			var extensions = me.GetExtensions("http://hl7.org/fhir/StructureDefinition/sdc-questionnaire-itemExtractionValue")
				.Union(me.GetExtensions("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-definitionExtractValue"))
				.Union(me.GetExtensions("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-itemExtractionValue"));
			foreach (var e in extensions)
			{
				// This implementation is tolerant of datatype for the extension value (it's not a validator at this point)
				var value = new DefinitionExtract()
				{
					Source = e,
					Definition = e.GetExtensionValue<FhirUri>("definition")?.Value ?? e.GetExtensionValue<Canonical>("definition") ?? e.GetStringExtension("definition"),
					Expression = e.GetExtensionValue<Expression>("expression"),
				};
				if (value.Expression == null)
					value.FixedValue = e.GetExtension("fixed-value")?.Value;
				results.Add(value);
			}
			return results;
		}

		#region << POC template based extraction>>
		/// <summary>
		/// POC only, will be on any item in the contained template item
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static Expression ItemTemplateExtractionContext(this Base me)
		{
			string strExtUrl = "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-templateExtractContext";

			if (me is Element e)
			{
				var result = e.GetExtensionValue<Expression>(strExtUrl);
				if (result == null)
				{
					// see if there is a raw string value for the extension
					var strExpression = e.GetStringExtension(strExtUrl);
					if (!string.IsNullOrEmpty(strExpression))
						result = new Expression() { Language = "text/fhirpath", Expression_ = strExpression };
				}
				return result;
			}
			if (me is DomainResource r)
			{
				var result = r.GetExtensionValue<Expression>(strExtUrl);
				if (result == null)
				{
					// see if there is a raw string value for the extension
					var strExpression = r.GetStringExtension(strExtUrl);
					if (!string.IsNullOrEmpty(strExpression))
						result = new Expression() { Language = "text/fhirpath", Expression_ = strExpression };
				}
				return result;
			}
			return null;
		}

		/// <summary>
		/// POC only, will be on any item in the contained template item
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static Expression ItemTemplateExtractionValue(this Base me)
		{
			if (me is Element e)
			{
				var result = e.GetExtensionValue<Expression>("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-templateExtractValue");
				if (result == null)
				{
					// see if there is a raw string value for the extension
					var strExpression = e.GetStringExtension("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-templateExtractValue");
					if (!string.IsNullOrEmpty(strExpression))
						result = new Expression() { Language = "text/fhirpath", Expression_ = strExpression };
				}
				return result;
			}
			return null;
		}


		/// <summary>
		/// http://hl7.org/fhir/R4/extension-questionnaire-constraint.html
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static IEnumerable<TemplateExtractDetail> TemplateExtract(this Questionnaire me)
		{
			return me.TemplateExtractInternal();
		}

		/// <summary>
		/// http://hl7.org/fhir/R4/extension-questionnaire-constraint.html
		/// </summary>
		/// <param name="me"></param>
		/// <returns></returns>
		public static IEnumerable<TemplateExtractDetail> TemplateExtract(this Questionnaire.ItemComponent me)
		{
			return me.TemplateExtractInternal();
		}

		public class TemplateExtractDetail : BundleEntryExtractDetail
		{
			// reference to the contained resource template
			public ResourceReference Template { get; set; }

			public string resourceId { get; set; }

		}

		private static IEnumerable<TemplateExtractDetail> TemplateExtractInternal(this IExtendable me)
		{
			List<TemplateExtractDetail> results = new List<TemplateExtractDetail>();
			var result = me.GetExtensions("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-templateExtractBundle");
			foreach (var rr in result.Select(v => v.Value).OfType<ResourceReference>())
			{
				results.Add(new TemplateExtractDetail() { Template = rr });
			}

			var extensions = me.GetExtensions("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-templateExtract");
			foreach (var e in extensions)
			{
				var value = new TemplateExtractDetail()
				{
					Template = e.GetExtensionValue<ResourceReference>("template"),
					FullUrl = e.GetStringExtension("fullUrl"),
					resourceId = e.GetStringExtension("resourceId"),
					IfNoneMatch = e.GetStringExtension("ifNoneMatch"),
					IfModifiedSince = e.GetStringExtension("ifModifiedSince"),
					IfMatch = e.GetStringExtension("ifMatch"),
					IfNoneExist = e.GetStringExtension("ifNoneExist")
				};
				results.Add(value);
			}
			return results;
		}
		#endregion
	}
}
