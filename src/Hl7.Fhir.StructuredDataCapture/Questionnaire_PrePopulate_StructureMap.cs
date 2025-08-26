using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Language.Debugging;
using Hl7.Fhir.MappingLanguage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.StructuredDataCapture;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Hl7.Fhir.MappingLanguage.StructureMapUtilitiesAnalyze;
using Task = System.Threading.Tasks.Task;

namespace Hl7.Fhir.StructuredDataCapture
{
	/// <summary>
	/// http://build.fhir.org/ig/HL7/sdc/extraction.html#obs-extract
	/// </summary>
	public class Questionnaire_PrePopulate_StructureMap
    {
        public Questionnaire_PrePopulate_StructureMap(IWorkerContext worker, IResourceResolver source)
        {
            _worker = worker;
            _source = source;
        }
        IResourceResolver _source;
        IWorkerContext _worker;

        public QuestionnaireResponse PrePopulate(Canonical structureMapUrl, Bundle inputData, OperationOutcome outcome)
        {
            var result = new QuestionnaireResponse();
            IStructureDefinitionSummaryProvider provider = new StructureDefinitionSummaryProvider(_source);
            var engine = new StructureMapUtilitiesExecute(_worker, null /*debuggerConsole4to3*/, provider);
            var sm = _worker.getTransform(structureMapUrl);

            var source = inputData.ToTypedElement();
            var target = engine.GenerateEmptyTargetOutputStructure(sm);

            try
            {
                engine.transform(null, source, sm, target);
                result = target.ToPoco<QuestionnaireResponse>();
            }
            catch (System.Exception ex)
            {
                throw;
            }

            return result;
        }
    }
}
