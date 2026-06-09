// <copyright file="ResponderWorkflowOverBrokerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER
using System.Reflection;
using System.Text;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.Arazzo11;
using Corvus.Text.Json.AsyncApi.CodeGeneration;
using Corvus.Text.Json.AsyncApi.Nats;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Testcontainers.Nats;

namespace Corvus.Text.Json.AsyncApi.Transport.IntegrationTests;

/// <summary>
/// End-to-end proof that a <em>code-generated</em> Arazzo responder workflow runs over a real broker: a
/// request/reply receive step is emitted, compiled in-memory with Roslyn, then run as a responder service
/// against a real NATS container while a separate requester service round-trips through the broker. This
/// closes the gap between the responder primitive (covered by the transport tests) and the generated
/// workflow that calls it (covered in-process) — here the two halves meet over the wire.
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public class ResponderWorkflowOverBrokerTests
{
    private const string ResponderDocument = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "events", "url": "./events.yaml", "type": "asyncapi" } ],
          "workflows": [
            {
              "workflowId": "respond",
              "steps": [
                {
                  "stepId": "serve",
                  "channelPath": "requests",
                  "action": "receive",
                  "requestBody": { "payload": "$message.payload" },
                  "successCriteria": [ { "condition": "$message.payload#/n == 21" } ],
                  "outputs": { "n": "$message.payload#/n" }
                }
              ],
              "outputs": { "served": "$steps.serve.outputs.n" }
            }
          ]
        }
        """;

    private static NatsContainer s_container = null!;

    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        s_container = new NatsBuilder("nats:2.11").Build();
        await s_container.StartAsync();
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        if (s_container is not null)
        {
            await s_container.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task CompiledResponderWorkflowRoundTripsOverNats()
    {
        // Emit + compile the responder workflow (a request/reply receive step that echoes the request).
        string source = EmitResponderWorkflow();
        source.ShouldContainReceiveAndReply();
        Assembly assembly = CompileInMemory(source);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.RespondWorkflow")!.GetMethod("ExecuteAsync")!;

        string url = s_container.GetConnectionString();
        await using NatsMessageTransport responder = await NatsMessageTransport.CreateAsync(new NatsTransportOptions { Url = url });
        await using NatsMessageTransport requester = await NatsMessageTransport.CreateAsync(new NatsTransportOptions { Url = url });

        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());

        // A responder workflow resumes off the broker's delivery thread (the one-shot reply wrapper uses
        // asynchronous continuations), so its products must be built in a non-thread-affine workspace.
        using JsonWorkspace workspace = JsonWorkspace.CreateUnrented();

        // Start the compiled responder service: it subscribes (request/reply) and awaits one request. The
        // first parameter is the HTTP transport — unused by a channel-only workflow, but the generated
        // executor null-guards it, so a no-op MockApiTransport is supplied.
        var apiTransport = new MockApiTransport();
        var pending = (ValueTask<JsonElement>)execute.Invoke(
            null,
            [apiTransport, responder, workspace, inputs.RootElement, default(CancellationToken)])!;
        System.Threading.Tasks.Task<JsonElement> responderTask = pending.AsTask();

        // Let the responder's subscription register on the broker before the requester sends.
        await Task.Delay(500);

        // The requester service round-trips a request through NATS.
        using ParsedJsonDocument<JsonElement> requestDocument = ParsedJsonDocument<JsonElement>.Parse("""{"n":21}"""u8.ToArray());
        (JsonElement reply, JsonElement _) = await requester.RequestAsync<JsonElement, JsonElement>(
            "requests"u8.ToArray(),
            "replies"u8.ToArray(),
            requestDocument.RootElement,
            "corr-responder-workflow"u8.ToArray());

        JsonElement outputs = await responderTask;

        // The compiled responder echoed the request payload back over the broker, and projected its output.
        Assert.AreEqual(JsonValueKind.Object, reply.ValueKind);
        Assert.AreEqual(21, reply.GetProperty("n"u8).GetInt32());
        Assert.AreEqual(21, outputs.GetProperty("served"u8).GetInt32());
    }

    private static string EmitResponderWorkflow()
    {
        // A request/reply receive descriptor: a receive operation that declares a reply, so the generated
        // step is a one-shot responder calling ReceiveOneAndReplyAsync.
        var descriptor = new AsyncApiChannelDescriptor(
            "requests",
            OperationAction.Receive,
            "onRequest",
            ProducerClassName: null,
            IsDynamicAddress: false,
            ChannelParameters: [],
            Messages: [new AsyncApiChannelMessageDescriptor("request", "Corvus.Text.Json.JsonElement", null, null, null)],
            ReplyPayloadTypeName: "Corvus.Text.Json.JsonElement");

        var binder = new WorkflowOperationBinder([], [new SourceDescriptionChannels("events", [descriptor])]);
        using var doc = ParsedJsonDocument<ArazzoDocument>.Parse(Encoding.UTF8.GetBytes(ResponderDocument));
        ArazzoDocument.WorkflowObject workflow = doc.RootElement.Workflows.EnumerateArray().First();
        return WorkflowExecutorEmitter.Emit(
            workflow,
            binder,
            new WorkflowExecutorOptions("GeneratedWorkflows", "RespondWorkflow", "Corvus.Text.Json.JsonElement", "Corvus.Text.Json.JsonElement"));
    }

    private static Assembly CompileInMemory(string source)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, parseOptions);

        // Force-load the assemblies the emitted executor references so they appear in the loaded-assembly
        // reference set used for the in-memory compilation.
        _ = typeof(Corvus.Text.Json.JsonElement).Assembly;
        _ = typeof(Corvus.Text.Json.AsyncApi.IMessageTransport).Assembly;
        _ = typeof(WorkflowStepFailedException).Assembly;
        _ = typeof(Corvus.Text.Json.OpenApi.IApiTransport).Assembly;
        _ = typeof(Corvus.Text.Json.JsonPath.JsonPathResult).Assembly;
        _ = typeof(NodaTime.OffsetTime).Assembly;
        _ = typeof(System.Numerics.BigInteger).Assembly;

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();

        var compilation = CSharpCompilation.Create(
            "GeneratedWorkflows.IntegrationTests",
            [tree],
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable));

        using var peStream = new MemoryStream();
        EmitResult result = compilation.Emit(peStream);
        if (!result.Success)
        {
            string errors = string.Join(
                Environment.NewLine,
                result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));
            Assert.Fail($"Generated responder workflow failed to compile:{Environment.NewLine}{errors}{Environment.NewLine}--- source ---{Environment.NewLine}{source}");
        }

        peStream.Position = 0;
        return Assembly.Load(peStream.ToArray());
    }
}

internal static class ResponderWorkflowSourceAssertions
{
    public static void ShouldContainReceiveAndReply(this string source)
    {
        Assert.IsTrue(
            source.Contains("ReceiveOneAndReplyAsync<Corvus.Text.Json.JsonElement, Corvus.Text.Json.JsonElement>(", StringComparison.Ordinal),
            "Generated responder should call the one-shot request/reply wrapper.");
    }
}
#endif