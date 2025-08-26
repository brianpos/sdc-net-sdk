|Demo SDC Operations Function App|
|---|

## Introduction ##
This project provides a quick POC on how you could create an Azure Function App and use the FirelySDK to implement FHIR Structured Data Capture (SDC) operations.
Specifically it handles the XML/JSON serializing the content in/out of the function through the content-type/accept headers and then
provides the object model to the classes themselves.

The function app implements two SDC operations:
- **$extract** - Extracts data from a QuestionnaireResponse into FHIR resources
- **$populate** - Pre-populates a Questionnaire with data

### Extract Operation Example
``` c#
[Function("extract-post")]
public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "QuestionnaireResponse/$extract")] HttpRequestData req,
    FunctionContext context,
    [InputConverter(typeof(FhirInputConverter))]
    Parameters resource)
{
    // the `resource` Parameters will be converted from the body of the POST request
    var qr = GetResource(resource, "questionnaire-response") as QuestionnaireResponse;
    var q = GetResource(resource, "questionnaire") as Questionnaire; // optional Q in the parameters

    // Perform the extraction operation
    var extractor = new Hl7.Fhir.StructuredDataCapture.QuestionnaireResponseExtract();
    var extractResults = await extractor.PerformExtractOperation(null, mr, qr, q);

    // Return the extracted FHIR resources
    var result = new FhirObjectResult(HttpStatusCode.OK, extractResults);
    return result;
}
```

### Populate Operation Example
``` c#
[Function("prepop-post")]
public static async Task<IActionResult> RunPrepop([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Questionnaire/$populate")] HttpRequestData req,
    FunctionContext context,
    [InputConverter(typeof(FhirInputConverter))]
    Parameters operationParameters)
{
    // the `operationParameters` will be converted from the body of the POST request
    var q = GetResource(operationParameters, "questionnaire") as Questionnaire;

    // Perform the pre-population operation
    Questionnaire_PrePopulate_Observation engine = new Questionnaire_PrePopulate_Observation(...);
    var qr = await engine.PrePopulate(q, operationParameters, outcome);

    // Return the pre-populated QuestionnaireResponse
    var result = new FhirObjectResult(HttpStatusCode.OK, qr);
    return result;
}
```

## Local Testing with FHIRPath Lab

You can test the Demo app locally using the [FHIRPath Lab](https://fhirpath-lab.com/Questionnaire/tester) tool:

1. Start the function app locally (it will run on `http://localhost:7195`)
2. In FHIRPath Lab, configure the SDC operations:
   - **Pre-population Service URL**: `http://localhost:7195/api/Questionnaire/$populate`
   - **Extract Service URL**: `http://localhost:7195/api/QuestionnaireResponse/$extract`

This allows you to test the SDC operations with real Questionnaire and QuestionnaireResponse resources through the FHIRPath Lab interface.

## Background reading
https://docs.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide?pivots=development-environment-vscode&tabs=browser
