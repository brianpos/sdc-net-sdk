// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification.Snapshot;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.StructuredDataCapture;
using Hl7.Fhir.Utility;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Hl7.DemoFhirAzureFunctionApp
{
    public class StructuredDataCaptureFunctions
    {
		private static CachedResolver Source;
		private static ModelInspector _inspectorR4B = ModelInspector.ForAssembly(typeof(Hl7.Fhir.Model.Patient).Assembly);

		public static Resource GetResource(Parameters me, string name)
        {
            var value = me.Parameter.Where(s => s.Name == name).FirstOrDefault();
            if (value == null)
                return null;
            return value.Resource;
        }

		private static IEnumerable<StructureDefinition> CustomStructureDefinitions(IResourceResolver resolver, Parameters operationParameters)
		{
			SnapshotGenerator sg = new SnapshotGenerator(resolver);
			var result = new List<StructureDefinition>();
			var models = operationParameters.Get("model");
			foreach (var model in models)
			{
				ScanResource(sg, result, model.Resource);

				// If there is no resource, but content in the string, assume it's the raw SD content
				if (model.Value is FhirString str)
				{
					var parserSettings = new ParserSettings() { PermissiveParsing = true, AllowUnrecognizedEnums = true };
					if (str.Value.StartsWith('<'))
					{
						var parser = new FhirXmlParser(parserSettings);
						var r = parser.Parse<Resource>(str.Value);
						ScanResource(sg, result, r);
					}
					else
					{
						var parser = new FhirJsonParser(parserSettings);
						var r = parser.Parse<Resource>(str.Value);
						ScanResource(sg, result, r);
					}
				}
			}
			return result;
		}

		private static void ScanResource(SnapshotGenerator sg, List<StructureDefinition> result, Resource? resource)
		{
			if (resource is StructureDefinition sd)
			{
				if (!sd.HasSnapshot)
				{
					sg.Update(sd);
				}
				if (sd.Abstract == true)
					sd.Abstract = false;
				result.Add(sd);
			}
			if (resource is Bundle b)
			{
				foreach (var bsd in b.GetResources().OfType<StructureDefinition>())
				{
					ScanResource(sg, result, bsd);
				}
			}
		}

		[Function("extract-post")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "QuestionnaireResponse/$extract")] HttpRequestData req,
            FunctionContext context,
            [InputConverter(typeof(FhirInputConverter))]
            Parameters resource)
        {
            var logger = context.GetLogger<StructuredDataCaptureFunctions>();
            FhirClient client = context.InstanceServices.GetService<FhirClient>();
            var qr = GetResource(resource, "questionnaire-response") as QuestionnaireResponse;
            var q = GetResource(resource, "questionnaire") as Questionnaire; // optional Q in the parameters

			if (Source == null)
			{
				var cacheResolver = new CachedResolver(ZipSource.CreateValidationSource());
				Source = cacheResolver;
			}

			var imr = new InMemoryResolver();
			foreach (var sd in CustomStructureDefinitions(Source, resource))
			{
				imr.Add(sd);
			}
			var mr = new MultiResolver(imr, Source);

			if (qr != null)
            {
                q = q ?? await ResolveQuestionnaire(client, qr);

                var extractor = new Hl7.Fhir.StructuredDataCapture.QuestionnaireResponseExtract();
                var extractResults = await extractor.PerformExtractOperation(null, mr, qr, q);

				var result = new FhirObjectResult(HttpStatusCode.OK, extractResults);
				result.ContentTypes.Add(new Microsoft.Net.Http.Headers.MediaTypeHeaderValue("application/fhir+json"));
				result.Formatters.Add(new JsonFhirOutputFormatter2(_inspectorR4B));
				return result;
            }

			var err = new OperationOutcome();
			err.Issue.Add(new OperationOutcome.IssueComponent()
			{
				Severity = OperationOutcome.IssueSeverity.Error,
				Code = OperationOutcome.IssueType.Value,
				Details = new CodeableConcept() { Text = "Missing questionnaire-response parameter" }
			});
			var resultErr = new FhirObjectResult(HttpStatusCode.BadRequest, err);
			resultErr.ContentTypes.Add(new Microsoft.Net.Http.Headers.MediaTypeHeaderValue("application/fhir+json"));
			resultErr.Formatters.Add(new JsonFhirOutputFormatter2(_inspectorR4B));
			return resultErr;
		}

		[Function("prepop-post")]
		public static async Task<IActionResult> RunPrepop([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Questionnaire/$populate")] HttpRequestData req,
			FunctionContext context,
			[InputConverter(typeof(FhirInputConverter))]
			Parameters operationParameters)
		{
			var logger = context.GetLogger<StructuredDataCaptureFunctions>();
			FhirClient client = context.InstanceServices.GetService<FhirClient>();
			var q = GetResource(operationParameters, "questionnaire") as Questionnaire; // optional Q in the parameters

			if (Source == null)
			{
				var cacheResolver = new CachedResolver(ZipSource.CreateValidationSource());
				Source = cacheResolver;
			}

			var imr = new InMemoryResolver();
			foreach (var sd in CustomStructureDefinitions(Source, operationParameters))
			{
				imr.Add(sd);
			}
			var mr = new MultiResolver(imr, Source);

			OperationOutcome outcome = new OperationOutcome();
			try
			{
				Questionnaire_PrePopulate_Observation engine = new Questionnaire_PrePopulate_Observation(
					(canonical) => null,
					Source,
					(canonical) => System.Threading.Tasks.Task.FromResult(imr.ResolveByCanonicalUri(canonical) as Library)
					);
				var qr = await engine.PrePopulate(q, operationParameters, outcome);
				if (outcome.Fatals > 0 || (qr == null && outcome.Errors > 0))
				{
					outcome.SetAnnotation<HttpStatusCode>(HttpStatusCode.BadRequest);
				}
				else if (outcome.Issue.Any())
				{
					// return this as a parameters object as there is outcome information to return
					var paramResult = new Parameters();
					paramResult.Add("response", qr);
					paramResult.Add("issues", outcome);

					var resultWithErr = new FhirObjectResult(HttpStatusCode.OK, paramResult);
					resultWithErr.ContentTypes.Add(new Microsoft.Net.Http.Headers.MediaTypeHeaderValue("application/fhir+json"));
					resultWithErr.Formatters.Add(new JsonFhirOutputFormatter2(_inspectorR4B));
					return resultWithErr;
				}
				// no outcome messages, so just return the QR directly
				var result = new FhirObjectResult(HttpStatusCode.OK, qr);
				result.ContentTypes.Add(new Microsoft.Net.Http.Headers.MediaTypeHeaderValue("application/fhir+json"));
				result.Formatters.Add(new JsonFhirOutputFormatter2(_inspectorR4B));
				return result;
			}
			catch (Exception ex)
			{
				outcome.Issue.Add(new OperationOutcome.IssueComponent()
				{
					Code = OperationOutcome.IssueType.Exception,
					Severity = OperationOutcome.IssueSeverity.Error,
					Details = new CodeableConcept(null, null, ex.Message)
				});
				outcome.SetAnnotation<HttpStatusCode>(HttpStatusCode.InternalServerError);
			}

			var resultErr = new FhirObjectResult(outcome.Annotation<HttpStatusCode>(), outcome);
			resultErr.ContentTypes.Add(new Microsoft.Net.Http.Headers.MediaTypeHeaderValue("application/fhir+json"));
			resultErr.Formatters.Add(new JsonFhirOutputFormatter2(_inspectorR4B));
			return resultErr;
		}

        static async Task<Questionnaire> ResolveQuestionnaire(FhirClient client, QuestionnaireResponse qr)
        {
            // contained questionnaire
            if (qr.Questionnaire.StartsWith("#"))
            {
                return qr.Contained.Find(c => "#"+c.Id == qr.Questionnaire) as Questionnaire;
            }

            // go resolve it from a server!
            CanonicalUrl canonical = new CanonicalUrl(qr.Questionnaire); // use the CanonicalUrl to parse out the version if its in there
            string query = $"url={canonical.Url.Value}";
            if (canonical.Version != null)
                query += $"&version={canonical.Version.Value}";
            var b = await client.SearchAsync<Questionnaire>(new string[]{ query });

            // According to the canonical URL resolving version rules!
            var qVers = b.Entry.Where(e => (e.Resource as Questionnaire)?.Url == canonical.Url.Value).Select(e => e.Resource).Cast<IVersionableConformanceResource>();
            // return CurrentCanonical.Current(qVers) as Questionnaire;
            // don't have the current canonical function here, so just return the highest version
            return qVers.OrderByDescending(q => q.Version).FirstOrDefault() as Questionnaire;

            // or this could also call the $current-canonical which is defined for R5
        }
    }
}
