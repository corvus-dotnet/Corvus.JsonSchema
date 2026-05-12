using Corvus.JsonSchemaTestSuite.CodeGenerator;
using Microsoft.Extensions.Configuration;

IConfiguration config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build();

Console.WriteLine("=== Generating type-based test cases ===");
TestCaseGenerator.GenerateTests(testFile => Console.WriteLine($"  Type test: '{testFile}'"));

Console.WriteLine();
Console.WriteLine("=== Generating standalone evaluator test cases ===");
EvaluatorTestCaseGenerator.GenerateTests(testFile => Console.WriteLine($"  Evaluator test: '{testFile}'"));

string jsonSchemaTestSuiteBase = Path.GetFullPath(config["jsonSchemaTestSuite:baseDirectory"]
    ?? throw new InvalidOperationException("Missing jsonSchemaTestSuite:baseDirectory"));

string annotationOutputPath = Path.GetFullPath(config["annotationOutputPath"]
    ?? throw new InvalidOperationException("Missing annotationOutputPath"));

Console.WriteLine();
Console.WriteLine("=== Generating annotation test cases ===");
AnnotationTestCaseGenerator.GenerateTests(
    jsonSchemaTestSuiteBase,
    annotationOutputPath,
    testFile => Console.WriteLine($"  Annotation test: '{testFile}'"));