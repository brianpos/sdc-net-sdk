using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using Hl7.FhirPath;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Introspection;

namespace Hl7.Fhir.StructuredDataCapture
{
	public class QuestionnaireResponse_Extract_Template
	{
		public QuestionnaireResponse_Extract_Template()
		{
		}

		private static ModelInspector _inspector = ModelInfo.ModelInspector;

		public async Task<IEnumerable<Bundle.EntryComponent>> Extract(Questionnaire q, QuestionnaireResponse qr, OperationOutcome outcome)
		{
			// execute the template based extraction
			try
			{
				List<Bundle.EntryComponent> entries = new List<Bundle.EntryComponent>();
				VariableDictionary extractEnvironment = new VariableDictionary();
				extractEnvironment.Add("questionnaire", [q.ToTypedElement()]);
				foreach (var allocateId in q.allocateId())
				{
					extractEnvironment.Add(allocateId.Value, ElementNode.CreateList(Guid.NewGuid().ToFhirUrnUuid()));
				}

				// Check at the root level if we have top level resources to create
				foreach (var extractTemplateDetails in q.TemplateExtract())
				{
					var resource = ExtractResource(qr, q, qr, extractTemplateDetails.Template, q.Item, qr.Item, extractEnvironment, outcome);
					var entry = AddResourceToTransactionBundle(entries, resource);
					if (entry != null)
					{
						var evalContext = qr.ToTypedElement();
						entry.FullUrl = QuestionnaireResponse_Extract_Definition.EvaluateFhirPathAsString(qr, evalContext, extractTemplateDetails.FullUrl, extractEnvironment, outcome);
						entry.Request.IfNoneMatch = QuestionnaireResponse_Extract_Definition.EvaluateFhirPathAsString(qr, evalContext, extractTemplateDetails.IfNoneMatch, extractEnvironment, outcome);
						entry.Request.IfMatch = QuestionnaireResponse_Extract_Definition.EvaluateFhirPathAsString(qr, evalContext, extractTemplateDetails.IfMatch, extractEnvironment, outcome);
						entry.Request.IfModifiedSinceElement = QuestionnaireResponse_Extract_Definition.EvaluateFhirPathAsInstant(qr, evalContext, extractTemplateDetails.IfModifiedSince, extractEnvironment, outcome);
						entry.Request.IfNoneExist = QuestionnaireResponse_Extract_Definition.EvaluateFhirPathAsString(qr, evalContext, extractTemplateDetails.IfNoneExist, extractEnvironment, outcome);

						var resourceId = QuestionnaireResponse_Extract_Definition.EvaluateFhirPathAsString(qr, evalContext, extractTemplateDetails.resourceId, extractEnvironment, outcome);
						if (!string.IsNullOrEmpty(resourceId))
						{
							resource.Id = resourceId;
							entry.Request.Method = Bundle.HTTPVerb.PUT;
						}

						if (string.IsNullOrEmpty(entry.FullUrl))
							entry.FullUrl = Guid.NewGuid().ToFhirUrnUuid();
					}
				}

				// iterate all the items in the questionnaire
				ExtractResourcesForItems(qr, q, qr, q.Item, qr.Item, extractEnvironment, outcome, entries);

				// If the extracted content is a single transaction bundle, then promote it to just the items inside
				if (entries.Count == 1 && entries[0].Resource is Bundle b && b.Type == Bundle.BundleType.Transaction)
					return b.Entry;

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

		private void ExtractResourcesForItems(Base qrSourceContext, Questionnaire q, QuestionnaireResponse qr, List<Questionnaire.ItemComponent> qItems, List<QuestionnaireResponse.ItemComponent> qrItems, VariableDictionary extractEnvironment, OperationOutcome outcome, List<Bundle.EntryComponent> entries)
		{
			foreach (var itemDef in qItems)
			{
				var itemExtractionContexts = itemDef.TemplateExtract();
				var childItems = qrItems.Where(i => i.LinkId == itemDef.LinkId);
				foreach (var child in childItems)
				{
					foreach (var extractTemplateRef in itemExtractionContexts)
					{
						var resource = ExtractResource(child, q, qr, extractTemplateRef.Template, itemDef.Item, child.Item, extractEnvironment, outcome);
						var entry = AddResourceToTransactionBundle(entries, resource);
						var ed = extractTemplateRef;
						if (entry != null && ed != null)
						{
							var evalContext = child.ToTypedElement();
							entry.FullUrl = QuestionnaireResponse_Extract_Definition.EvaluateFhirPathAsString(qr, evalContext, ed.FullUrl, extractEnvironment, outcome);
							entry.Request.IfNoneMatch = QuestionnaireResponse_Extract_Definition.EvaluateFhirPathAsString(qr, evalContext, ed.IfNoneMatch, extractEnvironment, outcome);
							entry.Request.IfMatch = QuestionnaireResponse_Extract_Definition.EvaluateFhirPathAsString(qr, evalContext, ed.IfMatch, extractEnvironment, outcome);
							entry.Request.IfModifiedSinceElement = QuestionnaireResponse_Extract_Definition.EvaluateFhirPathAsInstant(qr, evalContext, ed.IfModifiedSince, extractEnvironment, outcome);
							entry.Request.IfNoneExist = QuestionnaireResponse_Extract_Definition.EvaluateFhirPathAsString(qr, evalContext, ed.IfNoneExist, extractEnvironment, outcome);

							var resourceId = QuestionnaireResponse_Extract_Definition.EvaluateFhirPathAsString(qr, evalContext, ed.resourceId, extractEnvironment, outcome);
							if (!string.IsNullOrEmpty(resourceId))
							{
								resource.Id = resourceId;
								entry.Request.Method = Bundle.HTTPVerb.PUT;
							}

							if (string.IsNullOrEmpty(entry.FullUrl))
								entry.FullUrl = Guid.NewGuid().ToFhirUrnUuid();
						}
					}

					// And nest into the children
					ExtractResourcesForItems(itemDef, q, qr, itemDef.Item, child.Item, extractEnvironment, outcome, entries);
				}
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
				if (!String.IsNullOrEmpty(resource.Id))
				{
					result.Request.Method = Bundle.HTTPVerb.PUT;
					result.Request.Url = $"{resource.TypeName}/{resource.Id}";
				}
				entries.Add(result);
			}
			return result;
		}

		private Resource ExtractResource(Base qrSourceContext, Questionnaire q, QuestionnaireResponse qr, ResourceReference extractTemplateRef, List<Questionnaire.ItemComponent> itemDefChildren, IEnumerable<QuestionnaireResponse.ItemComponent> responseItems, VariableDictionary extractEnvironment, OperationOutcome outcome)
		{
			if (qrSourceContext is not QuestionnaireResponse && qrSourceContext is not QuestionnaireResponse.ItemComponent)
				throw new ArgumentException("Wrong type passed in", nameof(qrSourceContext));

			// only contained resources
			if (extractTemplateRef?.Reference?.StartsWith("#") != true)
			{
				outcome.Issue.Add(new OperationOutcome.IssueComponent()
				{
					Code = OperationOutcome.IssueType.NotSupported,
					Severity = OperationOutcome.IssueSeverity.Error,
					Details = new CodeableConcept(null, null, $"Extract resource template reference {extractTemplateRef.Reference} is not contained in this questionnaire")
				});
				return null;
			}

			// find the resource in the contained resources
			var result = q.Contained.FirstOrDefault(c => $"#{c.Id}" == extractTemplateRef.Reference);
			if (result == null)
			{
				outcome.Issue.Add(new OperationOutcome.IssueComponent()
				{
					Code = OperationOutcome.IssueType.NotFound,
					Severity = OperationOutcome.IssueSeverity.Error,
					Details = new CodeableConcept(null, null, $"Extract resource template reference {extractTemplateRef.Reference} not found in the contained resources")
				});
				return null;
			}
			result = result.DeepCopy() as Resource;
			result.Id = null; // remove the Id that is there for the template

			// Walk this entire resource to start replacing values.
			var ctx = new FhirEvaluationContext().WithResourceOverrides(new ScopedNode(qr.ToTypedElement()));
			ctx.Environment = extractEnvironment;
			ExtractProperties(qrSourceContext, result, outcome, ctx);
			return result;
		}

		private static void ExtractProperties(Base qrSourceContext, Base parent, OperationOutcome outcome, FhirEvaluationContext ctx)
		{
			var cm = _inspector.FindClassMapping(parent.GetType());
			foreach (var child in parent.NamedChildren.ToArray())
			{
				var templateContextExpression = child.Value.ItemTemplateExtractionContext();
				if (templateContextExpression != null)
				{
					var template = child.Value.DeepCopy();
					if (template is Element e)
					{
						e.RemoveExtension("http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-templateExtractContext");
					}

					// Need to remove this property from the object
					var pm = cm.PropertyMappings.FirstOrDefault(pm => pm.Name == child.ElementName);
					if (pm != null)
					{
						if (pm.IsCollection)
						{
							// remove the collection
							var list = pm.GetValue(parent) as IList;
							if (list != null)
							{
								if (list.Contains(child.Value))
									list.Remove(child.Value);
								else
									list.Clear();
							}
						}
						else
						{
							// remove the property
							pm.SetValue(parent, null);
						}

						// evaluate the expression to get the context
						var contextExpression = templateContextExpression.Expression_;
						var contextValue = qrSourceContext.Select(contextExpression, ctx);
						if (contextValue != null)
						{
							// iterate through the collection and create a new instance of this template item
							foreach (var item in contextValue)
							{
								// create a new instance of the template
								var newTemplate = template.DeepCopy() as Base;

								ExtractProperties(item, newTemplate, outcome, ctx);

								// set the context value
								if (pm.IsCollection)
								{
									// remove the collection
									var list = pm.GetValue(parent) as IList;
									if (list != null)
										list.Add(newTemplate);
								}
								else
								{
									// remove the property
									pm.SetValue(parent, newTemplate);
								}
							}
						}
					}

					// Don't walk this child for regular properties, they MUST come from the templated context shift
					continue;
				}

				// Now check for property set replacements
				var extractValueExpression = child.Value.ItemTemplateExtractionValue();
				if (extractValueExpression != null)
				{
					var pm = cm.PropertyMappings.FirstOrDefault(pm => pm.Name == child.ElementName);
					if (pm != null)
					{
						if (pm.IsCollection)
						{
							// remove the collection
							var list = pm.GetValue(parent) as IList;
							if (list != null)
							{
								if (list.Contains(child.Value))
									list.Remove(child.Value);
								else
									list.Clear();
							}
						}
						else
						{
							// remove the property
							pm.SetValue(parent, null);
						}

						// evaluate the expression to get the context
						var contextExpression = extractValueExpression.Expression_;
						var contextValue = qrSourceContext.Select(contextExpression, ctx).ToArray();
						if (contextValue != null)
						{
							if (!pm.IsCollection)
							{
								if (contextValue.Length > 1)
								{
									outcome.Issue.Add(new OperationOutcome.IssueComponent()
									{
										Code = OperationOutcome.IssueType.Informational,
										Severity = OperationOutcome.IssueSeverity.Information,
										Details = new CodeableConcept(null, null, $"Need to add/set context template {child.ElementName}")
									});
								}
								else if (contextValue.Length == 1)
								{
									try
									{
										var v = QuestionnaireResponse_Extract_Definition.GetPropertyValueCastingIfNeeded(outcome, contextValue[0], null, null, contextValue[0], String.Empty, pm.Name, pm);
										QuestionnaireResponse_Extract_Definition.SetValue(parent, pm, v, outcome);
									}
									catch (InvalidCastException ex)
									{
										outcome.Issue.Add(new OperationOutcome.IssueComponent()
										{
											Code = OperationOutcome.IssueType.Informational,
											Severity = OperationOutcome.IssueSeverity.Information,
											Details = new CodeableConcept(null, null, $"Need to cast {child.ElementName}: {ex.Message}")
										});
									}
								}
							}
							else
							{
								// iterate through the collection and create a new instance of this template item
								foreach (var item in contextValue)
								{
									var list = pm.GetValue(parent) as IList;
									if (list != null)
									{
										list.Add(item); // datatype MUST be the same - probably need to do some casting here
									}
								}
							}
						}
					}
				}

				// And replace it's children
				ExtractProperties(qrSourceContext, child.Value, outcome, ctx);
			}
		}
	}
}
