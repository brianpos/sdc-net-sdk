using System.Diagnostics;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.FhirPath.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hl7.Fhir.StructuredDataCapture.Test
{
	[TestClass]
    public class PrepopulateObservationTests : TestBase
    {
        [TestMethod]
        public void CreateBasicSkeleton()
        {
            var prepopEngine = new Questionnaire_PrePopulate_Observation(null, null);
            var q = GetTestQuestionnaire();
            OperationOutcome outcome = new OperationOutcome();
            Hl7.Fhir.FhirPath.ElementNavFhirExtensions.PrepareFhirSymbolTableFunctions();
            var symbolTable = new Hl7.FhirPath.Expressions.SymbolTable(Hl7.FhirPath.FhirPathCompiler.DefaultSymbolTable);
            var result = prepopEngine.CreateSkeleton(symbolTable, q, outcome);
            DebugDumpXml(result);
            DebugDumpXml(q);

            // And Validate the resulting result against the questionnaire using the Validator!
            var validator = new Hl7.Fhir.StructuredDataCapture.QuestionnaireResponseValidator();
            var outcomeValidation = validator.Validate(result, q).Result;
            DebugDumpXml(outcomeValidation);
            Assert.AreEqual(0, outcome.Issue.Count);
        }

        [TestMethod]
        public void ConvertValues()
        {
            Questionnaire.ItemComponent item = new Questionnaire.ItemComponent();
            item.LinkId = "lid";
            item.Text = "Demo Field";
            item.Type = Questionnaire.QuestionnaireItemType.String;

            OperationOutcome outcome = new OperationOutcome();
            var result = Questionnaire_PrePopulate_Observation.ConvertValue(item, new FhirString("temp"), outcome);
            DebugDumpXml(result);

            item.Type = Questionnaire.QuestionnaireItemType.Decimal;
            result = Questionnaire_PrePopulate_Observation.ConvertValue(item, new FhirString("temp"), outcome);
            DebugDumpXml(result);
            DebugDumpXml(outcome);
        }

        private Questionnaire GetTestQuestionnaire()
        {
            string testFile = @"TestData\Questionnaire.bit-of-everything.xml";
            return new Hl7.Fhir.Serialization.FhirXmlParser().Parse<Questionnaire>(System.IO.File.ReadAllText(testFile));
        }
    }
}
