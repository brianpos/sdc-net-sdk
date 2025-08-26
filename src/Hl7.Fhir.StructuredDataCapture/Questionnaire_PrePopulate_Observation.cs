using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.StructuredDataCapture;
using Hl7.Fhir.Support;
using Hl7.Fhir.Utility;
using Hl7.FhirPath.Expressions;
using Hl7.FhirPath.Sprache;
using Microsoft.Extensions.Logging;

namespace Hl7.Fhir.StructuredDataCapture
{
	public class Questionnaire_PrePopulate_Observation
	{
		public Questionnaire_PrePopulate_Observation(Func<Canonical, StructureMap> resolveStructureMap, IResourceResolver source, Func<Canonical, System.Threading.Tasks.Task<Library>> resolveLibrary = null)
		{
			ResolveStructureMap = resolveStructureMap;
			ResolveLibrary = resolveLibrary;
			_source = source;
		}
		public Func<Canonical, StructureMap> ResolveStructureMap { get; private set; }
		public Func<Canonical, System.Threading.Tasks.Task<Library>> ResolveLibrary { get; private set; }
		IResourceResolver _source;
		List<Library> _cqfLibsInContext = new List<Library>();

		private Dictionary<string, List<Observation>> ObservationValues = new Dictionary<string, List<Observation>>();
		DateTimeOffset ProcessBasedOnDate { get; set; } = DateTimeOffset.Now;

		public static void AddVariable(Hl7.FhirPath.Expressions.SymbolTable table, string name, IEnumerable<ITypedElement> value)
		{
			table.Add(name, () => { return value; });
		}

		public async System.Threading.Tasks.Task<QuestionnaireResponse> PrePopulate(Questionnaire q, Parameters operationParameters, OperationOutcome outcome)
		{
			// scan through the operation parameters looking for Observations and index these for possible use in extractions through the code value
			Bundle bundleInputData = null;
			foreach (var item in operationParameters.Parameter.Select(p => p.Resource))
			{
				if (item is Observation obs)
				{
					CacheObservation(obs);
				}
				if (item is Bundle bundle)
				{
					bundleInputData = bundleInputData ?? bundle;

					foreach (var resourceEntry in bundle.GetResources())
					{
						if (resourceEntry is Observation obsEntry)
						{
							CacheObservation(obsEntry);
						}
						if (resourceEntry is Bundle bunEntry)
						{
							foreach (var resourceEntry2 in bunEntry.GetResources())
							{
								if (resourceEntry2 is Observation obsEntry2)
								{
									CacheObservation(obsEntry2);
								}
							}
						}
					}
				}
			}

			// retrieve all the launch context
			Hl7.Fhir.FhirPath.ElementNavFhirExtensions.PrepareFhirSymbolTableFunctions();
			var symbolTable = new Hl7.FhirPath.Expressions.SymbolTable(Hl7.FhirPath.FhirPathCompiler.DefaultSymbolTable);
			foreach (var lc in q.LaunchContexts())
			{
				var lcParameterValue = operationParameters.Get(lc.Name);
				var otherContextParams = operationParameters.Get("context");
				var list = new List<ITypedElement>();
				if (lcParameterValue.Any())
					list.AddRange(lcParameterValue.Select(p1 => p1.Resource.ToTypedElement()));
				foreach (var ctx in otherContextParams)
				{
					if (ctx.Part.Any(p => p.Name == "name" && p.Value.ToString() == lc.Name))
					{
						// This is the "conformant" portion
						var content = ctx.Part.Where(p => p.Name == "content");
						foreach (var val in content)
						{
							if (val.Resource != null)
								list.Add(val.Resource.ToTypedElement());
							else
							{
								// This service does not resolve any references
								outcome.Issue.Add(new OperationOutcome.IssueComponent
								{
									Severity = OperationOutcome.IssueSeverity.Error,
									Code = OperationOutcome.IssueType.NotSupported,
									Details = new CodeableConcept() { Text = $"Does not currently support context parameters of type {val.Value.TypeName} detected for context: {lc.Name}" }
								});
							}
						}
					}
					else
					{
						// This is the older style that I miss-interpreted the spec (or did before the operation was actually defined)
						foreach (var part in ctx.Part.Where(p => p.Name == lc.Name))
						{
							if (part.Resource != null)
								list.Add(part.Resource.ToTypedElement());
							else
							{
								// This service does not resolve any references
								outcome.Issue.Add(new OperationOutcome.IssueComponent
								{
									Severity = OperationOutcome.IssueSeverity.Error,
									Code = OperationOutcome.IssueType.NotSupported,
									Details = new CodeableConcept() { Text = $"Does not currently support context parameters of type {part.Value.TypeName} detected for context: {lc.Name}" }
								});
							}
						}
					}
				}
				// also check if there are any in the "context" parameter parts too
				AddVariable(symbolTable, lc.Name, list);
			}

			// retrieve all the environment variables for the source query data
			foreach (var resref in q.SourceQueries())
			{
				if (resref.Reference?.StartsWith("#") == true)
				{
					// this is a contained search bundle definition
					if (q.FindContainedResource(resref) is Bundle bun)
					{
						// Get the bundle with this ID from the parameters
						var np = operationParameters.Get(bun.Id);
						AddVariable(symbolTable, bun.Id, new List<ITypedElement>(np.Select(p1 => p1.Resource.ToTypedElement())));
					}
				}
				else
				{
					ResourceIdentity ri = new ResourceIdentity(resref.Reference);
					var np = operationParameters.Get(ri.Id);
					AddVariable(symbolTable, ri.Id, new List<ITypedElement>(np.Select(p1 => p1.Resource.ToTypedElement())));
				}
			}

			//// retrieve any libraries
			//var libs = q.cqfLibrary();
			//if (libs.Any())
			//{
			//	// always pre-inject the helpers
			//	// await ResolveAndIncludeLibrary("http://hl7.org/fhir/uv/cql/Library/FHIRHelpers");

			//	// and load in the ones that this needs
			//	foreach (var cqfLib in libs)
			//	{
			//		await ResolveAndIncludeLibrary(cqfLib);
			//	}
			//}

			// Check if this is a StructureMap based pre-population
			if (q.SourceStructureMap() != null)
			{
				var worker = new MappingWorker(ResolveStructureMap, _source);
				var smEngine = new Questionnaire_PrePopulate_StructureMap(worker, _source);
				QuestionnaireResponse qr = smEngine.PrePopulate(q.SourceStructureMap(), bundleInputData, outcome);
				return qr;
			}

			// And the questionnaire itself needs to be available too (as the QR should be the actual top level context)
			symbolTable.AddVar("questionnaire", q.ToTypedElement());

			// Add any actual variables to the symbol table too (pre-evaluated)
			foreach (var variableExpression in q.Variables())
			{
				if (variableExpression.Language == "application/x-fhir-query")
				{
					var opValues = GetNamedContextParameter(operationParameters, variableExpression.Name, outcome);
					if (opValues != null)
					{
						AddVariable(symbolTable, variableExpression.Name, opValues);
						continue;
					}
				}
				var values = EvaluateFhirPath(symbolTable, variableExpression, outcome, "variable");
				AddVariable(symbolTable, variableExpression.Name, values);
			}

			// create a skeleton pre-populate all the data in it
			return CreateSkeleton(symbolTable, q, outcome);
		}

		private IEnumerable<ITypedElement> GetNamedContextParameter(Parameters operationParameters, string name, OperationOutcome outcome)
		{
			var contextParams = operationParameters.Get("context");
			List<ITypedElement> list = null;
			foreach (var ctx in contextParams)
			{
				if (ctx.Part.Any(p => p.Name == "name" && p.Value.ToString() == name))
				{
					if (list == null)
						list = new List<ITypedElement>();

					// This is the "conformant" portion
					var content = ctx.Part.Where(p => p.Name == "content");
					foreach (var val in content)
					{
						if (val.Resource != null)
							list.Add(val.Resource.ToTypedElement());
						else
							list.Add(val.Value.ToTypedElement());
					}
				}
				else
				{
					if (list == null)
						list = new List<ITypedElement>();

					// This is the older style that I miss-interpreted the spec (or did before the operation was actually defined)
					foreach (var part in ctx.Part.Where(p => p.Name == name))
					{
						if (part.Resource != null)
							list.Add(part.Resource.ToTypedElement());
						else
							list.Add(part.Value.ToTypedElement());
					}
				}
			}
			return list;
		}

		private void CacheObservation(Observation obs)
		{
			foreach (var code in obs.Code?.Coding)
			{
				string codeKey = $"{code.System}|{code.Code}"; // intentionally avoiding the system's version value here
				if (ObservationValues.ContainsKey(codeKey))
					ObservationValues[codeKey].Add(obs);
				else
				{
					ObservationValues.Add(codeKey, new List<Observation>() { obs });
				}
			}
		}

		public QuestionnaireResponse CreateSkeleton(Hl7.FhirPath.Expressions.SymbolTable symbolTable, Questionnaire q, OperationOutcome outcome)
		{
			var result = new QuestionnaireResponse();
			result.Meta = new Meta() { LastUpdated = DateTime.Now };
			result.Questionnaire = q.Url;
			if (!string.IsNullOrEmpty(q.Version))
				result.Questionnaire += $"|{q.Version}";
			result.Status = QuestionnaireResponse.QuestionnaireResponseStatus.InProgress;
			CreateSkeleton(symbolTable, q.Item, result.Item, outcome);
			return result;
		}

		private void CreateSkeleton(Hl7.FhirPath.Expressions.SymbolTable symbolTable, List<Questionnaire.ItemComponent> definitionItems, List<QuestionnaireResponse.ItemComponent> responseItems, OperationOutcome outcome)
		{
			foreach (var item in definitionItems)
			{
				// evaluate any variables that were to be introduced here
				var localSymbolTable = symbolTable;
				var variableExpressions = item.Variables();
				if (variableExpressions.Any())
				{
					localSymbolTable = new SymbolTable(symbolTable);
					foreach (var variableExpression in variableExpressions)
					{
						var values = EvaluateFhirPath(localSymbolTable, variableExpression, outcome, "variable");
						AddVariable(localSymbolTable, variableExpression.Name, values);
					}
				}

				// load in the item population context
				var ipc = item.ItemPopulationContext();
				if (ipc != null)
				{
					IEnumerable<ITypedElement> itemPopContext = EvaluateFhirPath(localSymbolTable, ipc, outcome, "itemPopulationContext");
					if (itemPopContext.Any())
					{
						foreach (var value in itemPopContext)
						{
							var itemSymbolTable = new SymbolTable(localSymbolTable);
							itemSymbolTable.AddVar(ipc.Name, value);
							var qrItem = new QuestionnaireResponse.ItemComponent();
							qrItem.LinkId = item.LinkId;
							qrItem.Text = item.Text;
							responseItems.Add(qrItem);
							CreateSkeleton(itemSymbolTable, item.Item, qrItem.Item, outcome);
							SetInitialAnswerValues(itemSymbolTable, item, qrItem, outcome);
						}
					}
					else
					{
						// will just add a null value for this item context variable (so the variable exists, just with a null value)
						var itemSymbolTable = new SymbolTable(localSymbolTable);
						AddVariable(itemSymbolTable, ipc.Name, null);
						var qrItem = new QuestionnaireResponse.ItemComponent();
						qrItem.LinkId = item.LinkId;
						qrItem.Text = item.Text;
						responseItems.Add(qrItem);
						CreateSkeleton(itemSymbolTable, item.Item, qrItem.Item, outcome);
						SetInitialAnswerValues(itemSymbolTable, item, qrItem, outcome);
					}
				}
				else
				{
					// no item population
					var qrItem = new QuestionnaireResponse.ItemComponent();
					qrItem.LinkId = item.LinkId;
					qrItem.Text = item.Text;
					responseItems.Add(qrItem);
					CreateSkeleton(localSymbolTable, item.Item, qrItem.Item, outcome);
					SetInitialAnswerValues(localSymbolTable, item, qrItem, outcome);
				}
			}
		}

		private void SetInitialAnswerValues(Hl7.FhirPath.Expressions.SymbolTable symbolTable, Questionnaire.ItemComponent item, QuestionnaireResponse.ItemComponent qrItem, OperationOutcome outcome)
		{
			if (item.Type != Questionnaire.QuestionnaireItemType.Group && item.Type != Questionnaire.QuestionnaireItemType.Display)
			{
				// copy the initial value in
				foreach (var initial in item.Initial)
				{
					var answer = new QuestionnaireResponse.AnswerComponent();
					answer.Value = initial.Value;
					qrItem.Answer.Add(answer);
					PerformAnswerTypeCoersion(item, qrItem, answer, outcome);
				}

				// also check for answerOptions that are initially selected
				foreach (var option in item.AnswerOption)
				{
					if (option.InitialSelected == true)
					{
						var answer = new QuestionnaireResponse.AnswerComponent();
						answer.Value = option.Value;
						qrItem.Answer.Add(answer);
						PerformAnswerTypeCoersion(item, qrItem, answer, outcome);
					}
				}

				// also include processing for the intialExpression extension too
				if (item.InitialExpression() != null)
				{
					var expression = item.InitialExpression();
					EvaluateFhirPathInitialExpression(symbolTable, item, qrItem, expression, outcome);
				}

				// check to see if there are any observation based pre-pop rules defined
				if (item.Code.Any() && item.ObservationLinkPeriod() != null)
				{
					foreach (var observationValue in GetObservationValues(item, outcome))
					{
						var answer = new QuestionnaireResponse.AnswerComponent();
						answer.Value = observationValue;
						qrItem.Answer.Add(answer);
						PerformAnswerTypeCoersion(item, qrItem, answer, outcome);
					}
				}

				// now that we have all the defaults in there, validate that the repeats wasn't violated
				if (item.Repeats != true && qrItem.Answer.Count > 1)
				{
					outcome.Issue.Add(new OperationOutcome.IssueComponent()
					{
						Severity = OperationOutcome.IssueSeverity.Information,
						Code = OperationOutcome.IssueType.Invariant,
						Details = new CodeableConcept() { Text = $"Multiple initial answers were detected for non-repeating question {item.Text}" }
					});
					qrItem.Answer.RemoveRange(1, qrItem.Answer.Count() - 1);
				}
				// probably should report in an operation outcome that this is just weird, maybe a warning in the validation code too.
			}
		}

		private static IEnumerable<ITypedElement> EvaluateFhirPath(Hl7.FhirPath.Expressions.SymbolTable symbolTable, Hl7.Fhir.Model.Expression expression, OperationOutcome outcome, string expressionSource)
		{
			if (!string.IsNullOrEmpty(expression.Language) && expression.Language != "text/fhirpath")
			{
				outcome.Issue.Add(new OperationOutcome.IssueComponent()
				{
					Severity = OperationOutcome.IssueSeverity.Information,
					Code = OperationOutcome.IssueType.NotSupported,
					Details = new CodeableConcept() { Text = $"Questionnaire {expressionSource} {expression.Name} is defined using an unsupported language '{expression.Language}'" }
				});
				return null;
			}

			var fpEngine = new Hl7.FhirPath.FhirPathCompiler(symbolTable);
			try
			{
				// TODO: Check what the appropriate context should be while doing the pre-pop initialExpression
				//       evaluation
				var xps = fpEngine.Compile(expression.Expression_);
				var ctxt = new Hl7.Fhir.FhirPath.FhirEvaluationContext();
				var exprResults = xps(null, ctxt);

				return exprResults;
			}
			catch (Exception ex)
			{
				// TODO: error handling also
				return null;
			}
		}

		//private static readonly ElmToolkitConfig ElmToolkitConfig =
		//	Debugger.IsAttached
		//	? new ElmToolkitConfig(AssemblyCompilerDebugInformationFormat: AssemblyCompilerDebugInformationFormat.Embedded)
		//	: ElmToolkitConfig.Default;

		//private static ILoggerFactory LoggerFactory { get; } =
		//	Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole());

		//public static CqlToolkit CreateCqlToolkit(
		//	ImmutableHashSet<CqlModel>? Models = null,
		//	ImmutableHashSet<Hl7.Cql.Model.ModelInfo>? ModelInfos = null,
		//	AmbiguousTypeBehavior AmbiguousTypeBehavior = AmbiguousTypeBehavior.Error,
		//	bool EnableListPromotion = false,
		//	bool EnableListDemotion = false,
		//	bool EnableIntervalPromotion = false,
		//	bool EnableIntervalDemotion = false,
		//	bool AllowNullIntervals = false)
		//{
		//	return new CqlToolkit(LoggerFactory,
		//		new CqlToolkitConfig(
		//			Models: Models ?? [CqlModel.ElmR1, CqlModel.Fhir401],
		//			ModelInfos: ModelInfos,
		//			AmbiguousTypeBehavior: AmbiguousTypeBehavior,
		//			EnableListDemotion: EnableListDemotion,
		//			EnableListPromotion: EnableListPromotion,
		//			EnableIntervalDemotion: EnableIntervalDemotion,
		//			EnableIntervalPromotion: EnableIntervalPromotion,
		//			AllowNullIntervals: AllowNullIntervals
		//		));
		//}

		//public bool TryConvertCqlValue(
		//	object? value,
		//	CqlTypeToFhirMapping mapping,
		//	out DataType? dataType)
		//{
		//	if (value is null)
		//	{
		//		dataType = null;
		//		return false;
		//	}

		//	dataType = (mapping.FhirType, value) switch
		//	{
		//		(FHIRAllTypes.Boolean, bool valueBool) => new FhirBoolean { Value = valueBool },
		//		(FHIRAllTypes.Integer, int valueInteger) => new Integer { Value = valueInteger },
		//		(FHIRAllTypes.Decimal, decimal valueDecimal) => new FhirDecimal { Value = valueDecimal },
		//		(FHIRAllTypes.String, string valueString) => new FhirString { Value = valueString },
		//		(FHIRAllTypes.String, FhirString valueString) => new FhirString { Value = valueString.Value },
		//		(FHIRAllTypes.Date, _) => FhirTypeConverter.Default.Convert<Date>(value),
		//		(FHIRAllTypes.DateTime, _) => FhirTypeConverter.Default.Convert<FhirDateTime>(value),
		//		(FHIRAllTypes.Time, _) => FhirTypeConverter.Default.Convert<Time>(value),
		//		(FHIRAllTypes.Quantity, _) => FhirTypeConverter.Default.Convert<Quantity>(value),
		//		(FHIRAllTypes.Range, _) => FhirTypeConverter.Default.Convert<Hl7.Fhir.Model.Range>(value),
		//		(FHIRAllTypes.Ratio, _) => FhirTypeConverter.Default.Convert<Ratio>(value),
		//		(FHIRAllTypes.Period, _) => FhirTypeConverter.Default.Convert<Period>(value),
		//		(FHIRAllTypes.Identifier, Identifier valueIdentifier) => valueIdentifier,
		//		(FHIRAllTypes.Code, Code valueCode) => valueCode,
		//		(FHIRAllTypes.Code, CqlCode { system: null, version: null, display: null } cqlCode) =>
		//			new Code { Value = cqlCode.code },
		//		(FHIRAllTypes.Code, CqlCode cqlCoding) =>
		//			new Coding
		//			{
		//				Code = cqlCoding.code,
		//				System = cqlCoding.system,
		//				Version = cqlCoding.version,
		//				Display = cqlCoding.display
		//			},
		//		(FHIRAllTypes.Coding, Coding valueCoding) => valueCoding,
		//		(FHIRAllTypes.Coding, CqlCode valueCqlCode) =>
		//			new Coding
		//			{
		//				Code = valueCqlCode.code,
		//				System = valueCqlCode.system,
		//				Version = valueCqlCode.version,
		//				Display = valueCqlCode.display
		//			},
		//		(FHIRAllTypes.CodeableConcept, Coding valueCodeableConcept) => valueCodeableConcept,
		//		_ => null
		//	};
		//	return dataType is not null;
		//}


		//internal static ElmToolkit CreateElmToolkit(
		//	ImmutableHashSet<CqlModel>? models = null,
		//	ImmutableHashSet<Hl7.Cql.Model.ModelInfo>? modelInfos = null,
		//	AmbiguousTypeBehavior ambiguousTypeBehavior = AmbiguousTypeBehavior.Error,
		//	bool enableListPromotion = false) =>
		//	CreateCqlToolkit(models, modelInfos, ambiguousTypeBehavior, enableListPromotion)
		//		.CreateElmToolkit(ElmToolkitConfig);

		private void EvaluateFhirPathInitialExpression(Hl7.FhirPath.Expressions.SymbolTable symbolTable, Questionnaire.ItemComponent item, QuestionnaireResponse.ItemComponent qrItem, Hl7.Fhir.Model.Expression expression, OperationOutcome outcome)
		{
			// Inject the CQL stuff here
			if (expression.Language == "text/cql-identifier")
			{
				// var expressionNameInLibrary = expression.Expression_;
			}


			if (!string.IsNullOrEmpty(expression.Language)
				&& expression.Language != "text/fhirpath" 
				&& expression.Language != "text/cql-identifier")
			{
				outcome.Issue.Add(new OperationOutcome.IssueComponent()
				{
					Severity = OperationOutcome.IssueSeverity.Information,
					Code = OperationOutcome.IssueType.NotSupported,
					Details = new CodeableConcept() { Text = $"Expression based initial answers on question {item.Text} were defined using an unsupported language '{expression.Language}'" }
				});
				return;
			}

			var fpEngine = new Hl7.FhirPath.FhirPathCompiler(symbolTable);
			try
			{
				// TODO: Check what the appropriate context should be while doing the pre-pop initialExpression
				//       evaluation
				var xps = fpEngine.Compile(expression.Expression_);
				var ctxt = new Hl7.Fhir.FhirPath.FhirEvaluationContext();
				var exprResults = xps(qrItem.ToTypedElement(), ctxt);

				// put the results into out answer
				foreach (var ea in exprResults.ToFhirValues())
				{
					var answer = new QuestionnaireResponse.AnswerComponent();
					answer.Value = ea as DataType;
					qrItem.Answer.Add(answer);
					PerformAnswerTypeCoersion(item, qrItem, answer, outcome);
					if (item.Repeats != true) // if there are more than 1 answer for a non repeating item, just ignore it
						break;
				}
			}
			catch (Exception ex)
			{
				// TODO: error handling also
				outcome.Issue.Add(new OperationOutcome.IssueComponent()
				{
					Severity = OperationOutcome.IssueSeverity.Information,
					Code = OperationOutcome.IssueType.Exception,
					Details = new CodeableConcept() { Text = $"Expression based initial answers on question {item.Text} failed: '{ex.Message}'" },
					Diagnostics = expression.Expression_
				});
			}
		}

		/// <summary>
		/// Scan the observations provided, and check for code, type conversions, unit conversions and validity dates (in case there are some that are different)
		/// considers the unitOption values, and will convert to the first available using ucum where possible,
		/// if no unit conversion, then no answer :( But will log the information message.
		/// </summary>
		/// <param name="item"></param>
		/// <param name="outcome"></param>
		/// <returns></returns>
		public IEnumerable<DataType> GetObservationValues(Questionnaire.ItemComponent item, OperationOutcome outcome)
		{
			var results = new List<DataType>();

			// Calculate the actual date to use for the start of the valid observation range
			// Check the duration based on ProcessBasedOnDate
			var searchPeriod = item.ObservationLinkPeriod();
			if (searchPeriod.System != "http://unitsofmeasure.org")
			{
				// this isn't a supported units for the period
				outcome.Issue.Add(new OperationOutcome.IssueComponent()
				{
					Severity = OperationOutcome.IssueSeverity.Information,
					Code = OperationOutcome.IssueType.NotSupported,
					Details = new CodeableConcept() { Text = $"Unsupported duration system for Observation based pre-population on question {item.Text}" }
				});
				return results;
			}
			if (!searchPeriod.Value.HasValue)
			{
				// this isn't a supported units for the period
				outcome.Issue.Add(new OperationOutcome.IssueComponent()
				{
					Severity = OperationOutcome.IssueSeverity.Information,
					Code = OperationOutcome.IssueType.BusinessRule,
					Details = new CodeableConcept() { Text = $"No observation link period value provided for Observation based pre-population on question {item.Text}" }
				});
				return results;
			}
			// refer to http://download.hl7.de/documents/ucum/ucumdata.html as a handy list of time based ucum codes
			var interval = searchPeriod.Value;
			var searchFrom = ProcessBasedOnDate;
			switch (searchPeriod.Code)
			{
				case "a":
				case "yr": // non ucum
					searchFrom = searchFrom.AddYears(-1 * (int)searchPeriod.Value.Value);
					break;
				case "mo":
				case "Mo": // non ucum
					searchFrom = searchFrom.AddMonths(-1 * (int)searchPeriod.Value.Value);
					break;
				case "wk":
					searchFrom = searchFrom.AddDays(-7 * (int)searchPeriod.Value.Value);
					break;
				case "d":
					searchFrom = searchFrom.AddDays(-1 * (int)searchPeriod.Value.Value);
					break;
				case "min":
					searchFrom = searchFrom.AddMinutes(-1 * (int)searchPeriod.Value.Value);
					break;
				default:
					// this isn't a supported units for the period
					outcome.Issue.Add(new OperationOutcome.IssueComponent()
					{
						Severity = OperationOutcome.IssueSeverity.Information,
						Code = OperationOutcome.IssueType.NotSupported,
						Details = new CodeableConcept() { Text = $"Observation link period units '{searchPeriod.Code}' not supported on question {item.Text}" }
					});
					return results;
			}

			foreach (var code in item.Code)
			{
				string codeKey = $"{code.System}|{code.Code}"; // intentionally avoiding the system's version value here
				if (ObservationValues.ContainsKey(codeKey))
				{
					foreach (var observation in ObservationValues[codeKey])
					{
						// TODO: check that the data is within the required date range before using it too
						// ...
						// observation.Effective;

						// convert the datatype/units if required
						var answerValue = ConvertValue(item, observation.Value, outcome);
						if (answerValue != null)
							results.Add(answerValue);
					}
				}
			}
			return results;
		}

		/// <summary>
		/// Convert the value from the Observation (or other source) to the appropriate unit
		/// </summary>
		/// <param name="value"></param>
		/// <param name="itemType"></param>
		/// <param name="unit"></param>
		/// <param name="unitOptions"></param>
		/// <param name="outcome"></param>
		/// <returns></returns>
		public static DataType ConvertValue(Questionnaire.ItemComponent item, DataType value, OperationOutcome outcome)
		{
			// Oil distillery style conversion, tap off the type on the way down (and tweak the conversion if needed)
			switch (item.Type)
			{
				case Questionnaire.QuestionnaireItemType.Boolean:
					if (value is FhirBoolean) return value;
					// TODO: Map string or code values to boolean also? - probably not, better to go to the fhirpath method to convert as desired properly
					break;
				case Questionnaire.QuestionnaireItemType.Decimal:
					{
						if (value is FhirDecimal) return value;
						if (value is Quantity quantityDec)
						{
							FhirDecimal valueResult = new FhirDecimal();
							valueResult.Value = quantityDec.Value;
							// TODO: do we need to convert this item's units?
							// ConvertUnits(valueResult.Value.Value, new Fhir.Metrics.Unit()
							return valueResult;
						}
						if (value is Integer integer) return new FhirDecimal(integer.Value);
						if (value is PositiveInt pInteger) return new FhirDecimal(pInteger.Value);
						if (value is Integer uInteger) return new FhirDecimal(uInteger.Value);
					}
					break;
				case Questionnaire.QuestionnaireItemType.Integer:
					if (value is Integer) return value;
					if (value is PositiveInt pi) return new Integer(pi.Value);
					if (value is UnsignedInt ui) return new Integer(ui.Value);
					if (value is FhirDecimal decInt)
					{
						if (decInt.Value.HasValue)
							return new Integer((int)decInt.Value.Value);
						return null;
					}
					// TODO: Quantity conversion too?
					break;
				case Questionnaire.QuestionnaireItemType.Date:
					if (value is Date) return value;
					if (value is FhirDateTime fdt)
					{
						// Yes the date conversion is to just truncate at 10 chars
						// don't need to actually refine further
						if (fdt.Value?.Length > 10)
							return new Date(fdt.Value.Substring(0, 10));
						return new Date(fdt.Value);
					}
					if (value is Instant inst)
					{
						return new Date(inst.Value.ToFhirDate());
					}
					break;
				case Questionnaire.QuestionnaireItemType.DateTime:
					if (value is FhirDateTime) return value;
					if (value is Date dt) return new FhirDateTime(dt.Value);
					if (value is Instant instDt) return new FhirDateTime(instDt.Value.ToFhirDateTime());
					break;
				case Questionnaire.QuestionnaireItemType.Time:
					if (value is Time) return value;
					break;
				case Questionnaire.QuestionnaireItemType.String:
				case Questionnaire.QuestionnaireItemType.Text:
					{
						if (value is FhirString) return value;
						if (value is Id id) return new FhirString(id.Value);
						if (value is Code code) return new FhirString(code.Value);
						if (value is FhirDecimal dec) return new FhirString(dec.Value.ToString());
						if (value is Integer integer) return new FhirString(integer.Value.ToString());
						if (value is PositiveInt posInteger) return new FhirString(posInteger.Value.ToString());
						if (value is UnsignedInt uInteger) return new FhirString(uInteger.Value.ToString());
						if (value is ISystemAndCode sc) return new FhirString(sc.Code);
					}
					break;
				case Questionnaire.QuestionnaireItemType.Url:
					if (value is FhirUrl) return value;
					if (value is FhirString strUrl) return new FhirUrl(strUrl.Value);
					if (value is FhirUri uriUrl) return new FhirUrl(uriUrl.Value);
					break;
#if !FHIR_R5
				case Questionnaire.QuestionnaireItemType.Choice:
					if (value is Coding) return value;
					Coding resultC;
					if (ConvertUnstructuredToCoding(item, value, out resultC))
						return resultC;
					break;
				case Questionnaire.QuestionnaireItemType.OpenChoice:
					if (value is FhirString) return value; // this is one of the valid options of an open-choice
					if (value is Coding) return value;
					Coding resultOC;
					if (ConvertUnstructuredToCoding(item, value, out resultOC))
						return resultOC;
					break;
#endif
				case Questionnaire.QuestionnaireItemType.Attachment:
					if (value is Attachment) return value;
					break;
				case Questionnaire.QuestionnaireItemType.Reference:
					if (value is ResourceReference) return value;
					break;
				case Questionnaire.QuestionnaireItemType.Quantity:
					if (value is Quantity quantity)
					{
						// Check if the type needs to be converted (due to units restrictions)
						//  item.unit()
						//  item.unitOption()
						return quantity;
					}
					if (value is FhirDecimal qdec)
					{
						var qty = new Quantity() { Value = qdec.Value };
						var unit = item.Unit();
						if (unit != null)
						{
							qty.Code = unit.Code;
							qty.Unit = unit.Display;
							qty.System = unit.System;
						}
						return qty;
					}
					break;
				case Questionnaire.QuestionnaireItemType.Group:
					outcome.Issue.Add(new OperationOutcome.IssueComponent()
					{
						Severity = OperationOutcome.IssueSeverity.Information,
						Code = OperationOutcome.IssueType.NotSupported,
						Details = new CodeableConcept() { Text = $"cannot map a result on a group: {item.Text}" }
					});
					return null;
				case Questionnaire.QuestionnaireItemType.Display:
					outcome.Issue.Add(new OperationOutcome.IssueComponent()
					{
						Severity = OperationOutcome.IssueSeverity.Information,
						Code = OperationOutcome.IssueType.NotSupported,
						Details = new CodeableConcept() { Text = $"cannot map a result on a display item: {item.Text}" }
					});
					return null;
				default:
					break;
			}
			outcome.Issue.Add(new OperationOutcome.IssueComponent()
			{
				Severity = OperationOutcome.IssueSeverity.Information,
				Code = OperationOutcome.IssueType.NotSupported,
				Details = new CodeableConcept() { Text = $"Cannot convert value from {value.TypeName} to {item.Type} for field {item.Text}" }
			});
			return null;
		}

		private static global::Fhir.Metrics.SystemOfUnits _ucum;

		private static decimal? ConvertUnits(decimal value, global::Fhir.Metrics.Unit fromUnit, global::Fhir.Metrics.Unit toUnit)
		{
			if (_ucum == null)
			{
				var ucum = global::Fhir.Metrics.UCUM.Load();
				_ucum = ucum;
			}
			var result = _ucum.Convert(new global::Fhir.Metrics.Quantity(new global::Fhir.Metrics.Exponential(value), fromUnit), new global::Fhir.Metrics.Metric(toUnit));
			return result.Value.ToDecimal();

			// TODO: Check what the exception that could be thrown here if the
			// return null;
		}

		private static bool ConvertUnstructuredToCoding(Questionnaire.ItemComponent item, Element value, out Coding result)
		{
			// Catch things like the Patient Gender which as just a code, however the context knows what the system is
			if (value is ISystemAndCode sac)
			{
				result = new Coding(sac.System, sac.Code);
				return true;
			}
			string rawValue = null;
			if (value is FhirString str) rawValue = str.Value;
			if (value is Code code) rawValue = code.Value;
			if (item.AnswerOption.Any() && !string.IsNullOrEmpty(rawValue))
			{
				var options = item.AnswerOption.Where(ao => ao.Value is Coding).Select(ao => ao.Value as Coding).ToList();
				// Check all the code values (first)
				foreach (var coding in options)
				{
					if (string.Compare(coding.Code, rawValue, true) == 0)
					{
						result = coding;
						return true;
					}
				}
				if (value is FhirString)
				{
					// If this was just a string, then we can scan over the display values too.
					foreach (var coding in options)
					{
						if (string.Compare(coding.Display, rawValue, true) == 0)
						{
							result = coding;
							return true;
						}
					}
				}
			}
			// TODO: Lookup using the terminology service
			// ...

			// no result so just mark it as empty
			result = null;
			return false;
		}

		private static void PerformAnswerTypeCoersion(Questionnaire.ItemComponent item, QuestionnaireResponse.ItemComponent qrItem, QuestionnaireResponse.AnswerComponent answer, OperationOutcome outcome)
		{
			// TODO: Check that this answer type is actually supported for the item type defined

			if (item.Type == Questionnaire.QuestionnaireItemType.Integer)
			{
				foreach (var a in qrItem.Answer)
				{
					// switch over all the possible types of a.Value
					switch (a.Value)
					{
						case Integer:
							// The expected type
							break;

						case UnsignedInt oldValue:
							a.Value = new Integer(oldValue.Value);
							break;

						case PositiveInt oldValue:
							a.Value = new Integer(oldValue.Value);
							break;

						default:
							if (a.Value != null)
							{
								outcome.Issue.Add(new OperationOutcome.IssueComponent()
								{
									Severity = OperationOutcome.IssueSeverity.Information,
									Code = OperationOutcome.IssueType.NotSupported,
									Details = new CodeableConcept() { Text = $"Cannot cast value from {a.Value?.TypeName ?? "(null)"} to {item.Type} for field {item.Text}" }
								});
								a.Value = null;
							}
							break;
					}
				}
			}

			// Should this be doing type co-ersion as a part of its operation, rather than spreading that logic all about the place. Probably yes - and therefore change the name too.
#if !FHIR_R5
			if (item.Type == Questionnaire.QuestionnaireItemType.Choice || item.Type == Questionnaire.QuestionnaireItemType.OpenChoice)
			{
				foreach (var a in qrItem.Answer)
				{
					if (a.Value.TypeName == "code" && a.Value is ISystemAndCode code)
					{
						a.Value = new Coding(code.System, code.Code);
					}
				}
			}
#endif

			QuestionnaireResponseValidator _validator = new();
			_validator.ValidateItemTypeData(qrItem, item, qrItem.Answer.IndexOf(answer), [], QuestionnaireResponse.QuestionnaireResponseStatus.InProgress);
			outcome.Issue.AddRange(_validator.GetCurrentIssues());
		}
	}
}
