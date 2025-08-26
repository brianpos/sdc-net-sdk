using Hl7.Fhir.Model;
using Hl7.Fhir.Specification;
using Hl7.Fhir.Specification.Navigation;
using Hl7.Fhir.Specification.Snapshot;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Utility;
using Hl7.FhirPath;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Introspection;
using System.Collections.Specialized;
using static Hl7.Fhir.Model.Group;
using static Hl7.Fhir.StructuredDataCapture.StructuredDataCaptureExtensions;

namespace Hl7.Fhir.StructuredDataCapture
{
	public class QuestionnaireResponse_Extract_Definition
	{
		public QuestionnaireResponse_Extract_Definition(IResourceResolver source)
		{
			Source = source;
		}
		public IResourceResolver Source { get; private set; }
		private bool LogToOutcome;

		private static ModelInspector _inspector = ModelInfo.ModelInspector;

		public async Task<IEnumerable<Bundle.EntryComponent>> Extract(Questionnaire q, QuestionnaireResponse qr, OperationOutcome outcome)
		{
			// execute the Definition based extraction
			if (q.Meta?.Tag?.Any(t => t.Code == "debug") == true)
				LogToOutcome = true;
			try
			{
				List<Bundle.EntryComponent> entries = new List<Bundle.EntryComponent>();
				VariableDictionary extractEnvironment = new VariableDictionary();
				extractEnvironment.Add("questionnaire", [q.ToTypedElement()]);
				foreach (var allocateId in q.allocateId())
				{
					var newIdValue = Guid.NewGuid().ToFhirUrnUuid();
					extractEnvironment.Add(allocateId.Value, ElementNode.CreateList(newIdValue));
					LogInfoMessage($"Questionnaire.extension[{q.Extension.IndexOf(allocateId.Source)}]", null, outcome, $"Allocating ID {allocateId.Value}: {newIdValue}");
				}
				var entryDetails = q.ItemExtractToDefinition();

				// Check at the root level if we have top level resources to create
				if (entryDetails.Any())
				{
					var evalContext = qr.ToTypedElement();
					foreach (var ed in entryDetails)
					{
						// this is a canonical URL for a profile
						var resource = ExtractResourceAtRoot(q, qr, ed, extractEnvironment, outcome);
						AddResourceToTransactionBundle(qr, outcome, entries, extractEnvironment, evalContext, ed, resource);
					}
				}

				// iterate all the items in the questionnaire
				ExtractResourcesForItems("Questionnaire", "QuestionnaireResponse", q, q, qr, q.Item, qr.Item, extractEnvironment, outcome, entries);

				return entries.AsEnumerable();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Trace.WriteLine(ex.Message);
				outcome.Issue.Add(new OperationOutcome.IssueComponent()
				{
					Code = OperationOutcome.IssueType.Exception,
					Severity = OperationOutcome.IssueSeverity.Error,
					Details = new CodeableConcept(null, null, $"Definition based extraction error: {ex.Message}")
				});
				return null;
			}
		}

		private static Bundle.EntryComponent AddResourceToTransactionBundle(List<Bundle.EntryComponent> entries, Resource resource)
		{
			Bundle.EntryComponent result = null;
			if (resource != null)
			{
				result = new Bundle.EntryComponent()
				{
					Resource = resource,
					Request = new Bundle.RequestComponent
					{
						Method = Bundle.HTTPVerb.POST,
						Url = resource.TypeName
					}
				};
				if (!string.IsNullOrEmpty(resource.Id))
				{
					result.Request.Method = Bundle.HTTPVerb.PUT;
					result.Request.Url = $"{resource.TypeName}/{resource.Id}";
				}
				entries.Add(result);
			}
			return result;
		}

		private void ExtractResourcesForItems(string processingPath, string processingQRPath, Base context, Questionnaire q, QuestionnaireResponse qr, List<Questionnaire.ItemComponent> qItems, List<QuestionnaireResponse.ItemComponent> qrItems, VariableDictionary extractEnvironment, OperationOutcome outcome, List<Bundle.EntryComponent> entries)
		{
			foreach (var itemDef in qItems)
			{
				var entryDetails = itemDef.ItemExtractToDefinition();
				var childItems = qrItems.Where(i => i.LinkId == itemDef.LinkId);
				foreach (var child in childItems)
				{
					var itemProcessingPath = $"{processingPath}.item[{qItems.IndexOf(itemDef)}]";
					var itemProcessingQrPath = $"{processingQRPath}.item[{qrItems.IndexOf(child)}]";
					var envForItem = extractEnvironment;
					var allocIdsForItem = itemDef.allocateId();
					if (allocIdsForItem.Any())
					{
						envForItem = new VariableDictionary(extractEnvironment);
						foreach (var allocateId in allocIdsForItem)
						{
							if (!envForItem.ContainsKey(allocateId.Value))
							{
								var childVars = child.Annotation<VariableDictionary>();
								if (childVars?.ContainsKey(allocateId.Value) == true)
								{
									// Use the value that was already allocated for this item
									envForItem.Add(allocateId.Value, childVars[allocateId.Value]);
								}
								else
								{
									var newIdValue = Guid.NewGuid().ToFhirUrnUuid();
									var value = ElementNode.CreateList(newIdValue);
									envForItem.Add(allocateId.Value, value);
									LogInfoMessage($"{processingPath}.item[{qItems.IndexOf(itemDef)}].extension[{itemDef.Extension.IndexOf(allocateId.Source)}]", itemProcessingQrPath, outcome, $"Allocating ID {allocateId.Value}: {newIdValue}");

									if (!child.HasAnnotation<VariableDictionary>())
									{
										childVars = new VariableDictionary();
										childVars.Add(allocateId.Value, value);
										child.SetAnnotation(childVars);
									}
								}
							}
						}
					}

					// find the corresponding items in the questionnaire response
					if (entryDetails.Any())
					{
						var evalContext = child.ToTypedElement();
						foreach (var ed in entryDetails)
						{
							// this is a canonical URL for a profile
							var resource = ExtractResourceForItem(itemProcessingPath, itemProcessingQrPath, itemDef, q, qr, ed, itemDef, child, envForItem, outcome);
							AddResourceToTransactionBundle(qr, outcome, entries, envForItem, evalContext, ed, resource);
						}
					}

					// And nest into the children
					ExtractResourcesForItems(itemProcessingPath, itemProcessingQrPath, itemDef, q, qr, itemDef.Item, child.Item, envForItem, outcome, entries);
				}
			}
		}

		private static void AddResourceToTransactionBundle(QuestionnaireResponse qr, OperationOutcome outcome, List<Bundle.EntryComponent> entries, VariableDictionary extractEnvironment, ITypedElement evalContext, DefinitionExtractDetail ed, Resource resource)
		{
			if (resource != null)
			{
				var entry = AddResourceToTransactionBundle(entries, resource);
				entry.FullUrl = EvaluateFhirPathAsString(qr, evalContext, ed.FullUrl, extractEnvironment, outcome);
				entry.Request.IfNoneMatch = EvaluateFhirPathAsString(qr, evalContext, ed.IfNoneMatch, extractEnvironment, outcome);
				entry.Request.IfMatch = EvaluateFhirPathAsString(qr, evalContext, ed.IfMatch, extractEnvironment, outcome);
				entry.Request.IfModifiedSinceElement = EvaluateFhirPathAsInstant(qr, evalContext, ed.IfModifiedSince, extractEnvironment, outcome);
				entry.Request.IfNoneExist = EvaluateFhirPathAsString(qr, evalContext, ed.IfNoneExist, extractEnvironment, outcome);
				if (string.IsNullOrEmpty(entry.FullUrl))
					entry.FullUrl = Guid.NewGuid().ToFhirUrnUuid();
			}
		}

		public static string EvaluateFhirPathAsString(QuestionnaireResponse response, ITypedElement element, string expression, VariableDictionary envForItem, OperationOutcome outcome)
		{
			if (string.IsNullOrEmpty(expression))
				return null;
			try
			{
				FhirPathCompiler compiler = new();
				var expr = compiler.Compile(expression);
				var context = new EvaluationContext().WithResourceOverrides(new ScopedNode(response.ToTypedElement()));
				context.Environment = envForItem;
				var result = expr.Scalar(element, context);
				return result?.ToString();
			}
			catch (Exception ex)
			{
				// Most likely issue here is a type mismatch, or casting/precision issue
				System.Diagnostics.Trace.WriteLine(ex.Message);

				outcome.Issue.Add(new OperationOutcome.IssueComponent()
				{
					Code = OperationOutcome.IssueType.Exception,
					Severity = OperationOutcome.IssueSeverity.Error,
					Details = new CodeableConcept(null, null, $"Unable to evaluate {expression}: {ex.Message}")
				});
			}
			return null;
		}

		public static Instant EvaluateFhirPathAsInstant(QuestionnaireResponse response, ITypedElement element, string expression, VariableDictionary envForItem, OperationOutcome outcome)
		{
			if (string.IsNullOrEmpty(expression))
				return null;
			try
			{
				FhirPathCompiler compiler = new();
				var expr = compiler.Compile(expression);
				var context = new EvaluationContext().WithResourceOverrides(new ScopedNode(response.ToTypedElement()));
				context.Environment = envForItem;
				var result = expr(element, context).ToFhirValues().FirstOrDefault();
				if (result is Instant i)
					return i;
				if (result is FhirDateTime fdt)
					return new Instant(fdt.ToDateTimeOffsetRetainTimezone());
			}
			catch (Exception ex)
			{
				// Most likely issue here is a type mismatch, or casting/precision issue
				System.Diagnostics.Trace.WriteLine(ex.Message);

				outcome.Issue.Add(new OperationOutcome.IssueComponent()
				{
					Code = OperationOutcome.IssueType.Exception,
					Severity = OperationOutcome.IssueSeverity.Error,
					Details = new CodeableConcept(null, null, $"Unable to evaluate {expression}: {ex.Message}")
				});
			}
			return null;
		}

		private Resource ExtractResourceSkeleton(string processingPath, string processingPathForResourceExtension, string processingQRPath, Questionnaire q, string resourceProfile, OperationOutcome outcome, out StructureDefinition sd, out StructureDefinitionWalker walker, out ClassMapping cm)
		{
			sd = Source.ResolveByCanonicalUri(resourceProfile) as StructureDefinition;
			if (sd == null)
			{
				outcome.Issue.Add(new OperationOutcome.IssueComponent()
				{
					Code = OperationOutcome.IssueType.NotFound,
					Severity = OperationOutcome.IssueSeverity.Error,
					Details = new CodeableConcept(null, null, $"Resource profile {resourceProfile} not found")
				});
				sd = null;
				walker = null;
				cm = null;
				return null;
			}

			if (sd.Kind != StructureDefinition.StructureDefinitionKind.Resource)
			{
				outcome.Issue.Add(new OperationOutcome.IssueComponent()
				{
					Code = OperationOutcome.IssueType.NotFound,
					Severity = OperationOutcome.IssueSeverity.Error,
					Details = new CodeableConcept(null, null, $"Resource profile {resourceProfile} is not a resource profile and cannot be used with $extract")
				});
				sd = null;
				walker = null;
				cm = null;
				return null;
			}

			if (!sd.HasSnapshot)
			{
				// Generate the snapshot!
				try
				{
					SnapshotGenerator generator = new SnapshotGenerator(Source);
					generator.Update(sd);
				}
				catch (Exception ex)
				{
					outcome.Issue.Add(new OperationOutcome.IssueComponent()
					{
						Code = OperationOutcome.IssueType.Exception,
						Severity = OperationOutcome.IssueSeverity.Fatal,
						Details = new CodeableConcept(null, null, $"Unable to generate the snapshot for {sd.Url}"),
						Diagnostics = ex.Message
					});
					sd = null;
					walker = null;
					cm = null;
					return null;
					// throw new FhirServerException(System.Net.HttpStatusCode.BadRequest, $"Error Generating snapshot or {sd.Url}: " + ex.Message);
				}
				if (!sd.HasSnapshot)
				{
					// We weren't able to generate the snapshot
					outcome.Issue.Add(new OperationOutcome.IssueComponent()
					{
						Code = OperationOutcome.IssueType.NotFound,
						Severity = OperationOutcome.IssueSeverity.Error,
						Details = new CodeableConcept(null, null, $"Unable to generate the snapshot for {sd.Url}")
					});
					sd = null;
					walker = null;
					cm = null;
					return null;
				}
			}

			walker = new StructureDefinitionWalker(sd, Source);
			cm = _inspector.FindClassMapping(sd.Type);
			Resource result = Activator.CreateInstance(cm.NativeType) as Resource;
			var extractedResource = new ExtractedValue(walker, cm, result);
			LogInfoMessage(processingPathForResourceExtension, processingQRPath, outcome, $"Extracting resource {sd.Type}: {resourceProfile}");

			// add in the meta of the profile that we created from (provided it's more than just a core resource profile)
			if (resourceProfile != ModelInfo.CanonicalUriForFhirCoreType(result.TypeName))
			{
				if (result.Meta == null) result.Meta = new();
				if (!result.Meta.Profile.Contains(resourceProfile))
					result.Meta.Profile = result.Meta.Profile.Union([resourceProfile]);
			}

			// set any fixed/pattern values at the root
			SetMandatoryFixedAndPatternProperties(processingPath, processingQRPath, extractedResource, outcome);

			return result;
		}

		private Resource ExtractResourceAtRoot(Questionnaire q, QuestionnaireResponse qr, DefinitionExtractDetail resourceProfile, VariableDictionary extractEnvironment, OperationOutcome outcome)
		{
			Resource result = ExtractResourceSkeleton("Questionnaire", $"Questionnaire.extension[{q.Extension.IndexOf(resourceProfile.Source)}]", "QuestionnaireResponse", q, resourceProfile.Definition, outcome, out var sd, out var walker, out var cm);
			if (result == null)
				return null;
			var extractedResource = new ExtractedValue(walker, cm, result);

			// Check for any definition fixed/dynamic values (at root of resource)
			foreach (var definitionValue in q.DefinitionExtractValues())
			{
				ExtractDynamicAndFixedDefinitionValues("Questionnaire", "QuestionnaireResponse", q, qr, sd, extractedResource, extractEnvironment, outcome, null, null, definitionValue);
			}

			foreach (var responseItem in qr.Item)
			{
				var itemDef = q.Item.FirstOrDefault(i => i.LinkId == responseItem.LinkId);
				ExtractProperties($"Questionnaire.item[{q.Item.IndexOf(itemDef)}]", $"QuestionnaireResponse.item[{qr.Item.IndexOf(responseItem)}]", q, qr, itemDef, responseItem, sd, walker, extractedResource, extractEnvironment, outcome);
			}

			return result;
		}

		private Resource ExtractResourceForItem(string processingPath, string processingQRPath, Questionnaire.ItemComponent itemContext, Questionnaire q, QuestionnaireResponse qr, DefinitionExtractDetail resourceProfile, Questionnaire.ItemComponent itemDef, QuestionnaireResponse.ItemComponent responseItem, VariableDictionary extractEnvironment, OperationOutcome outcome)
		{
			Resource result = ExtractResourceSkeleton(processingPath, $"{processingPath}.extension[{itemContext.Extension.IndexOf(resourceProfile.Source)}]", processingQRPath, q, resourceProfile.Definition, outcome, out var sd, out var walker, out var cm);
			if (result == null)
				return null;
			var extractedResource = new ExtractedValue(walker, cm, result);

			// Check for any definition fixed/dynamic values (at root of resource)
			//foreach (var definitionValue in itemContext.DefinitionExtracts())
			//{
			//	ExtractDynamicAndFixedDefinitionValues(processingPath, q, qr, sd, extractedResource, extractEnvironment, outcome, itemContext, null, definitionValue);
			//}

			ExtractProperties(processingPath, processingQRPath, q, qr, itemDef, responseItem, sd, walker, extractedResource, extractEnvironment, outcome);

			return result;
		}


		record ExtractedValue
		{
			public ExtractedValue(StructureDefinitionWalker walker, ExtractedValue parent, ClassMapping cm, Base value)
			{
				if (cm == null) throw new ArgumentNullException(nameof(cm));
				this.walker = walker;
				this.Parent = parent;
				this.Path = parent == null ? $"{walker.Current.PathName}" : $"{parent.Path}.{walker.Current.PathName}";
				this.cm = cm;
				this.Value = value;
			}
			public ExtractedValue(StructureDefinitionWalker walker, ClassMapping cm, Base value)
			{
				if (cm == null) throw new ArgumentNullException(nameof(cm));
				this.walker = walker;
				this.Path = walker.Current.PathName;
				this.cm = cm;
				this.Value = value;
			}
			public StructureDefinitionWalker walker;
			public ExtractedValue Parent;
			public string Path;
			public ClassMapping cm;
			public Base Value;
		}

		private void ExtractProperties(string processingPath, string processingQRPath, Questionnaire q, QuestionnaireResponse qr, Questionnaire.ItemComponent itemDef, QuestionnaireResponse.ItemComponent respItem, StructureDefinition sd, StructureDefinitionWalker walker, ExtractedValue extractedValue, VariableDictionary extractEnvironment, OperationOutcome outcome)
		{
			if (itemDef.Definition?.StartsWith(sd.Url + "#") == true)
			{
				var propertyPath = itemDef.Definition.Substring(sd.Url.Length + 1);
				if (!string.IsNullOrEmpty(walker.Current.Current.SliceName))
				{
					var prefix = $"{walker.Current.Path}:{walker.Current.Current.SliceName}";
					if (propertyPath.StartsWith(prefix))
						propertyPath = propertyPath.Substring(prefix.Length + 1);
				}
				if (propertyPath.StartsWith(walker.Current.Path) && propertyPath != walker.Current.Path)
					propertyPath = propertyPath.Substring(walker.Current.Path.Length + 1);
				if (propertyPath.StartsWith(extractedValue.Path) && propertyPath != extractedValue.Path)
					propertyPath = propertyPath.Substring(extractedValue.Path.Length + 1);

				foreach (var answer in respItem.Answer)
				{
					var extractedAnswerValue = SetValueAtPropertyPath($"{processingPath}.definition", processingQRPath, extractedValue, outcome, itemDef.LinkId, propertyPath, answer.Value, itemDef.Definition, itemDef);
					ExtractDynamicAndFixedDefinitionValues(processingPath, processingQRPath, q, qr, sd, extractedAnswerValue, extractEnvironment, outcome, itemDef, respItem);
				}

				// handle group level answers
				if (itemDef.Type == Questionnaire.QuestionnaireItemType.Group)
				{
					// SetValueAtPropertyPath(cm, result, outcome, itemDef, propertyPath, answer.Value);
					var props = propertyPath.Split('.');
					if (propertyPath == walker.Current.Path)
						props = [];

					var itemWalker = walker;
					var propCm = extractedValue.cm;
					Base val = extractedValue.Value;
					var extractedGroupItemValue = extractedValue;
					// walk down the children listed in the props to the final node
					while (props.Any())
					{
						// move to the child item
						var propName = props.First();
						var cd = ChildDefinitions(itemWalker, propName).ToArray();
						if (!cd.Any())
						{
							outcome.Issue.Add(new OperationOutcome.IssueComponent()
							{
								Code = OperationOutcome.IssueType.Exception,
								Severity = OperationOutcome.IssueSeverity.Error,
								Details = new CodeableConcept(null, null, $"Property `{propName}` does not exist on `{itemWalker.Current.Path}` while extracting for linkId: {itemDef.LinkId}  Definition: {itemDef.Definition}")
							});
							break;
						}
						itemWalker = new StructureDefinitionWalker(cd.First(), Source); // itemWalker.Child(props.First());

						// create the new property value
						var pm = extractedGroupItemValue.cm.PropertyMappings.FirstOrDefault(pm => pm.Name == itemWalker.Current.PathName);
						// Do we need to do any casting here?
						// LogInfoMessage(processingPath, processingQRPath, outcome, $"    creating {pm.PropertyTypeMapping.NativeType.Name}");
						Base propValue = Activator.CreateInstance(pm.PropertyTypeMapping.NativeType) as Base;
						extractedGroupItemValue = new ExtractedValue(itemWalker, extractedGroupItemValue, pm.PropertyTypeMapping, propValue);

						// Set the value
						LogInfoMessage($"{processingPath}.definition", processingQRPath, outcome, $"creating group {itemWalker.Current.Path}");
						SetValue(val, pm, propValue, outcome);

						ExtractDynamicAndFixedDefinitionValues(processingPath, processingQRPath, q, qr, sd, extractedGroupItemValue, extractEnvironment, outcome, itemDef, respItem);

						if (itemWalker.Current.Current.IsChoice())
						{
							var localWalker = itemWalker.Walk($"ofType({propValue.TypeName})").FirstOrDefault();
							if (localWalker == null)
							{
								// report the issue
								outcome.Issue.Add(new OperationOutcome.IssueComponent()
								{
									Code = OperationOutcome.IssueType.Exception,
									Severity = OperationOutcome.IssueSeverity.Warning,
									Details = new CodeableConcept(null, null, $"Unable to step into type `{propValue.TypeName}` for property: {itemWalker.Current.CanonicalPath()}")
								});
								break;
							}
							itemWalker = localWalker;
						}

						SetMandatoryFixedAndPatternProperties(processingPath, processingQRPath, extractedGroupItemValue, outcome);

						// move to the next item
						val = propValue;
						propCm = pm.PropertyTypeMapping;
						props = props.Skip(1).ToArray();
					}

					// And set the values into the item
					if (itemDef.Item.Any() && respItem.Item.Any())
					{
						foreach (var childItem in respItem.Item)
						{
							var childItemDef = itemDef.Item.FirstOrDefault(i => i.LinkId == childItem.LinkId);
							ExtractProperties($"{processingPath}.item[{itemDef.Item.IndexOf(childItemDef)}]", $"{processingQRPath}.item[{respItem.Item.IndexOf(childItem)}]", q, qr, childItemDef, childItem, sd, itemWalker, extractedGroupItemValue, extractEnvironment, outcome);
						}
					}
				}
			}
			else
			{
				// any IDs to allocate here?
				var envForItem = extractEnvironment;
				var allocIdsForItem = itemDef.allocateId();
				if (allocIdsForItem.Any())
				{
					envForItem = new VariableDictionary(extractEnvironment);
					foreach (var allocateId in allocIdsForItem)
					{
						if (!envForItem.ContainsKey(allocateId.Value))
						{
							var childVars = respItem.Annotation<VariableDictionary>();
							if (childVars?.ContainsKey(allocateId.Value) == true)
							{
								// Use the value that was already allocated for this item
								envForItem.Add(allocateId.Value, childVars[allocateId.Value]);
							}
							else
							{
								var newIdValue = Guid.NewGuid().ToFhirUrnUuid();
								var value = ElementNode.CreateList(newIdValue);
								envForItem.Add(allocateId.Value, value);
								LogInfoMessage($"{processingPath}.extension[{itemDef.Extension.IndexOf(allocateId.Source)}]", processingQRPath, outcome, $"Allocating ID {allocateId.Value}: {newIdValue}");

								if (!respItem.HasAnnotation<VariableDictionary>())
								{
									childVars = new VariableDictionary();
									childVars.Add(allocateId.Value, value);
									respItem.SetAnnotation(childVars);
								}
							}
						}
					}
				}

				// Check for any definition fixed/dynamic values (at root of resource)
				ExtractDynamicAndFixedDefinitionValues(processingPath, processingQRPath, q, qr, sd, extractedValue, envForItem, outcome, itemDef, respItem);

				// walk into children
				if (itemDef.Item.Any() && respItem.Item.Any())
				{
					foreach (var childItem in respItem.Item)
					{
						var childItemDef = itemDef.Item.FirstOrDefault(i => i.LinkId == childItem.LinkId);
						ExtractProperties($"{processingPath}.item[{itemDef.Item.IndexOf(childItemDef)}]", $"{processingQRPath}.item[{respItem.Item.IndexOf(childItem)}]", q, qr, childItemDef, childItem, sd, walker, extractedValue, envForItem, outcome);
					}
				}
			}
		}

		private void ExtractDynamicAndFixedDefinitionValues(string processingPath, string processingQRPath, Questionnaire q, QuestionnaireResponse qr, StructureDefinition sd, ExtractedValue extractedValue, VariableDictionary extractEnvironment, OperationOutcome outcome, Questionnaire.ItemComponent itemDef, QuestionnaireResponse.ItemComponent respItem)
		{
			// Check for any definition fixed/dynamic values
			foreach (var definitionValue in itemDef.DefinitionExtractValues())
			{
				ExtractDynamicAndFixedDefinitionValues(processingPath, processingQRPath, q, qr, sd, extractedValue, extractEnvironment, outcome, itemDef, respItem, definitionValue);
			}
		}

		private void ExtractDynamicAndFixedDefinitionValues(string processingPath, string processingQRPath, Questionnaire q, QuestionnaireResponse qr, StructureDefinition sd, ExtractedValue extractedValue, VariableDictionary extractEnvironment, OperationOutcome outcome, Questionnaire.ItemComponent itemDef, QuestionnaireResponse.ItemComponent respItem, DefinitionExtract definitionValue)
		{
			if (!definitionValue.Definition.Contains("#"))
			{
				outcome.Issue.Add(new OperationOutcome.IssueComponent()
				{
					Code = OperationOutcome.IssueType.Exception,
					Severity = OperationOutcome.IssueSeverity.Error,
					Details = new CodeableConcept(null, null, $"Invalid definitionExtractValue extension while processing linkId {itemDef?.LinkId}, definition: {definitionValue.Definition} does not contain the profile prefix"),
				});
				return;
			}
			if (!definitionValue.Definition.StartsWith(sd.Url))
			{
				// This extension is not for this profile, so skip things
				return;
			}
			var propertyPathDefinedValues = definitionValue.Definition.Substring(sd.Url.Length + 1);

			// locate the common ancestor
			var commonAncestor = extractedValue;
			while (commonAncestor.Parent != null && (!propertyPathDefinedValues.StartsWith(commonAncestor.Path) || propertyPathDefinedValues == commonAncestor.Path))
			{
				commonAncestor = commonAncestor.Parent;
			}

			var fvWalker = new StructureDefinitionWalker(commonAncestor.walker);
			if (!string.IsNullOrEmpty(fvWalker.Current.Current.SliceName))
			{
				var prefix = $"{fvWalker.Current.Path}:{fvWalker.Current.Current.SliceName}";
				if (propertyPathDefinedValues.StartsWith(prefix))
					propertyPathDefinedValues = propertyPathDefinedValues.Substring(prefix.Length + 1);
			}
			if (propertyPathDefinedValues.StartsWith(fvWalker.Current.Path) && propertyPathDefinedValues != fvWalker.Current.Path)
				propertyPathDefinedValues = propertyPathDefinedValues.Substring(fvWalker.Current.Path.Length + 1);

			if (propertyPathDefinedValues.StartsWith(commonAncestor.Path))
				propertyPathDefinedValues = propertyPathDefinedValues.Substring(commonAncestor.Path.Length + 1);

			if (definitionValue.Expression != null)
			{
				// Process the expression
				if (definitionValue.Expression.Language == "text/fhirpath")
				{
					// Validate this fhirpath expression
					FhirPathCompiler fpc = new FhirPathCompiler();
					try
					{
						var cexpr = fpc.Compile(definitionValue.Expression.Expression_);
						// set environment variables
						var env = new VariableDictionary(extractEnvironment);
						if (itemDef != null)
							env.Add("qitem", [itemDef.ToTypedElement()]);
						var qrTypedElement = qr.ToTypedElement();
						var itemContextTypedElement = respItem != null ? respItem.ToTypedElement() : qrTypedElement;
						var ctx = new EvaluationContext().WithResourceOverrides(new ScopedNode(qrTypedElement));
						ctx.Environment = env;
						var exprValues = cexpr(itemContextTypedElement, ctx).ToFhirValues();
						foreach (var value in exprValues)
						{
							if (value != null)
								SetValueAtPropertyPath($"{processingPath}.extension[{itemDef.Extension.IndexOf(definitionValue.Source)}]", processingQRPath, commonAncestor, outcome, itemDef?.LinkId, propertyPathDefinedValues, value, definitionValue.Definition, itemDef);
						}
					}
					catch (Exception ex)
					{
						// check for Unknown symbol `blah` and suggest if that's one of the 
						// other SDC defined variables that extract doesn't have access
						outcome.Issue.Add(new OperationOutcome.IssueComponent()
						{
							Code = OperationOutcome.IssueType.Exception,
							Severity = OperationOutcome.IssueSeverity.Error,
							Details = new CodeableConcept(null, null, $"Error evaluating fhirpath expression: {ex.Message}"),
							Diagnostics = definitionValue.Expression.Expression_
						});
					}
				}
				else
				{
					outcome.Issue.Add(new OperationOutcome.IssueComponent()
					{
						Code = OperationOutcome.IssueType.NotSupported,
						Severity = OperationOutcome.IssueSeverity.Warning,
						Details = new CodeableConcept(null, null, $"Only the fhirpath language is supported for extraction ({definitionValue.Expression.Language} is unsupported)")
					});
				}
			}
			else
			{
				var value = definitionValue.FixedValue;
				if (value != null)
					SetValueAtPropertyPath($"{processingPath}.extension[{itemDef.Extension.IndexOf(definitionValue.Source)}]", processingQRPath, commonAncestor, outcome, itemDef?.LinkId, propertyPathDefinedValues, value, definitionValue.Definition, itemDef);
			}
		}

		static private Dictionary<string, string> FhirToFhirPathDataTypeMappings = new Dictionary<string, string>(){
									{ "boolean", "http://hl7.org/fhirpath/System.Boolean" },
									{ "uri", "string" },
									{ "code", "string" },
									{ "oid", "string" },
									{ "id", "string" },
									{ "uuid", "string" },
									{ "markdown", "string" },
									{ "base64Binary", "string" },
									{ "unsignedInt", "integer" },
									{ "positiveInt", "integer" },
									{ "integer64", "http://hl7.org/fhirpath/System.Long" },
									{ "date", "dateTime" },
									{ "dateTime", "dateTime" },
								};
		static private StringCollection SupportedDirectPrimitiveCasting = [
									"canonical-uri",
									"uri-canonical"
								];

		private ExtractedValue SetValueAtPropertyPath(string processingPath, string processingQRPath, ExtractedValue extractedValue, OperationOutcome outcome, string linkId, string propertyPath, Base value, string definition, Questionnaire.ItemComponent itemDef)
		{
			LogInfoMessage(processingPath, processingQRPath, outcome, $"Setting ({linkId}) {extractedValue.Path}.{propertyPath} = {value.ToString()}", $"Definition: {definition} EV: {extractedValue.Path}");

			var props = propertyPath.Split('.');
			if (propertyPath == extractedValue.walker.Current.Path)
				props = [];

			var itemWalker = extractedValue.walker;
			var propCm = extractedValue.cm;
			Base val = extractedValue.Value;

			// start with the value passed in
			// ExtractedValue extractedValue = new ExtractedValue() { cm = cm, Path = propertyPath, Value = result };
			string messagePrefix = linkId != null ? $"linkId: {linkId}" : String.Empty;

			// walk down the children listed in the props to the final node
			while (props.Any())
			{
				// move to the child item
				var propName = props.First();
				props = props.Skip(1).ToArray();
				var cd = ChildDefinitions(itemWalker, propName);
				if (!cd.Any())
				{
					outcome.Issue.Add(new OperationOutcome.IssueComponent()
					{
						Code = OperationOutcome.IssueType.Exception,
						Severity = OperationOutcome.IssueSeverity.Error,
						Details = new CodeableConcept(null, null, $"Property `{propName}` does not exist on `{itemWalker.Current.Path}` while extracting {messagePrefix}  Definition: {definition}")
					});
					break;
				}
				itemWalker = new StructureDefinitionWalker(cd.First(), Source); // itemWalker.Child(props.First());

				string sliceName = null;
				if (propName.Contains(':'))
				{
					// Remove slice from name
					sliceName = propName.Substring(propName.IndexOf(':') + 1);
					propName = propName.Substring(0, propName.IndexOf(':')).Replace("[x]", "");
					if (itemWalker.Current.Current.SliceName == null && sliceName.StartsWith(propName))
					{
						var typeName = sliceName.Substring(propName.Length);
						var typeDef = itemWalker.Current.Current.Type.FirstOrDefault(t => t.Code.Equals(typeName, StringComparison.OrdinalIgnoreCase));
						itemWalker = itemWalker.Walk($"ofType({typeDef.Code})").First();
					}
				}
				else if (itemWalker.Current.Current.IsChoice() && itemWalker.Current.PathName.Replace("[x]", "") != propName.Replace("[x]", ""))
				{
					// Walk into the implicit type slice
					if (itemWalker.Current.Current.SliceName == null)
					{
						var implicitType = propName.Substring(itemWalker.Current.PathName.Length - 3);
						sliceName = propName;
						propName = itemWalker.Current.PathName.Replace("[x]", "");
						string typeName = itemWalker.Current.Current.Type.FirstOrDefault(t => String.Equals(t.Code, implicitType, StringComparison.OrdinalIgnoreCase))?.Code;
						if (typeName != null)
							itemWalker = itemWalker.Walk($"ofType({typeName})").First();
					}
				}

				// create the new property value
				var pm = propCm.PropertyMappings.FirstOrDefault(pm => pm.Name == propName || pm.Name == propName.Replace("[x]", ""));
				propCm = pm.PropertyTypeMapping;
				Base propValue;
				if (props.Length == 0)
				{
					// Do we need to do any casting here?
					propValue = GetPropertyValueCastingIfNeeded(outcome, value, definition, itemDef, val, messagePrefix, propName, pm);
				}
				else
				{
					var existingValue = pm.GetValue(val);
					if (existingValue != null && existingValue is Base) //existingValue?.GetType() == pm.PropertyTypeMapping.NativeType)
					{
						propValue = existingValue as Base;
						if (existingValue.GetType() != pm.PropertyTypeMapping.NativeType)
						{
							// This is a choice type, so we can grab the class mapping for this specific type
							propCm = _inspector.FindClassMapping(propValue.TypeName);
						}
					}
					else
					{
						if (pm.Name != propName)
						{
							// This is choice type, so locate the type from the attributes
							var typeName = propName.Substring(pm.Name.Length);

							// use the itemWalker to get the types
							var typeDef = itemWalker.Current.Current.Type.FirstOrDefault(t => t.Code.Equals(typeName, StringComparison.OrdinalIgnoreCase));
							if (itemWalker.Current.Current.IsChoice() && typeDef != null)
							{
								itemWalker = itemWalker.Walk($"ofType({typeDef.Code})").FirstOrDefault();
								propCm = _inspector.FindClassMapping(typeDef.Code);
								var propType = Hl7.Fhir.Model.ModelInfo.ModelInspector.GetTypeForFhirType(typeDef.Code);
								if (existingValue?.GetType() == propType)
									propValue = existingValue as Base;
								else
								{
									// LogInfoMessage(processingPath, processingQRPath, outcome, $"    creating {propType.Name}");
									propValue = Activator.CreateInstance(propType) as Base;
								}
							}
							else
							{
								// in the [AllowedTypes(typeof(Hl7.Fhir.Model.Quantity),typeof(Hl7.Fhir.Model.CodeableConcept),typeof(Hl7.Fhir.Model.FhirString),typeof(Hl7.Fhir.Model.FhirBoolean),typeof(Hl7.Fhir.Model.Integer),typeof(Hl7.Fhir.Model.Range),typeof(Hl7.Fhir.Model.Ratio),typeof(Hl7.Fhir.Model.SampledData),typeof(Hl7.Fhir.Model.Time),typeof(Hl7.Fhir.Model.FhirDateTime),typeof(Hl7.Fhir.Model.Period))]
								var typeProperty = pm.NativeProperty.GetCustomAttribute<Hl7.Fhir.Validation.AllowedTypesAttribute>()?.Types.FirstOrDefault(t => t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));
								if (typeProperty != null)
								{
									// LogInfoMessage(processingPath, processingQRPath, outcome, $"    creating {typeProperty.Name}");
									propValue = Activator.CreateInstance(typeProperty) as Base;
									itemWalker = itemWalker.Walk($"ofType({propValue.TypeName})").FirstOrDefault();
									propCm = _inspector.FindClassMapping(propValue.TypeName);
								}
								else
								{
									// cannot create an instance of ...
									propValue = null;
									outcome.Issue.Add(new OperationOutcome.IssueComponent()
									{
										Code = OperationOutcome.IssueType.Exception,
										Severity = OperationOutcome.IssueSeverity.Warning,
										Details = new CodeableConcept(null, null, $"Unable to create instance of {pm.Name} while extracting {messagePrefix}  Definition: {definition}")
									});
								}
							}
						}
						else
						{
							if (sliceName != null)
							{
								propCm = _inspector.FindClassMapping(itemWalker.Current.PathName) ?? pm.PropertyTypeMapping;
								if (propCm == null)
									propCm = pm.PropertyTypeMapping;
								// LogInfoMessage(processingPath, processingQRPath, outcome, $"    creating {propCm.NativeType.Name}");
								propValue = Activator.CreateInstance(propCm.NativeType) as Base;
							}
							else
							{
								// LogInfoMessage(processingPath, processingQRPath, outcome, $"    creating {pm.PropertyTypeMapping.Name}");
								propValue = Activator.CreateInstance(pm.PropertyTypeMapping.NativeType) as Base;
							}
						}
					}
				}

				extractedValue = new ExtractedValue(itemWalker, extractedValue, propCm, propValue);
				if (propValue != null)
				{
					if (itemWalker.Current.Current.IsChoice())
					{
						var localWalker = itemWalker.Walk($"ofType({propValue.TypeName})").FirstOrDefault();
						if (localWalker == null)
						{
							// report the issue
							outcome.Issue.Add(new OperationOutcome.IssueComponent()
							{
								Code = OperationOutcome.IssueType.Exception,
								Severity = OperationOutcome.IssueSeverity.Warning,
								Details = new CodeableConcept(null, null, $"Unable to step into type `{propValue.TypeName}` for property: {itemWalker.Current.CanonicalPath()}")
							});
							break;
						}
						itemWalker = localWalker;

						// re-set the walker on the extracted value (this isn't another parent level)
						extractedValue = new ExtractedValue(itemWalker, extractedValue.Parent, propCm, propValue);
					}

					SetMandatoryFixedAndPatternProperties(processingPath, processingQRPath, extractedValue, outcome);
				}

				// Set the value
				// LogInfoMessage(outcome, $"setting value {itemWalker.Current.Path}");
				SetValue(val, pm, propValue, outcome);

				// move to the next item
				val = propValue;
			}
			return extractedValue;
		}

		internal static Base GetPropertyValueCastingIfNeeded(OperationOutcome outcome, Base value, string definition, Questionnaire.ItemComponent itemDef, Base val, string messagePrefix, string propName, PropertyMapping pm)
		{
			Base propValue;
			// special case for string primitive target
			if (value != null && pm.ImplementingType == typeof(string) && value is IValue<string> str)
			{
				return value;
			}
			else if (value != null && value.GetType() != pm.ImplementingType)
			{
				if (pm.ImplementingType.IsAbstract)
				{
					// Check if the type is one of the supported types
					if (pm.FhirType.Contains(value.GetType()))
						propValue = value;
					else
					{
						// need to cast it?
						var pt = value as PrimitiveType;
						if (pt == null && pm.DeclaringClass.Name != "Extension")
						{
							outcome.Issue.Add(new OperationOutcome.IssueComponent()
							{
								Code = OperationOutcome.IssueType.Informational,
								Severity = OperationOutcome.IssueSeverity.Information,
								Details = new CodeableConcept(null, null, $"Casting required for {value.GetType().Name} to {pm.ImplementingType.Name} while extracting {messagePrefix}  Definition: {definition}")
							});
						}

						// Just use the value we have (and hope for the best)
						var existingValue = pm.GetValue(val) as Base;
						if (existingValue == null)
							propValue = value;
						else if (pt.TypeName == existingValue?.TypeName && existingValue is PrimitiveType ePT)
						{
							ePT.ObjectValue = pt.ObjectValue;
							propValue = ePT;
						}
						else
						{
							// copy the value in?
							propValue = value;
						}
					}
				}
				else
				{
					propValue = Activator.CreateInstance(pm.ImplementingType) as Base;
					if (propValue is PrimitiveType pt && value is PrimitiveType ptA)
					{
						if (pt is Instant ptI && ptA is FhirDateTime ptD)
						{
							ptI.Value = ptD.ToDateTimeOffsetRetainTimezone();
						}
						else if (FhirToFhirPathDataTypeMappings.ContainsKey(pt.TypeName) && FhirToFhirPathDataTypeMappings[pt.TypeName] == ptA.TypeName)
						{
							pt.ObjectValue = ptA.ObjectValue;
							if (pt is Date fdate && fdate.Value?.Length > 10)
							{
								// Truncate the date if it was too long (might happens when allocating from a datetime)
								fdate.Value = fdate.Value.Substring(0, 10);
							}
						}
						else if (SupportedDirectPrimitiveCasting.Contains($"{pt.TypeName}-{ptA.TypeName}"))
						{
							pt.ObjectValue = ptA.ObjectValue;
							if (pt is Date fdate && fdate.Value?.Length > 10)
							{
								// Truncate the date if it was too long (might happens when allocating from a datetime)
								fdate.Value = fdate.Value.Substring(0, 10);
							}
						}
						else if (pt.TypeName != ptA.TypeName)
						{
							outcome.Issue.Add(new OperationOutcome.IssueComponent()
							{
								Code = OperationOutcome.IssueType.Informational,
								Severity = OperationOutcome.IssueSeverity.Information,
								Details = new CodeableConcept(null, null, $"Invalid item type {value.TypeName} to populate into {propValue.TypeName} for {value.GetType().Name} to {pm.PropertyTypeMapping.NativeType.Name} while extracting {messagePrefix}  Definition: {definition}")
							});
						}
						else
							pt.ObjectValue = ptA.ObjectValue;
					}
					else if (propValue is PrimitiveType ptV && value is Coding codingA)
						ptV.ObjectValue = codingA.Code;
					else if (pm.ImplementingType == typeof(Hl7.Fhir.ElementModel.Types.String) && value is FhirString fs)
						propValue = fs;
					else if (propValue is FhirDecimal fd && value is Quantity q)
					{
						fd.Value = q.Value;
						if (itemDef?.Type == Questionnaire.QuestionnaireItemType.Quantity)
							outcome.Issue.Add(new OperationOutcome.IssueComponent()
							{
								Code = OperationOutcome.IssueType.Informational,
								Severity = OperationOutcome.IssueSeverity.Information,
								Details = new CodeableConcept(null, null, $"Used `Quantity.value` to populate a {propValue.TypeName} property `{propName}` while extracting {messagePrefix}, consider removing the `.value` so that the other quantity properties are extracted, or change the item type to `decimal`. Definition: {definition}")
							});
					}
					else
					{
						outcome.Issue.Add(new OperationOutcome.IssueComponent()
						{
							Code = OperationOutcome.IssueType.Value,
							Severity = OperationOutcome.IssueSeverity.Warning,
							Details = new CodeableConcept(null, null, $"Casting required for {value.GetType().Name} to {pm.ImplementingType.Name} while extracting {messagePrefix}  Definition: {definition}")
						});
					}
				}
			}
			else
			{
				propValue = value;
			}

			return propValue;
		}

		private void SetMandatoryFixedAndPatternProperties(string processingPath, string processingQRPath, ExtractedValue extractedValue, OperationOutcome outcome)
		{
			if (!extractedValue.walker.Current.HasChildren)
			{
				// Only Nothing to do if there are no child properties
				// return;
			}

			var localWalker = extractedValue.walker;
			if (extractedValue.walker.Current.Current.IsChoice())
			{
				localWalker = extractedValue.walker.Walk($"ofType({extractedValue.Value.TypeName})").FirstOrDefault();
				if (localWalker == null)
				{
					// report the issue
					outcome.Issue.Add(new OperationOutcome.IssueComponent()
					{
						Code = OperationOutcome.IssueType.Exception,
						Severity = OperationOutcome.IssueSeverity.Warning,
						Details = new CodeableConcept(null, null, $"Unable to step into type `{extractedValue.Value.TypeName}` for property: {extractedValue.walker.Current.CanonicalPath()}")
					});
					return;
				}
			}
			foreach (var pm in extractedValue.cm.PropertyMappings)
			{
				var cd = ChildDefinitions(localWalker, pm.Name);
				foreach (var elementOrSlice in cd)
				{
					var child = new StructureDefinitionWalker(elementOrSlice, Source);
					if (child != null) // && child.Current.Current.Min > 0)
					{
						if (child.Current.Current.Fixed != null)
						{
							// fill in this property
							LogInfoMessage(processingPath, processingQRPath, outcome, $"setting fixed value {child.Current.Path}");
							SetValue(extractedValue.Value, pm, child.Current.Current.Fixed, outcome);
						}
						if (child.Current.Current.Pattern != null)
						{
							// fill in this property
							LogInfoMessage(processingPath, processingQRPath, outcome, $"setting pattern {child.Current.Path}");
							SetValue(extractedValue.Value, pm, child.Current.Current.Pattern, outcome);
						}
					}
				}
			}
		}

		internal static void SetValue(Base context, Introspection.PropertyMapping pm, Base value, OperationOutcome outcome)
		{
			if (pm.IsCollection)
			{
				var list = pm.GetValue(context) as IList;
				list.Add(value);
			}
			else
			{
				try
				{
					if (pm.ImplementingType.Name == "Code`1" && value is PrimitiveType ptCode)
					{
						PrimitiveType newValue = Activator.CreateInstance(pm.ImplementingType) as PrimitiveType;
						newValue.ObjectValue = ptCode.ObjectValue;
						pm.SetValue(context, newValue);
					}
					else if (pm.ImplementingType == typeof(string) && value is IValue<string> pt)
					{
						pm.SetValue(context, pt.Value);
					}
					else
					{
						pm.SetValue(context, value);
					}
				}
				catch (Exception)
				{
					System.Diagnostics.Trace.WriteLine($"Error setting {pm.Name} with {value}");
					outcome.Issue.Add(new OperationOutcome.IssueComponent()
					{
						Code = OperationOutcome.IssueType.Exception,
						Severity = OperationOutcome.IssueSeverity.Error,
						Details = new CodeableConcept(null, null, $"Error setting {pm.Name} with {value}, casting may be required")
					});
				}
			}
		}

		private IEnumerable<ElementDefinitionNavigator> ChildDefinitions(StructureDefinitionWalker walker, string? childName = null)
		{
			string sliceName = null;
			if (childName.Contains(':'))
			{
				var parts = childName.Split(':');
				sliceName = parts[1];
				childName = parts[0];
			}
			var canonicals = walker.Current.Current.Type.Select(t => t.GetTypeProfile()).Distinct().ToArray();
			if (canonicals.Length > 1)
				throw new StructureDefinitionWalkerException($"Cannot determine which child to select, since there are multiple paths leading from here ('{walker.Current.CanonicalPath()}'), use 'ofType()' to disambiguate");

			// Since the element ID, resource ID and extension URL use fhirpath primitives, we should not walk into those
			// e.g. type http://hl7.org/fhir/StructureDefinition/http://hl7.org/fhirpath/System.String is returned by t.GetTypeProfile()
			if (canonicals.Length == 1 && canonicals[0].Contains("http://hl7.org/fhirpath"))
				yield break;


			// Take First(), since we have determined above that there's just one distinct result to expect.
			// (this will be the case when Type=R
			var expanded = walker.Expand().Single();
			var nav = expanded.Current.ShallowCopy();

			if (!nav.MoveToFirstChild()) yield break;

			do
			{
				if (nav.Current.IsPrimitiveValueConstraint()) continue;      // ignore value attribute
				if (childName != null && nav.Current.MatchesName(childName))
				{
					if (sliceName == null || sliceName != null && nav.Current.SliceName == sliceName)
						yield return nav.ShallowCopy();
				}
				// Also check the name as a type constraint e.g. valueQuantity
				if (nav.Current.IsChoice())
				{
					string namePart = GetNameFromPath(nav.Current.Path);
					foreach (var type in nav.Current.Type)
					{
						if (childName.Equals(namePart.Replace("[x]", type.Code), StringComparison.OrdinalIgnoreCase))
						{
							if (sliceName == null || sliceName != null && nav.Current.SliceName == sliceName)
								yield return nav.ShallowCopy();
						}
						// special case for this Questionnaire item definition based validation routine
						if (childName == namePart && sliceName.Equals(namePart.Replace("[x]", type.Code), StringComparison.OrdinalIgnoreCase))
						{
							yield return nav.ShallowCopy();
						}
					}
				}
			}
			while (nav.MoveToNext());
		}

		/// <summary>
		/// Returns the last part of the element's path.
		/// </summary>
		private static string GetNameFromPath(string path)
		{
			var pos = path.LastIndexOf(".");

			return pos != -1 ? path.Substring(pos + 1) : path;
		}

		private void LogInfoMessage(string locationInQuestionnaire, string locationInQR, OperationOutcome outcome, string message, string diagnostics = null)
		{
			System.Diagnostics.Trace.WriteLine(message);

			if (LogToOutcome)
			{
				outcome.Issue.Add(new OperationOutcome.IssueComponent()
				{
					Severity = OperationOutcome.IssueSeverity.Information,
					Code = OperationOutcome.IssueType.Informational,
					Details = new CodeableConcept() { Text = message },
					Diagnostics = diagnostics,
					Expression = locationInQR == null ? [locationInQuestionnaire] : [locationInQuestionnaire, locationInQR]
				});
			}
		}
	}
}
