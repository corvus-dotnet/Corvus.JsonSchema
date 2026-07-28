// <copyright file="AotHostAppAssembler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using Corvus.Text.Json.Arazzo.Execution;

namespace Corvus.Text.Json.Arazzo.Durability.Aot;

/// <summary>
/// Assembles the thin serverless host-app around a version's already-signed executor assembly (ADR 0055). It emits the
/// entry stub, the project file, and the hermetic feed config, ready for a target's <see cref="IWorkflowAotBuilder"/> to
/// compile. It does not re-run the generator or Roslyn: the executor is compiled once, at catalog-add, and the artifact
/// is derived from that signed IL. The <em>form-factor</em> is per target (ADR 0055 amendment, ADR 0061): a runtime-less
/// target (AWS Lambda's <c>provided.al2023</c>) compiles to a self-contained Native-AOT <c>bootstrap</c>; a
/// runtime-present target (Azure Functions' <c>dotnet-isolated</c> worker) compiles to a framework-dependent ReadyToRun
/// isolated-worker app.
/// </summary>
public sealed class AotHostAppAssembler
{
    private const string ProjectFileName = "fn.csproj";
    private const string ProgramFileName = "Program.cs";
    private const string NuGetConfigFileName = "nuget.config";
    private const string InvokeFunctionFileName = "ArazzoInvokeFunction.cs";
    private const string HostJsonFileName = "host.json";

    // The shared transport binder both targets' entry stubs use: each configured source's base URL arrives as an
    // ARAZZO_SOURCE__<name> environment variable (set by the deploy), so the baked binder builds one HttpClientTransport
    // per source. Held as one literal so the two entry templates cannot drift.
    private const string BindFunction =
        """
        // Transport binding from the deployed environment's source configuration: each HTTP source's base URL is supplied to
        // the function as an ARAZZO_SOURCE__<name> environment variable (set by the deploy from the environment's source
        // registry), so the baked binder builds one HttpClientTransport per source. A source with no configured URL binds
        // nothing (its steps fault if called). Message (AsyncAPI) transports are not yet bound here.
        static WorkflowTransports Bind(WorkflowDescriptor descriptor, SecurityTagSet tags)
        {
            const string prefix = "ARAZZO_SOURCE__";
            var apiTransports = new Dictionary<string, IApiTransport>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                if (entry.Key is string key && key.StartsWith(prefix, StringComparison.Ordinal)
                    && entry.Value is string url && url.Length > 0
                    && Uri.TryCreate(url, UriKind.Absolute, out Uri? baseAddress))
                {
                    apiTransports[key.Substring(prefix.Length)] = new HttpClientTransport(new HttpClient { BaseAddress = baseAddress });
                }
            }

            return new WorkflowTransports(apiTransports, WorkflowTransports.NoMessageTransports);
        }
        """;

    /// <summary>
    /// Assembles the host-app for one runtime target.
    /// </summary>
    /// <param name="executorAssembly">The version's signed executor IL assembly (<c>metadata/executor.dll</c>).</param>
    /// <param name="executorManifest">The executor manifest (for the entry type and engine version).</param>
    /// <param name="runtimeIdentifier">The .NET runtime identifier to target (e.g. <c>linux-x64</c>).</param>
    /// <param name="options">The target, feed, and package versions the host-app references.</param>
    /// <returns>The assembled host-app.</returns>
    /// <exception cref="ArgumentException">The runtime identifier is empty, or the manifest has no engine version (a manifest written before format 2, which cannot be safely compiled).</exception>
    public AssembledHostApp Assemble(ReadOnlyMemory<byte> executorAssembly, ReadOnlyMemory<byte> executorManifest, string runtimeIdentifier, AotHostAppOptions options)
    {
        ArgumentException.ThrowIfNullOrEmpty(runtimeIdentifier);
        ArgumentNullException.ThrowIfNull(options);

        WorkflowExecutorManifest manifest = WorkflowExecutorManifest.Parse(executorManifest);
        if (string.IsNullOrEmpty(manifest.EngineVersion))
        {
            throw new ArgumentException(
                "The executor manifest records no engineVersion (it predates manifest format 2), so a matching runtime cannot be pinned for the native build; re-add the version to record it.",
                nameof(executorManifest));
        }

        string executorAssemblyName = ReadAssemblyName(executorAssembly);

        // The executor file is named after its assembly simple name. For Native AOT this is required: ILC (and the
        // runtime AOT type loader) resolve a referenced assembly by simple name (GetModuleForSimpleName), NOT by the
        // <Reference>'s HintPath, so a fixed 'executor.dll' whose identity differs cannot be found and the entry type
        // fails to load. For ReadyToRun the same name keeps the reference and the loaded assembly aligned.
        string executorFileName = $"{executorAssemblyName}.dll";

        var files = new List<AotProjectFile>
        {
            Text(ProjectFileName, Project(runtimeIdentifier, executorAssemblyName, options)),
            Text(NuGetConfigFileName, NuGetConfig(options)),
            new(executorFileName, executorAssembly),
        };

        if (options.Target == ServerlessTarget.AzureFunctions)
        {
            // The isolated worker is a Functions app: the entry host, the [Function] HTTP trigger the Worker source
            // generator discovers in the project, and the host.json that marks it a v4 Functions app.
            files.Add(Text(ProgramFileName, AzureProgram(manifest.EntryType)));
            files.Add(Text(InvokeFunctionFileName, AzureInvokeFunction()));
            files.Add(Text(HostJsonFileName, AzureHostJson()));
        }
        else
        {
            files.Add(Text(ProgramFileName, LambdaProgram(manifest.EntryType)));
        }

        return new AssembledHostApp(runtimeIdentifier, manifest.EntryType, manifest.EngineVersion!, files, options.Target);
    }

    // The executor assembly's name, read from its PE metadata without loading it, so the project reference matches the
    // assembly identity the compiler and loader link.
    private static string ReadAssemblyName(ReadOnlyMemory<byte> assembly)
    {
        using var stream = new MemoryStream(assembly.ToArray(), writable: false);
        using var pe = new PEReader(stream);
        MetadataReader metadata = pe.GetMetadataReader();
        return metadata.GetString(metadata.GetAssemblyDefinition().Name);
    }

    private static AotProjectFile Text(string path, string content) => new(path, Encoding.UTF8.GetBytes(content));

    private static string LambdaProgram(string entryType) =>
$$"""
// Generated by the workflow AOT builder (ADR 0055): the baked per-(environment, version) serverless function. It runs
// the Lambda custom-runtime loop with the version's executor compiled in; the executor is the signed executor.dll this
// project references, constructed directly (no loader, no reflection) so the whole host is Native-AOT compiled.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Serverless.Lambda;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;

await LambdaServerlessFunction.RunAsync(new BakedHostedWorkflowResolver(new {{entryType}}()), Bind);

{{BindFunction}}
""";

    private static string AzureProgram(string entryType) =>
$$"""
// Generated by the workflow AOT builder (ADR 0055/0061): the baked per-(environment, version) serverless function. It
// runs as an Azure Functions isolated worker (framework-dependent ReadyToRun) with the version's executor compiled in;
// the HTTP-trigger [Function] (ArazzoInvokeFunction) feeds each invocation to the vendor-neutral ServerlessInvocationHandler.
// The executor is the signed executor.dll this project references, constructed directly (no loader, no reflection).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Serverless;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

FunctionsApplicationBuilder builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();

// One invocation handler for the worker instance's life so checkpoint connections pool across warm invocations; the
// [Function] resolves it per request. The executor is the signed executor.dll, constructed directly.
builder.Services.AddSingleton(new ServerlessInvocationHandler(new BakedHostedWorkflowResolver(new {{entryType}}()), Bind, new SocketsHttpHandler()));

builder.Build().Run();

{{BindFunction}}
""";

    // The isolated-worker HTTP trigger, in its own file so the Worker source generator discovers it in the host-app. It
    // is fixed (no per-version substitution): the version varies only through the injected handler the entry registers.
    private static string AzureInvokeFunction() =>
"""
// Generated by the workflow AOT builder (ADR 0061): the isolated-worker HTTP trigger. It forwards each invocation body
// to the vendor-neutral ServerlessInvocationHandler and returns the outcome document verbatim.
using System.IO;
using System.Threading.Tasks;
using Corvus.Text.Json.Arazzo.Durability.Serverless;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;

namespace Corvus.Workflows.Generated.Serverless;

public sealed class ArazzoInvokeFunction
{
    private readonly ServerlessInvocationHandler handler;

    public ArazzoInvokeFunction(ServerlessInvocationHandler handler) => this.handler = handler;

    [Function("invoke")]
    public async Task<IResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest request)
    {
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, request.HttpContext.RequestAborted);

        // The outcome bytes are already-serialized JSON from the neutral handler, returned verbatim; Results.Bytes avoids
        // the reflection-based JSON output path (trim-unsafe) that Results.Json(object) would take.
        byte[] outcome = await this.handler.HandleAsync(buffer.ToArray(), request.HttpContext.RequestAborted);
        return Results.Bytes(outcome, "application/json");
    }
}
""";

    private static string AzureHostJson() =>
"""
{
  "version": "2.0"
}
""";

    private static string Project(string runtimeIdentifier, string executorAssemblyName, AotHostAppOptions options) =>
        options.Target == ServerlessTarget.AzureFunctions
            ? AzureProject(runtimeIdentifier, executorAssemblyName, options)
            : LambdaProject(runtimeIdentifier, executorAssemblyName, options);

    private static string LambdaProject(string runtimeIdentifier, string executorAssemblyName, AotHostAppOptions options) =>
$"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
    <PublishAot>true</PublishAot>
    <!-- The Lambda custom runtime (provided.al2023) expects the executable named 'bootstrap'. -->
    <AssemblyName>bootstrap</AssemblyName>
    <RuntimeIdentifier>{runtimeIdentifier}</RuntimeIdentifier>
    <StripSymbols>true</StripSymbols>
    <InvariantGlobalization>true</InvariantGlobalization>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Amazon.Lambda.Core" Version="{options.AmazonLambdaCoreVersion}" />
    <PackageReference Include="Amazon.Lambda.RuntimeSupport" Version="{options.AmazonLambdaRuntimeSupportVersion}" />
    <!-- The runtime graph as packages at the version whose assemblies match the executor's engine version. -->
    <PackageReference Include="{options.LambdaShimPackageId}" Version="{options.RuntimePackageVersion}" />
    <!-- The HTTP transport the baked Bind() constructs (HttpClientTransport) for each configured source. -->
    <PackageReference Include="Corvus.Text.Json.OpenApi.HttpTransport" Version="{options.RuntimePackageVersion}" />
  </ItemGroup>
  <ItemGroup>
    <!-- The version's already-signed executor IL; ILC links it into the native binary. The file is named after the
         assembly simple name so ILC resolves it by name (see AotHostAppAssembler.Assemble). -->
    <Reference Include="{executorAssemblyName}">
      <HintPath>{executorAssemblyName}.dll</HintPath>
    </Reference>
    <!-- Root the executor for ILC: its types are reached only through the entry type constructed in Program, so the
         AOT trimmer would otherwise drop the assembly's metadata and the entry type fails to load at startup with a
         type-loader FileNotFoundException for the executor assembly. Rooting keeps all its types + metadata compiled in. -->
    <TrimmerRootAssembly Include="{executorAssemblyName}" />
  </ItemGroup>
</Project>
""";

    private static string AzureProject(string runtimeIdentifier, string executorAssemblyName, AotHostAppOptions options) =>
$"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <AzureFunctionsVersion>v4</AzureFunctionsVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
    <AssemblyName>handler</AssemblyName>
    <!-- Framework-dependent ReadyToRun: the dotnet-isolated Functions host provides the runtime, and R2R + the host
         placeholder pre-warm is Azure's optimal cold start (ADR 0055 per-target form-factor, ADR 0061). -->
    <PublishReadyToRun>true</PublishReadyToRun>
    <SelfContained>false</SelfContained>
    <RuntimeIdentifier>{runtimeIdentifier}</RuntimeIdentifier>
    <InvariantGlobalization>true</InvariantGlobalization>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker" Version="{options.AzureFunctionsWorkerVersion}" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Sdk" Version="{options.AzureFunctionsWorkerSdkVersion}" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore" Version="{options.AzureFunctionsWorkerHttpAspNetCoreVersion}" />
    <!-- The vendor-neutral serverless core the baked [Function] feeds each invocation to. -->
    <PackageReference Include="Corvus.Text.Json.Arazzo.Durability.Serverless" Version="{options.RuntimePackageVersion}" />
    <!-- The HTTP transport the baked Bind() constructs (HttpClientTransport) for each configured source. -->
    <PackageReference Include="Corvus.Text.Json.OpenApi.HttpTransport" Version="{options.RuntimePackageVersion}" />
  </ItemGroup>
  <ItemGroup>
    <!-- The version's already-signed executor IL, referenced and copied to the output for the isolated worker to load
         by name at runtime (framework-dependent, so no trimming or ILC name-resolution constraint applies). -->
    <Reference Include="{executorAssemblyName}">
      <HintPath>{executorAssemblyName}.dll</HintPath>
    </Reference>
  </ItemGroup>
</Project>
""";

    private static string NuGetConfig(AotHostAppOptions options)
    {
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"<?xml version=\"1.0\" encoding=\"utf-8\"?>{Environment.NewLine}");
        builder.Append(CultureInfo.InvariantCulture, $"<configuration>{Environment.NewLine}");
        builder.Append(CultureInfo.InvariantCulture, $"  <packageSources>{Environment.NewLine}");
        builder.Append(CultureInfo.InvariantCulture, $"    <clear />{Environment.NewLine}");
        foreach ((string name, string value) in options.FeedSources)
        {
            builder.Append(CultureInfo.InvariantCulture, $"    <add key=\"{name}\" value=\"{value}\" />{Environment.NewLine}");
        }

        builder.Append(CultureInfo.InvariantCulture, $"  </packageSources>{Environment.NewLine}");
        builder.Append(CultureInfo.InvariantCulture, $"</configuration>{Environment.NewLine}");
        return builder.ToString();
    }
}

/// <summary>The target, feed, and package versions an assembled host-app references.</summary>
public sealed record AotHostAppOptions
{
    /// <summary>Gets the serverless platform the host-app is assembled and compiled for. Defaults to <see cref="ServerlessTarget.AwsLambda"/>.</summary>
    public ServerlessTarget Target { get; init; } = ServerlessTarget.AwsLambda;

    /// <summary>Gets the NuGet version of the Corvus runtime packages the host-app references. Its assemblies must match the executor's engine version.</summary>
    public required string RuntimePackageVersion { get; init; }

    /// <summary>Gets the NuGet feed sources the build restores from (name, value), in order. The deploy provides the feed that resolves the runtime package version.</summary>
    public required IReadOnlyList<(string Name, string Value)> FeedSources { get; init; }

    /// <summary>Gets the package id of the AWS Lambda entry shim the Lambda host-app references.</summary>
    public string LambdaShimPackageId { get; init; } = "Corvus.Text.Json.Arazzo.Durability.Serverless.Lambda";

    /// <summary>Gets the Amazon.Lambda.Core version (AWS Lambda target).</summary>
    public string AmazonLambdaCoreVersion { get; init; } = "2.6.0";

    /// <summary>Gets the Amazon.Lambda.RuntimeSupport version (AWS Lambda target).</summary>
    public string AmazonLambdaRuntimeSupportVersion { get; init; } = "1.13.1";

    /// <summary>Gets the Microsoft.Azure.Functions.Worker version (Azure Functions target).</summary>
    public string AzureFunctionsWorkerVersion { get; init; } = "2.52.0";

    /// <summary>Gets the Microsoft.Azure.Functions.Worker.Sdk version (Azure Functions target).</summary>
    public string AzureFunctionsWorkerSdkVersion { get; init; } = "2.1.0";

    /// <summary>Gets the Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore version (Azure Functions target).</summary>
    public string AzureFunctionsWorkerHttpAspNetCoreVersion { get; init; } = "2.1.1";
}