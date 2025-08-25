|SDC .NET API|
|---|

## Introduction ##

This is a [HL7 FHIR Structured Data Capture (SDC)][sdc-spec] reference implementation for the Microsoft .NET (dotnet) platform.

This library provides:

* Reference implementations for FHIR Structured Data Capture capabilities across multiple FHIR versions (R4, R4B, R5)
* QuestionnaireResponse validation against Questionnaire definitions
* Extract operations to convert QuestionnaireResponse data into other FHIR resources (Observation-based, Definition-based, and StructureMap-based extraction)
* Questionnaire validation capabilities
* Support for SDC extensions and advanced questionnaire features
* Azure Function demonstration for SDC extract operations
* Comprehensive unit test suite

The library supports multiple .NET target frameworks:

* .NET 8.0
* .NET Framework 4.6.2
* .NET Standard 2.1
* .NET Standard 2.0

The library depends on several NuGet packages (notably):

* *Core* (NuGet packages starting with `Hl7.Fhir.<version>`) - contains the FhirClient, resource object models and parsers
* *Specification* (NuGet packages starting with `Hl7.Fhir.Specification.<version>`) - functionality to work with the specification metadata and validation
* *FhirPath* (NuGet package `Hl7.FhirPath`) - the FhirPath evaluator, used by the Core and Specification assemblies
* *Support* (NuGet package `Hl7.Fhir.Support`) - a library with interfaces, abstractions and utility methods that are used by the other packages

**IMPORTANT**
This library provides NuGet packages for FHIR Structured Data Capture functionality.
Before installing one of the NuGet packages (or clone the repo) it is important to understand that HL7 has published several updates of the FHIR specification,
each with breaking changes - so you need to ensure you use the version that is right for you:

* [R5][r5-spec] (published March 2023) latest release, supported by this library
* [R4B][r4b-spec] (published May 2022) interim release, supported by this library
* [R4][r4-spec] (published October 2019) widely adopted, supported by this library

## Projects in this Solution ##

### Core SDC Libraries ###
* **Hl7.Fhir.R4.StructuredDataCapture** - R4 implementation of SDC capabilities
* **Hl7.Fhir.R4B.StructuredDataCapture** - R4B implementation of SDC capabilities  
* **Hl7.Fhir.R5.StructuredDataCapture** - R5 implementation of SDC capabilities

### Testing ###
* **Test.Hl7.Fhir.StructuredDataCapture** - Comprehensive unit test suite

### Demo Applications ###
* **Hl7.DemoSDCExtractOperation** - Azure Function demonstration showing how to use the SDC extract capabilities

## Getting Started ##

To use the SDC libraries in your project, install the appropriate NuGet package for your FHIR version:

```
dotnet add package brianpos.Fhir.R4.StructuredDataCapture
```

Then you can perform operations like QuestionnaireResponse extraction:

```csharp
var extractor = new QuestionnaireResponseExtract();
var result = await extractor.PerformExtractOperation(questionnaireResponse, questionnaire);
```


## Support ##

For questions and broader discussions, we use the .NET FHIR Implementers chat on [Zulip][netapi-zulip].

## Contributing ##

We are welcoming contributors!

If you want to participate in this project, we're using [Git Flow][nvie] for our branch management, so please submit your commits using pull requests on the develop branches mentioned above!

### GIT branching strategy ###

- [NVIE](http://nvie.com/posts/a-successful-git-branching-model/)
- Or see: [Git workflow](https://www.atlassian.com/git/workflows#!workflow-gitflow)

[netapi-zulip]: https://chat.fhir.org/#narrow/stream/dotnet
[sdc-spec]: http://hl7.org/fhir/uv/sdc/
[r5-spec]: http://www.hl7.org/fhir/r5
[r4b-spec]: http://www.hl7.org/fhir/r4b
[r4-spec]: http://www.hl7.org/fhir/r4
[nvie]: http://nvie.com/posts/a-successful-git-branching-model/
