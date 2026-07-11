using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Tests for the default 'generate' command using the V5 engine.
/// </summary>
[TestClass]
public class GenerateCommandTests : IDisposable
{
    private readonly string _outputDir;

    public GenerateCommandTests()
    {
        _outputDir = CodeGeneratorRunner.CreateTempOutputDirectory();
    }

    public void Dispose()
    {
        CodeGeneratorRunner.CleanupTempDirectory(_outputDir);
    }

    [TestMethod]
    public async Task Generate_SimpleObject_ProducesFiles()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\"");

        Assert.AreEqual(0, result.ExitCode);
        Assert.IsTrue(
            Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories).Length > 0,
            $"Expected generated .cs files in {_outputDir}. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
    }

    [TestMethod]
    public async Task Generate_ArrayType_ProducesFiles()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "array-type.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\"");

        Assert.AreEqual(0, result.ExitCode);
        Assert.IsTrue(
            Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories).Length > 0,
            $"Expected generated .cs files. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
    }

    [TestMethod]
    public async Task Generate_ComposedType_ProducesFiles()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "composed-type.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\"");

        Assert.AreEqual(0, result.ExitCode);
        Assert.IsTrue(
            Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories).Length > 0,
            $"Expected generated .cs files. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
    }

    [TestMethod]
    public async Task Generate_WithOutputRootTypeName_UsesSpecifiedName()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\" --outputRootTypeName MyPerson");

        Assert.AreEqual(0, result.ExitCode);

        string[] files = Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories);
        Assert.IsTrue(files.Length > 0, "Expected generated files");

        // The root type file should contain the specified type name
        bool foundTypeName = files.Any(f => Path.GetFileName(f).Contains("MyPerson", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(foundTypeName, $"Expected a file containing 'MyPerson'. Files: {string.Join(", ", files.Select(Path.GetFileName))}");
    }

    [TestMethod]
    public async Task Generate_WithOutputMapFile_CreatesMapFile()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.json");
        string mapFile = Path.Combine(_outputDir, "output.map.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\" --outputMapFile \"{mapFile}\"");

        Assert.AreEqual(0, result.ExitCode);
        Assert.IsTrue(File.Exists(mapFile), $"Expected map file at {mapFile}. Stdout: {result.StandardOutput}");

        string mapContent = await File.ReadAllTextAsync(mapFile);
        Assert.StartsWith("[", mapContent.TrimStart());
    }

    [TestMethod]
    public async Task Generate_WithV4Engine_ProducesFiles()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\" --engine V4");

        Assert.AreEqual(0, result.ExitCode);
        Assert.IsTrue(
            Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories).Length > 0,
            $"Expected V4 generated files. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
    }

    [TestMethod]
    public async Task Generate_V4BooleanNullUnion_ImplementsJsonValueInterfaceOnNet8Plus()
    {
        // Regression test for #832. A ["boolean", "null"] union generated with the V4 engine did not
        // implement IJsonValue<T> on net8.0+, so the generated code failed to compile: CS0315 (the
        // JsonValueConverter<T> attribute constraint) and CS0540 (the explicit IJsonValue members).
        // The "null" core type contributes no type-family interface, and IJsonBoolean<T> — the only
        // other candidate supplier of IJsonValue<T> — is declared only on pre-net8.0 frameworks for a
        // union (a union converts to bool explicitly, so it cannot satisfy IJsonBoolean<T>'s static
        // abstract "implicit operator bool" on net8.0+). The generator must therefore declare
        // IJsonValue<T> directly on net8.0+.
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "boolean-null-union.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputRootTypeName NullableBool --outputPath \"{_outputDir}\" --engine V4");

        Assert.AreEqual(0, result.ExitCode, $"Stdout: {result.StandardOutput} Stderr: {result.StandardError}");

        // The core partial carries [JsonConverter(typeof(JsonValueConverter<NullableBool>))], whose type
        // argument requires NullableBool : IJsonValue<NullableBool>. Inspect that file's base list.
        string coreFile = Directory
            .GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories)
            .Single(f => File.ReadAllText(f).Contains("JsonValueConverter<NullableBool>", StringComparison.Ordinal));
        string content = await File.ReadAllTextAsync(coreFile);

        // Isolate the struct's base-interface list (from the declaration to the opening body brace).
        int structIndex = content.IndexOf("partial struct NullableBool", StringComparison.Ordinal);
        int bodyBraceIndex = content.IndexOf('{', structIndex);
        string baseList = content.Substring(structIndex, bodyBraceIndex - structIndex);

        Assert.IsTrue(
            baseList.Contains("IJsonValue<", StringComparison.Ordinal),
            "Expected the core partial for a [\"boolean\", \"null\"] union to declare IJsonValue<T> in its " +
            $"base-interface list so it implements IJsonValue<T> on net8.0+ (regression #832). Base list:\n{baseList}");

        // The IJsonValue<T> base declaration must be present on net8.0+, not confined to pre-net8.0.
        Assert.IsFalse(
            baseList.Contains("#if !NET8_0_OR_GREATER", StringComparison.Ordinal),
            "The IJsonValue<T> base declaration for a [\"boolean\", \"null\"] union must be present on net8.0+ " +
            $"(regression #832). Base list:\n{baseList}");
    }

    [TestMethod]
    public async Task Generate_V4ConstrainedAndNullableInteger_EmitsImplicitPreferredNumericOperators()
    {
        // Regression test for #834. The V4 engine classified a type as a "multi-core union" (whose
        // numeric conversions are explicit for branch safety) by counting ImpliedCoreTypes() flags.
        // That count unions keyword implications, so "type": "integer" plus a numeric constraint
        // (minimum/maximum imply Number) counted Integer|Number = 2, and ["integer", "null"] counted
        // Integer|Null = 2 — demoting the 4.x implicit conversions for the preferred .NET numeric
        // type to explicit. Only a union that can hold a non-numeric value kind may force explicit.
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "constrained-integer-operators.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputRootTypeName OperatorsRoot --outputPath \"{_outputDir}\" --engine V4");

        Assert.AreEqual(0, result.ExitCode, $"Stdout: {result.StandardOutput} Stderr: {result.StandardError}");

        string allContent = ReadAllGeneratedContent(_outputDir);

        // A constrained int32 integer ("count") converts to and from its preferred type implicitly.
        AssertContainsOperator(allContent, "public static implicit operator int(CountValue value)");
        AssertContainsOperator(allContent, "public static implicit operator CountValue(int value)");

        // Non-preferred numeric conversions remain explicit, as in 4.x.
        AssertContainsOperator(allContent, "public static explicit operator long(CountValue value)");

        // A nullable integer ("total", no format) prefers long, implicitly, as in 4.x.
        AssertContainsOperator(allContent, "public static implicit operator long(TotalValue value)");
        AssertContainsOperator(allContent, "public static implicit operator TotalValue(long value)");

        // A union with a non-numeric branch ("mixed") keeps the explicit conversions introduced in 5.1
        // — an implicit conversion could throw when the instance holds the string branch.
        AssertContainsOperator(allContent, "public static explicit operator long(MixedValue value)");
        AssertContainsOperator(allContent, "public static explicit operator MixedValue(long value)");
    }

    [TestMethod]
    public async Task Generate_V5ConstrainedAndNullableInteger_EmitsImplicitNumericOperators()
    {
        // Companion to the V4 regression test for #834. The V5 engine had the same quirk from the
        // same root cause: ImpliedCoreTypes() flag-counting sent a constrained integer (Integer|Number)
        // or a nullable numeric (Integer|Null) down the multi-type-union branch, demoting the numeric
        // conversions that a bare "type": "integer" emits implicitly (long, double) to explicit.
        // A type whose implied kinds are all within the numeric domain (Number/Integer/Null) must emit
        // the same implicit conversions as a single-core numeric type.
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "constrained-integer-operators.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputRootTypeName OperatorsRoot --outputPath \"{_outputDir}\"");

        Assert.AreEqual(0, result.ExitCode, $"Stdout: {result.StandardOutput} Stderr: {result.StandardError}");

        string allContent = ReadAllGeneratedContent(_outputDir);

        // A constrained int32 integer ("count") keeps its format-driven implicit int conversion and
        // emits the standard implicit numeric conversions of a single-core integer type.
        AssertContainsOperator(allContent, "public static implicit operator int(CountValue value)");
        AssertContainsOperator(allContent, "public static implicit operator long(CountValue value)");
        AssertContainsOperator(allContent, "public static implicit operator double(CountValue value)");

        // Allocating conversions stay explicit, matching the single-core numeric shape.
        AssertContainsOperator(allContent, "public static explicit operator decimal(CountValue value)");

        // A nullable integer ("total") emits the same implicit conversions as a single-core integer.
        AssertContainsOperator(allContent, "public static implicit operator long(TotalValue value)");
        AssertContainsOperator(allContent, "public static implicit operator double(TotalValue value)");

        // A union with a non-numeric branch ("mixed") keeps explicit numeric conversions — an implicit
        // conversion could throw when the instance holds the string branch.
        AssertContainsOperator(allContent, "public static explicit operator long(MixedValue value)");
        AssertContainsOperator(allContent, "public static explicit operator double(MixedValue value)");
    }

    private static string ReadAllGeneratedContent(string outputDir)
    {
        string[] files = Directory.GetFiles(outputDir, "*.cs", SearchOption.AllDirectories);
        Assert.IsTrue(files.Length > 0, "Expected generated .cs files");
        return string.Join("\n", files.Select(File.ReadAllText));
    }

    private static void AssertContainsOperator(string allContent, string expectedSignature)
    {
        Assert.IsTrue(
            allContent.Contains(expectedSignature, StringComparison.Ordinal),
            $"Expected the generated code to contain \"{expectedSignature}\" (regression #834).");
    }

    [TestMethod]
    public async Task Generate_WithOptionalAsNullable_ProducesFiles()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\" --optionalAsNullable NullOrUndefined");

        Assert.AreEqual(0, result.ExitCode);
        Assert.IsTrue(
            Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories).Length > 0,
            $"Expected generated files. Stdout: {result.StandardOutput} Stderr: {result.StandardError}");
    }

    [TestMethod]
    public async Task Generate_WithDefaultAccessibilityInternal_GeneratesInternalRootType()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\" --outputRootTypeName InternalPerson --defaultAccessibility Internal");

        Assert.AreEqual(0, result.ExitCode);

        string generatedCode = string.Concat(Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.AreEqual(3, generatedCode.Split("internal readonly partial struct InternalPerson").Length - 1);
        Assert.AreEqual(0, generatedCode.Split("public readonly partial struct InternalPerson").Length - 1);
    }

    [TestMethod]
    public async Task Generate_WithOutputRootAccessibility_OverridesDefaultAccessibility()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\" --outputRootTypeName PublicPerson --defaultAccessibility Internal --outputRootAccessibility Public");

        Assert.AreEqual(0, result.ExitCode);

        string generatedCode = string.Concat(Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.AreEqual(3, generatedCode.Split("public readonly partial struct PublicPerson").Length - 1);
        Assert.AreEqual(0, generatedCode.Split("internal readonly partial struct PublicPerson").Length - 1);
    }

    [TestMethod]
    public async Task Generate_WithV4EngineAndDefaultAccessibilityInternal_GeneratesInternalRootType()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\" --outputRootTypeName V4InternalPerson --defaultAccessibility Internal --engine V4");

        Assert.AreEqual(0, result.ExitCode);

        string generatedCode = string.Concat(Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.AreEqual(6, generatedCode.Split("internal readonly partial struct V4InternalPerson").Length - 1);
        Assert.AreEqual(0, generatedCode.Split("public readonly partial struct V4InternalPerson").Length - 1);
    }

    [TestMethod]
    public async Task Generate_WithV4EngineAndOutputRootAccessibility_OverridesDefaultAccessibility()
    {
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "simple-object.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace TestGenerated --outputPath \"{_outputDir}\" --outputRootTypeName V4PublicPerson --defaultAccessibility Internal --outputRootAccessibility Public --engine V4");

        Assert.AreEqual(0, result.ExitCode);

        string generatedCode = string.Concat(Directory.GetFiles(_outputDir, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.AreEqual(6, generatedCode.Split("public readonly partial struct V4PublicPerson").Length - 1);
        Assert.AreEqual(0, generatedCode.Split("internal readonly partial struct V4PublicPerson").Length - 1);
    }

    [TestMethod]
    public async Task Generate_AssertFormatFalse_DisablesFormatAssertion()
    {
        // Regression: --assertFormat was a value-less flag pinned to DefaultValue(true), so
        // '--assertFormat false' was silently ignored. It must now actually disable format assertion.
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "format-datetime-2020-12.json");
        string assertDir = Path.Combine(_outputDir, "assert");
        string annotateDir = Path.Combine(_outputDir, "annotate");

        ProcessResult asserted = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace T --outputPath \"{assertDir}\" --assertFormat true");
        ProcessResult annotated = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace T --outputPath \"{annotateDir}\" --assertFormat false");

        Assert.AreEqual(0, asserted.ExitCode, asserted.StandardError);
        Assert.AreEqual(0, annotated.ExitCode, annotated.StandardError);
        Assert.IsTrue(ReadAllGeneratedCode(assertDir).Contains("MatchDateTime"), "--assertFormat true must assert date-time.");
        Assert.IsFalse(ReadAllGeneratedCode(annotateDir).Contains("MatchDateTime"), "--assertFormat false must not assert date-time.");
    }

    [TestMethod]
    public async Task Generate_FormatModeDisable_DisablesFormatEvenForDraft07()
    {
        // A bare '--formatMode disable' (the '*' wildcard) disables format assertion for ALL drafts,
        // including draft-07 whose vocabulary asserts format (which '--assertFormat false' alone cannot).
        string schema = CodeGeneratorRunner.GetFixturePath("Schemas", "format-datetime-draft7.json");
        string defaultDir = Path.Combine(_outputDir, "default");
        string disabledDir = Path.Combine(_outputDir, "disabled");

        ProcessResult def = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace T --outputPath \"{defaultDir}\"");
        ProcessResult disabled = await CodeGeneratorRunner.RunAsync(
            $"jsonschema \"{schema}\" --rootNamespace T --outputPath \"{disabledDir}\" --formatMode disable");

        Assert.AreEqual(0, def.ExitCode, def.StandardError);
        Assert.AreEqual(0, disabled.ExitCode, disabled.StandardError);
        Assert.IsTrue(ReadAllGeneratedCode(defaultDir).Contains("MatchDateTime"), "draft-07 asserts format by default.");
        Assert.IsFalse(ReadAllGeneratedCode(disabledDir).Contains("MatchDateTime"), "--formatMode disable must produce annotation-only output.");
    }

    private static string ReadAllGeneratedCode(string directory) =>
        string.Concat(Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
}