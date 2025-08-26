using System.Linq;
using Hl7.Fhir.Model;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.StructuredDataCapture;
using Hl7.Fhir.Support;
using Hl7.Fhir.Utility;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Task = System.Threading.Tasks.Task;

namespace Hl7.Fhir.StructuredDataCapture.Test
{
	[TestClass]
    public class ExtractDefinitionTests : TestBase
    {
        [TestMethod]
        public async Task ExtractDefinitionSimple()
        {
            var q = GetTestQuestionnaire("definition-simple");
			var qr = GetTestQuestionnaireResponse("definition-simple");

            var source = new CachedResolver(ZipSource.CreateValidationSource());

			OperationOutcome outcome = new OperationOutcome();
			var definitionExtractor = new QuestionnaireResponse_Extract_Definition(source);
			var definitionResourcesExtracted = await definitionExtractor.Extract(q, qr, outcome);
            foreach (var resource in definitionResourcesExtracted)
            {
				DebugDumpXml(resource.Resource);
			}
			DebugDumpXml(outcome);

            Assert.AreEqual(1, definitionResourcesExtracted.Count());

            var resourceObs = definitionResourcesExtracted.FirstOrDefault()?.Resource;
			Assert.IsInstanceOfType<Observation>(resourceObs);
            if (resourceObs is Observation obs)
            {
                Assert.IsInstanceOfType<Quantity>(obs.Component[0].Value);
                var value = obs.Component[0].Value as Quantity;
				Assert.AreEqual(45, value.Value);
				Assert.AreEqual("mg", value.Unit);
			}

			Assert.AreEqual(0, outcome.Issue.Count);
        }

		[TestMethod]
		public async Task ExtractDefinitionBitOfEverything()
		{
			var q = GetTestQuestionnaire("definition-boe");
			var qr = GetTestQuestionnaireResponse("definition-boe");

			var source = new CachedResolver(ZipSource.CreateValidationSource());

			OperationOutcome outcome = new OperationOutcome();
			var definitionExtractor = new QuestionnaireResponse_Extract_Definition(source);
			var definitionResourcesExtracted = await definitionExtractor.Extract(q, qr, outcome);
			foreach (var resource in definitionResourcesExtracted)
			{
				DebugDumpJson(resource.Resource);
			}
			DebugDumpXml(outcome);

			Assert.AreEqual(1, definitionResourcesExtracted.Count());

			var resourcePat = definitionResourcesExtracted.FirstOrDefault()?.Resource;
			Assert.IsInstanceOfType<Patient>(resourcePat);
			if (resourcePat is Patient pat)
			{
				// string
				Assert.AreEqual("single line string", pat.Name[0].Given.First());

				// text
				Assert.AreEqual("multi-line\nstring", pat.Name[1].Family);

				// Boolean
				Assert.AreEqual(true, pat.Active);

				// Coding to Code
				Assert.AreEqual("male", pat.Gender.GetLiteral());

				// Date
				Assert.AreEqual("2024-09-11", pat.BirthDate);

				// DateTime
				Assert.AreEqual("2024-09-20T13:45:00+10:00", pat.Meta.LastUpdated.ToFhirDateTime());

				// Time
				Assert.AreEqual("03:45:00", (pat.Extension[0].Value as Time).Value);

				// Integer
				Assert.AreEqual(12, pat.Telecom[0].Rank);

				// Decimal
				Assert.AreEqual(3.5M, (pat.Extension[1].Value as FhirDecimal).Value);

				// URL (valueUri)
				Assert.AreEqual("http://example.com/url", (pat.Extension[2].Value as FhirUri).Value);

				// Reference
				Assert.AreEqual("Patient/example", pat.GeneralPractitioner[0].Reference);

				// Quantity
				Assert.AreEqual(12.5M, (pat.Extension[3].Value as Quantity).Value);
				Assert.AreEqual("Mg", (pat.Extension[3].Value as Quantity).Unit);

				// Coding
				Assert.AreEqual("http://example.org/coding-test", (pat.Extension[4].Value as Coding).System);
				Assert.AreEqual("a", (pat.Extension[4].Value as Coding).Code);
				Assert.AreEqual("option A", (pat.Extension[4].Value as Coding).Display);
			}

			Assert.AreEqual(0, outcome.Issue.Count);
		}

		[TestMethod]
		public async Task ExtractDefinitionSlicing()
		{
			var q = GetTestQuestionnaire("definition-slicing");
			var qr = GetTestQuestionnaireResponse("definition-slicing");
			var models = GetTestBundle("definition-slicing");

			InMemoryResolver imr = new InMemoryResolver();
			imr.Add(models);
			var source = new CachedResolver(new MultiResolver(imr, ZipSource.CreateValidationSource()));

			OperationOutcome outcome = new OperationOutcome();
			var definitionExtractor = new QuestionnaireResponse_Extract_Definition(source);
			var definitionResourcesExtracted = await definitionExtractor.Extract(q, qr, outcome);
			foreach (var resource in definitionResourcesExtracted)
			{
				DebugDumpXml(resource.Resource);
			}
			DebugDumpXml(outcome);

			Assert.AreEqual(1, definitionResourcesExtracted.Count());

			var resourcePrac = definitionResourcesExtracted.FirstOrDefault()?.Resource;
			Assert.IsInstanceOfType<Practitioner>(resourcePrac);
			if (resourcePrac is Practitioner prac)
			{
				// Check a string going into a code
				Assert.AreEqual(AdministrativeGender.Male, prac.Gender);

				// Check the extension
				Assert.AreEqual("sometimes", prac.GetStringExtension("http://healthconnex.com.au/hcxd/Practitioner/AppointmentRequired"));

				// Check the profile is listed in meta
				Assert.AreEqual("http://healthconnex.com.au/hcxd/Practitioner", prac.Meta.Profile.First());

				// Check the name (simplest case)
				Assert.AreEqual("Brian", prac.Name[0].Text);

				// Birthdate: Check date extraction
				Assert.AreEqual("2024-09-10", prac.BirthDate);

				// Active: Check boolean extraction
				Assert.AreEqual(true, prac.Active);

				// Check the telecom value from teh questionnaire
				Assert.AreEqual("https://fhirpath-lab.com", prac.Telecom[0].Value);
				// Check the telecom defaults from the profile
				Assert.AreEqual("other", prac.Telecom[0].System.GetLiteral());
				Assert.AreEqual("work", prac.Telecom[0].Use.GetLiteral());
			}

			Assert.AreEqual(0, outcome.Issue.Count);
		}

		// Test Extract Definition for Identifiers (fixed val)

		// Test Extract Definition for Extension URLs (fixed val)

		// Test Extract Definition for HumanName text (calculated field)

		[TestMethod]
		public async Task ExtractDefinitionWithCrossResourceRefs()
		{
			var q = GetTestQuestionnaire("definition-refs");
			var qr = GetTestQuestionnaireResponse("definition-refs");

			var source = new CachedResolver(ZipSource.CreateValidationSource());

			OperationOutcome outcome = new OperationOutcome();
			var definitionExtractor = new QuestionnaireResponse_Extract_Definition(source);
			var definitionResourcesExtracted = await definitionExtractor.Extract(q, qr, outcome);
			Bundle results = new Bundle();
			results.Type = Bundle.BundleType.Transaction;
			results.Entry = definitionResourcesExtracted.ToList();
			DebugDumpJson(results);

			DebugDumpJson(outcome);

			Assert.AreEqual(4, definitionResourcesExtracted.Count());

			var resourcePatient = definitionResourcesExtracted.FirstOrDefault()?.Resource;
			Assert.IsInstanceOfType<Patient>(resourcePatient);
			if (resourcePatient is Patient pat)
			{
				Assert.IsNull(pat.Id);
				// Check the name (simplest case)
				Assert.AreEqual("Brian", pat.Name[0].Given.First());
				Assert.AreEqual("Postlethwaite", pat.Name[0].Family);

				// Check a string going into a code
				Assert.AreEqual(AdministrativeGender.Male, pat.Gender);
			}

			// Check the transaction references
			Assert.IsNotNull(results.Entry[0].FullUrl);
			var fullUrlPatient = results.Entry[0].FullUrl;
			Assert.AreEqual(Bundle.HTTPVerb.POST, results.Entry[0].Request.Method);
			Assert.AreEqual("Patient", results.Entry[0].Request.Url);

			var specimen = results.Entry[1].Resource as Specimen;
			Assert.AreEqual("spec-id", specimen.Id);
			Assert.IsNotNull(specimen.Subject);
			Assert.AreEqual(fullUrlPatient, specimen.Subject.Reference);
			Assert.AreEqual(Bundle.HTTPVerb.PUT, results.Entry[1].Request.Method);
			Assert.AreEqual("Specimen/spec-id", results.Entry[1].Request.Url);

			//var procedure = results.Entry[2].Resource as Procedure;
			//Assert.IsNotNull(procedure.Subject);
			//Assert.AreEqual(fullUrlPatient, procedure.Subject.Reference);
			Assert.AreEqual(Bundle.HTTPVerb.POST, results.Entry[2].Request.Method);
			Assert.AreEqual("Procedure", results.Entry[2].Request.Url);

			var condition = results.Entry[3].Resource as Condition;
			Assert.IsNotNull(condition.Subject);
			Assert.AreEqual(fullUrlPatient, condition.Subject.Reference);
			Assert.AreEqual(Bundle.HTTPVerb.POST, results.Entry[3].Request.Method);
			Assert.AreEqual("Condition", results.Entry[3].Request.Url);

			Assert.AreEqual(0, outcome.Issue.Count);
		}

		[TestMethod]
		public async Task ExtractDefinitionWithIfMatch()
		{
			var q = GetTestQuestionnaire("definition-ifmatch");
			var qr = GetTestQuestionnaireResponse("definition-ifmatch");

			var source = new CachedResolver(ZipSource.CreateValidationSource());

			OperationOutcome outcome = new OperationOutcome();
			var definitionExtractor = new QuestionnaireResponse_Extract_Definition(source);
			var definitionResourcesExtracted = await definitionExtractor.Extract(q, qr, outcome);
			Bundle results = new Bundle();
			results.Type = Bundle.BundleType.Transaction;
			results.Entry = definitionResourcesExtracted.ToList();
			DebugDumpJson(results);

			DebugDumpJson(outcome);

			Assert.AreEqual(4, definitionResourcesExtracted.Count());

			var resourcePatient = definitionResourcesExtracted.FirstOrDefault()?.Resource;
			Assert.IsInstanceOfType<Patient>(resourcePatient);
			if (resourcePatient is Patient pat)
			{
				Assert.IsNull(pat.Id);
				// Check the name (simplest case)
				Assert.AreEqual("Brian", pat.Name[0].Given.First());
				Assert.AreEqual("Postlethwaite", pat.Name[0].Family);

				// Check a string going into a code
				Assert.AreEqual(AdministrativeGender.Male, pat.Gender);
			}

			// Check the transaction references
			Assert.IsNotNull(results.Entry[0].FullUrl);
			var fullUrlPatient = results.Entry[0].FullUrl;
			Assert.AreEqual(Bundle.HTTPVerb.POST, results.Entry[0].Request.Method);
			Assert.AreEqual("Patient", results.Entry[0].Request.Url);

			var specimen = results.Entry[1].Resource as Specimen;
			Assert.AreEqual("spec-id", specimen.Id);
			Assert.IsNotNull(specimen.Subject);
			Assert.AreEqual(fullUrlPatient, specimen.Subject.Reference);
			Assert.AreEqual(Bundle.HTTPVerb.PUT, results.Entry[1].Request.Method);
			Assert.AreEqual("Specimen/spec-id", results.Entry[1].Request.Url);

			//var procedure = results.Entry[2].Resource as Procedure;
			//Assert.IsNotNull(procedure.Subject);
			//Assert.AreEqual(fullUrlPatient, procedure.Subject.Reference);
			Assert.AreEqual(Bundle.HTTPVerb.POST, results.Entry[2].Request.Method);
			Assert.AreEqual("Procedure", results.Entry[2].Request.Url);

			var condition = results.Entry[3].Resource as Condition;
			Assert.IsNotNull(condition.Subject);
			Assert.AreEqual(fullUrlPatient, condition.Subject.Reference);
			Assert.AreEqual(Bundle.HTTPVerb.POST, results.Entry[3].Request.Method);
			Assert.AreEqual("Condition", results.Entry[3].Request.Url);

			Assert.AreEqual(0, outcome.Issue.Count);
		}

		[TestMethod]
		public async Task ExtractDefinitionUT1_RootAllocAndResource()
		{
			var q = GetTestQuestionnaire("definition-ut1");
			var qr = GetTestQuestionnaireResponse("definition-ut1");

			var source = new CachedResolver(ZipSource.CreateValidationSource());

			OperationOutcome outcome = new OperationOutcome();
			var definitionExtractor = new QuestionnaireResponse_Extract_Definition(source);
			var definitionResourcesExtracted = await definitionExtractor.Extract(q, qr, outcome);
			foreach (var resource in definitionResourcesExtracted)
			{
				DebugDumpJson(resource.Resource);
			}
			DebugDumpXml(outcome);

			Assert.AreEqual(1, definitionResourcesExtracted.Count());

			var resourcePat = definitionResourcesExtracted.FirstOrDefault()?.Resource;
			Assert.IsInstanceOfType<Patient>(resourcePat);
			if (resourcePat is Patient pat)
			{
				// Check a string going into a code
				Assert.AreEqual(AdministrativeGender.Male, pat.Gender);

				// Check the name (simplest case)
				Assert.AreEqual("Brian SDC Postlethwaite", pat.Name[0].Text);

				// Birthdate: Check date extraction
				Assert.AreEqual("2024-09-10", pat.BirthDate);

				// Check the telecom value from teh questionnaire
				Assert.AreEqual("12354", pat.Telecom[0].Value);
				// Check the telecom defaults from the profile
				Assert.AreEqual("phone", pat.Telecom[0].System.GetLiteral());
				Assert.AreEqual("mobile", pat.Telecom[0].Use.GetLiteral());
			}

			Assert.AreEqual(0, outcome.Issue.Count);
		}

		[TestMethod]
		public async Task ExtractDefinitionUT2_ChildItemAllocAndResource()
		{
			var q = GetTestQuestionnaire("definition-ut1");
			var qr = GetTestQuestionnaireResponse("definition-ut1");

			var source = new CachedResolver(ZipSource.CreateValidationSource());

			OperationOutcome outcome = new OperationOutcome();
			var definitionExtractor = new QuestionnaireResponse_Extract_Definition(source);
			var definitionResourcesExtracted = await definitionExtractor.Extract(q, qr, outcome);
			foreach (var resource in definitionResourcesExtracted)
			{
				DebugDumpJson(resource.Resource);
			}
			DebugDumpXml(outcome);

			Assert.AreEqual(1, definitionResourcesExtracted.Count());

			var resourcePat = definitionResourcesExtracted.FirstOrDefault()?.Resource;
			Assert.IsInstanceOfType<Patient>(resourcePat);
			if (resourcePat is Patient pat)
			{
				// Check a string going into a code
				Assert.AreEqual(AdministrativeGender.Male, pat.Gender);

				// Check the name (simplest case)
				Assert.AreEqual("Brian SDC Postlethwaite", pat.Name[0].Text);

				// Birthdate: Check date extraction
				Assert.AreEqual("2024-09-10", pat.BirthDate);

				// Check the telecom value from teh questionnaire
				Assert.AreEqual("12354", pat.Telecom[0].Value);
				// Check the telecom defaults from the profile
				Assert.AreEqual("phone", pat.Telecom[0].System.GetLiteral());
				Assert.AreEqual("mobile", pat.Telecom[0].Use.GetLiteral());
			}

			Assert.AreEqual(0, outcome.Issue.Count);
		}





		[TestMethod]
		public async Task ExtractTemplateWithIfMatch()
		{
			var q = GetTestQuestionnaire("template-ifmatch");
			var qr = GetTestQuestionnaireResponse("template-ifmatch");

			OperationOutcome outcome = new OperationOutcome();
			var definitionExtractor = new QuestionnaireResponse_Extract_Template();
			var definitionResourcesExtracted = await definitionExtractor.Extract(q, qr, outcome);
			Bundle results = new Bundle();
			results.Type = Bundle.BundleType.Transaction;
			results.Entry = definitionResourcesExtracted.ToList();
			DebugDumpXml(results);

			DebugDumpXml(outcome);

			var fullUrlPatient = results.Entry[0].FullUrl;
			var expectedBundle = GetTestBundle("template-ifmatch", "urn:uuid:571fa7d3a50447a99811775153c89c7a", fullUrlPatient);

			Assert.AreEqual(5, definitionResourcesExtracted.Count());

			var resourcePatient = definitionResourcesExtracted.FirstOrDefault()?.Resource;
			Assert.IsInstanceOfType<Patient>(resourcePatient);
			if (resourcePatient is Patient pat)
			{
				Assert.IsNull(pat.Id);
				// Check the name (simplest case)
				Assert.AreEqual("Brian", pat.Name[0].Given.First());

				// Check a string going into a code
				Assert.AreEqual(AdministrativeGender.Male, pat.Gender);
			}

			// Check the transaction references
			Assert.IsNotNull(results.Entry[0].FullUrl);
			Assert.AreEqual(Bundle.HTTPVerb.POST, results.Entry[0].Request.Method);
			Assert.AreEqual("Patient", results.Entry[0].Request.Url);

			foreach (var entry in expectedBundle.Entry)
			{
				var expectedResource = entry.Resource;
				var expectedFullUrl = entry.FullUrl;
				var expectedMethod = entry.Request.Method;
				var expectedUrl = entry.Request.Url;

				var index = expectedBundle.Entry.IndexOf(entry);
				var resultEntry = results.Entry[index];
				Assert.IsNotNull(resultEntry);
				// Assert.AreEqual(expectedFullUrl, resultEntry.FullUrl);
				Assert.AreEqual(expectedMethod, resultEntry.Request.Method);
				Assert.AreEqual(expectedUrl, resultEntry.Request.Url);
				Assert.IsTrue(resultEntry.Resource.IsExactly(expectedResource), $"entry[{index}].resource doesn't match");
			}

			//var specimen = results.Entry[1].Resource as Specimen;
			//Assert.AreEqual("spec-id", specimen.Id);
			//Assert.IsNotNull(specimen.Subject);
			//Assert.AreEqual(fullUrlPatient, specimen.Subject.Reference);
			//Assert.AreEqual(Bundle.HTTPVerb.PUT, results.Entry[1].Request.Method);
			//Assert.AreEqual("Specimen/spec-id", results.Entry[1].Request.Url);

			////var procedure = results.Entry[2].Resource as Procedure;
			////Assert.IsNotNull(procedure.Subject);
			////Assert.AreEqual(fullUrlPatient, procedure.Subject.Reference);
			//Assert.AreEqual(Bundle.HTTPVerb.POST, results.Entry[2].Request.Method);
			//Assert.AreEqual("Procedure", results.Entry[2].Request.Url);

			//var condition = results.Entry[3].Resource as Condition;
			//Assert.IsNotNull(condition.Subject);
			//Assert.AreEqual(fullUrlPatient, condition.Subject.Reference);
			//Assert.AreEqual(Bundle.HTTPVerb.POST, results.Entry[3].Request.Method);
			//Assert.AreEqual("Condition", results.Entry[3].Request.Url);

			Assert.AreEqual(0, outcome.Issue.Count);
		}


		private Questionnaire GetTestQuestionnaire(string name)
        {
            string testFile = $@"TestData\Questionnaire.extract-{name}.json";
            return new Hl7.Fhir.Serialization.FhirJsonParser().Parse<Questionnaire>(System.IO.File.ReadAllText(testFile));
        }

		private QuestionnaireResponse GetTestQuestionnaireResponse(string name)
		{
			string testFile = $@"TestData\QuestionnaireResponse.extract-{name}.json";
			return new Hl7.Fhir.Serialization.FhirJsonParser().Parse<QuestionnaireResponse>(System.IO.File.ReadAllText(testFile));
		}

		private Bundle GetTestBundle(string name, string replace = null, string with = null)
		{
			string testFile = $@"TestData\Bundle.extract-{name}.json";
			var content = System.IO.File.ReadAllText(testFile);
			if (!string.IsNullOrEmpty(replace))
			{
				content = content.Replace(replace, with);
			}
			return new Hl7.Fhir.Serialization.FhirJsonParser().Parse<Bundle>(content);
		}
	}
}
