using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// End-to-end tests for the <c>arazzo-generate</c> CLI command: it generates the OpenAPI client/models
/// for each source description and a strongly-typed executor (and inputs model) per workflow.
/// </summary>
[TestClass]
public class ArazzoGenerateCommandTests : IDisposable
{
    private readonly string _outputDir;

    public ArazzoGenerateCommandTests()
    {
        _outputDir = CodeGeneratorRunner.CreateTempOutputDirectory();
    }

    public void Dispose()
    {
        CodeGeneratorRunner.CleanupTempDirectory(_outputDir);
        GC.SuppressFinalize(this);
    }

    [TestMethod]
    public async Task ArazzoGenerate_ProducesExecutorAndSourceClient()
    {
        string arazzo = CodeGeneratorRunner.GetFixturePath("Arazzo", "adopt.arazzo.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"arazzo-generate \"{arazzo}\" --rootNamespace TestWorkflows --outputPath \"{_outputDir}\"");

        Assert.AreEqual(0, result.ExitCode, $"Stdout: {result.StandardOutput} Stderr: {result.StandardError}");

        // The executor for the 'adopt' workflow was generated.
        string executorPath = Path.Combine(_outputDir, "Workflows", "AdoptWorkflow.cs");
        Assert.IsTrue(File.Exists(executorPath), $"Expected {executorPath}. Stdout: {result.StandardOutput}");

        // The OpenAPI client + models for the 'pets' source description were generated under Pets/.
        Assert.IsTrue(
            Directory.Exists(Path.Combine(_outputDir, "Pets"))
            && Directory.GetFiles(Path.Combine(_outputDir, "Pets"), "*.cs", SearchOption.AllDirectories).Length > 0,
            $"Expected generated OpenAPI client files under {Path.Combine(_outputDir, "Pets")}.");

        // The executor uses the strongly-typed input accessor and the generated source namespace.
        string executor = await File.ReadAllTextAsync(executorPath);
        StringAssert.Contains(executor, "((JsonElement)inputs.PetId)");
        StringAssert.Contains(executor, "TestWorkflows.Pets");

        // The inputs model for the workflow was generated under Models/Adopt/.
        Assert.IsTrue(
            Directory.Exists(Path.Combine(_outputDir, "Models", "Adopt")),
            $"Expected inputs model under {Path.Combine(_outputDir, "Models", "Adopt")}.");
    }

    [TestMethod]
    public async Task ArazzoGenerate_WithAsyncApiSource_ProducesProducerAndChannelStep()
    {
        string arazzo = CodeGeneratorRunner.GetFixturePath("Arazzo", "notify.arazzo.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"arazzo-generate \"{arazzo}\" --rootNamespace TestWorkflows --outputPath \"{_outputDir}\"");

        Assert.AreEqual(0, result.ExitCode, $"Stdout: {result.StandardOutput} Stderr: {result.StandardError}");

        // The executor for the 'notify' workflow was generated.
        string executorPath = Path.Combine(_outputDir, "Workflows", "NotifyWorkflow.cs");
        Assert.IsTrue(File.Exists(executorPath), $"Expected {executorPath}. Stdout: {result.StandardOutput}");

        // The AsyncAPI producer + message models for the 'events' source description were generated
        // under Events/ (the producer the channel step publishes through).
        Assert.IsTrue(
            Directory.Exists(Path.Combine(_outputDir, "Events"))
            && Directory.GetFiles(Path.Combine(_outputDir, "Events"), "*Producer.cs", SearchOption.AllDirectories).Length > 0,
            $"Expected a generated AsyncAPI producer under {Path.Combine(_outputDir, "Events")}.");

        // The executor takes an IMessageTransport and publishes through the generated producer for the
        // 'events' source namespace.
        string executor = await File.ReadAllTextAsync(executorPath);
        StringAssert.Contains(executor, "IMessageTransport messageTransport");
        StringAssert.Contains(executor, "new TestWorkflows.Events.NotifyProducer(messageTransport)");
    }

    [TestMethod]
    public async Task ArazzoGenerate_WithAsyncApi26Source_ProducesProducerAndChannelStep()
    {
        string arazzo = CodeGeneratorRunner.GetFixturePath("Arazzo", "notify26.arazzo.json");

        ProcessResult result = await CodeGeneratorRunner.RunAsync(
            $"arazzo-generate \"{arazzo}\" --rootNamespace TestWorkflows --outputPath \"{_outputDir}\"");

        Assert.AreEqual(0, result.ExitCode, $"Stdout: {result.StandardOutput} Stderr: {result.StandardError}");

        // The AsyncAPI 2.6 'subscribe' operation generated a producer under the 'events' source segment.
        Assert.IsTrue(
            Directory.Exists(Path.Combine(_outputDir, "Events"))
            && Directory.GetFiles(Path.Combine(_outputDir, "Events"), "*Producer.cs", SearchOption.AllDirectories).Length > 0,
            $"Expected a generated AsyncAPI producer under {Path.Combine(_outputDir, "Events")}.");

        // The executor publishes through the producer derived from the 2.6 subscribe operationId.
        string executor = await File.ReadAllTextAsync(Path.Combine(_outputDir, "Workflows", "NotifyWorkflow.cs"));
        StringAssert.Contains(executor, "IMessageTransport messageTransport");
        StringAssert.Contains(executor, "new TestWorkflows.Events.NotifyProducer(messageTransport)");
    }
}
