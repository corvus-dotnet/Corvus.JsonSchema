// <copyright file="LambdaServerlessDeployer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon.Runtime;
using Corvus.Text.Json.Arazzo.Durability.Aot;

namespace Corvus.Text.Json.Arazzo.Durability.Serverless.Lambda.Deploy;

/// <summary>
/// The AWS Lambda <see cref="IServerlessDeployer"/> (ADR 0055, ADR 0059, ADR 0060). It packages a version's verified
/// native binary into a <c>provided.al2023</c> custom-runtime deployment zip and creates or updates a Lambda function
/// with an <c>AWS_IAM</c> Function URL, then returns that URL.
/// </summary>
/// <remarks>
/// The <see cref="IAmazonLambda"/> client is injected: the runner wires it to LocalStack for local development and to
/// real AWS in production, and only the client's endpoint differs (ADR 0060, one deployer for both). This class never
/// constructs a client and never handles credentials; the runner is the secure boundary that holds the environment's
/// cloud identity (ADR 0059). An AWS failure is returned as a <see cref="ServerlessDeployResult.Failure"/> rather than
/// thrown, matching how the runner's deploy service treats a deploy failure.
/// </remarks>
public sealed class LambdaServerlessDeployer : IServerlessDeployer
{
    private const int MaxStatusPolls = 30;
    private static readonly TimeSpan StatusPollDelay = TimeSpan.FromMilliseconds(500);

    private readonly IAmazonLambda lambda;
    private readonly LambdaDeployerOptions options;

    /// <summary>Initializes a new instance of the <see cref="LambdaServerlessDeployer"/> class.</summary>
    /// <param name="lambda">The AWS Lambda client, wired by the runner to LocalStack or to real AWS with the environment's identity.</param>
    /// <param name="options">The deployer options: the function's execution role ARN and its size/time limits.</param>
    public LambdaServerlessDeployer(IAmazonLambda lambda, LambdaDeployerOptions options)
    {
        ArgumentNullException.ThrowIfNull(lambda);
        ArgumentNullException.ThrowIfNull(options);
        this.lambda = lambda;
        this.options = options;
    }

    /// <inheritdoc/>
    public async ValueTask<ServerlessDeployResult> DeployAsync(ServerlessDeployRequest request, CancellationToken cancellationToken)
    {
        string functionName = FunctionName(request);
        byte[] zipBytes = BuildDeploymentZip(request.NativeBinary);
        Amazon.Lambda.Architecture architecture = Architecture(request.RuntimeIdentifier);

        try
        {
            bool updated = await this.FunctionExistsAsync(functionName, cancellationToken).ConfigureAwait(false);
            if (updated)
            {
                // The function already exists: replace its code with the new bootstrap zip.
                await this.lambda.UpdateFunctionCodeAsync(
                    new UpdateFunctionCodeRequest
                    {
                        FunctionName = functionName,
                        ZipFile = new MemoryStream(zipBytes),
                        Architectures = [architecture],
                    },
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // First deploy of this target: create the custom-runtime function from the bootstrap zip.
                await this.lambda.CreateFunctionAsync(
                    new CreateFunctionRequest
                    {
                        FunctionName = functionName,
                        Runtime = Amazon.Lambda.Runtime.ProvidedAl2023,
                        Role = this.options.ExecutionRoleArn,
                        Handler = "bootstrap",
                        Code = new FunctionCode { ZipFile = new MemoryStream(zipBytes) },
                        Architectures = [architecture],
                        MemorySize = this.options.MemorySizeMb,
                        Timeout = this.options.TimeoutSeconds,
                        PackageType = PackageType.Zip,

                        // The deployed environment's source configuration (base URLs) the baked function's transport
                        // binder reads to reach its sources (ADR 0059: the runner, not the control plane, holds it).
                        Environment = FunctionEnvironment(this.options.FunctionEnvironment),
                    },
                    cancellationToken).ConfigureAwait(false);
            }

            // The Function URL and any invoke only work once the function is Active and the code update has settled.
            await this.WaitForActiveAsync(functionName, cancellationToken).ConfigureAwait(false);

            string functionUrl = await this.EnsureFunctionUrlAsync(functionName, cancellationToken).ConfigureAwait(false);

            return ServerlessDeployResult.Success(
                functionUrl,
                $"{(updated ? "Updated" : "Created")} Lambda function '{functionName}' ({architecture.Value}, provided.al2023) with an AWS_IAM Function URL.");
        }
        catch (AmazonLambdaException ex)
        {
            return ServerlessDeployResult.Failure($"AWS Lambda deploy of '{functionName}' failed: {ex.Message}");
        }
        catch (AmazonServiceException ex)
        {
            return ServerlessDeployResult.Failure($"AWS deploy of '{functionName}' failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Packages a bootstrap into a <c>provided.al2023</c> deployment zip in memory: a single root entry named
    /// <c>bootstrap</c> whose bytes are the native binary, marked executable (Unix mode <c>0755</c>) via the zip
    /// entry's external file attributes so the Lambda custom runtime can launch it.
    /// </summary>
    /// <param name="bootstrap">The native binary to place at the zip root as <c>bootstrap</c>.</param>
    /// <returns>The deployment zip bytes.</returns>
    internal static byte[] BuildDeploymentZip(ReadOnlyMemory<byte> bootstrap)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry entry = archive.CreateEntry("bootstrap", CompressionLevel.Optimal);

            // The high 16 bits of the external attributes carry the Unix file mode. 0100755 = a regular file with
            // rwxr-xr-x, so the custom runtime can execute the bootstrap.
            entry.ExternalAttributes = Convert.ToInt32("100755", 8) << 16;

            using Stream entryStream = entry.Open();
            entryStream.Write(bootstrap.Span);
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Builds the deterministic, AWS-Lambda-safe function name for a deploy target. The name is
    /// <c>arazzo-fn-{base}-v{ver}-{env}-{rid}</c> with each of base/env/rid sanitized to the Lambda-safe set
    /// (<c>[a-zA-Z0-9-_]</c>); if the result exceeds 64 characters it is truncated and a short deterministic suffix
    /// (derived from the full untruncated name) is appended, so it stays unique and within Lambda's 64-character limit.
    /// </summary>
    /// <param name="request">The deploy request whose target tuple names the function.</param>
    /// <returns>The function name.</returns>
    internal static string FunctionName(ServerlessDeployRequest request)
    {
        string body = $"arazzo-fn-{Sanitize(request.BaseWorkflowId)}-v{request.VersionNumber}-{Sanitize(request.Environment)}-{Sanitize(request.RuntimeIdentifier)}";
        if (body.Length <= 64)
        {
            return body;
        }

        // Too long for Lambda: keep a truncated prefix and append a short hash of the full name so two distinct
        // over-long targets never collide.
        string suffix = ShortHash(body);
        int keep = 64 - 1 - suffix.Length;
        return $"{body[..keep]}-{suffix}";
    }

    /// <summary>
    /// Maps a .NET runtime identifier to the Lambda architecture: anything targeting Arm64 (a rid ending
    /// <c>-arm64</c>) maps to <see cref="Amazon.Lambda.Architecture.Arm64"/>; everything else (for example
    /// <c>linux-x64</c> or <c>win-x64</c>) maps to <see cref="Amazon.Lambda.Architecture.X86_64"/>.
    /// </summary>
    /// <param name="runtimeIdentifier">The .NET runtime identifier the native binary targets.</param>
    /// <returns>The Lambda architecture.</returns>
    internal static Amazon.Lambda.Architecture Architecture(string runtimeIdentifier)
    {
        return runtimeIdentifier.EndsWith("-arm64", StringComparison.OrdinalIgnoreCase)
            ? Amazon.Lambda.Architecture.Arm64
            : Amazon.Lambda.Architecture.X86_64;
    }

    // The function's environment block, or null when there is nothing to set (a null block leaves the function's
    // environment empty rather than clearing it to an empty map, which some platforms treat differently).
    private static Amazon.Lambda.Model.Environment? FunctionEnvironment(IReadOnlyDictionary<string, string>? variables)
        => variables is { Count: > 0 }
            ? new Amazon.Lambda.Model.Environment { Variables = new Dictionary<string, string>(variables, StringComparer.Ordinal) }
            : null;

    private static string Sanitize(string value)
    {
        // Map any character outside the Lambda-safe set to '-'.
        return string.Create(value.Length, value, static (span, source) =>
        {
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                span[i] = IsSafe(c) ? c : '-';
            }
        });
    }

    private static bool IsSafe(char c)
        => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-' or '_';

    private static string ShortHash(string value)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(value), hash);
        return Convert.ToHexStringLower(hash[..4]);
    }

    private async ValueTask<bool> FunctionExistsAsync(string functionName, CancellationToken cancellationToken)
    {
        try
        {
            await this.lambda.GetFunctionAsync(new GetFunctionRequest { FunctionName = functionName }, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (ResourceNotFoundException)
        {
            return false;
        }
    }

    private async ValueTask WaitForActiveAsync(string functionName, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < MaxStatusPolls; attempt++)
        {
            GetFunctionResponse response = await this.lambda
                .GetFunctionAsync(new GetFunctionRequest { FunctionName = functionName }, cancellationToken)
                .ConfigureAwait(false);

            FunctionConfiguration configuration = response.Configuration;
            bool active = configuration.State == State.Active;
            bool updateSettled = configuration.LastUpdateStatus is null || configuration.LastUpdateStatus == LastUpdateStatus.Successful;
            if (active && updateSettled)
            {
                return;
            }

            await Task.Delay(StatusPollDelay, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<string> EnsureFunctionUrlAsync(string functionName, CancellationToken cancellationToken)
    {
        try
        {
            GetFunctionUrlConfigResponse existing = await this.lambda
                .GetFunctionUrlConfigAsync(new GetFunctionUrlConfigRequest { FunctionName = functionName }, cancellationToken)
                .ConfigureAwait(false);
            return existing.FunctionUrl;
        }
        catch (ResourceNotFoundException)
        {
            // AWS_IAM auth per ADR 0059: the Function URL is never public. LocalStack Community ignores IAM but still
            // returns a URL, so the deploy mechanics are proven locally and the auth is proven against real AWS.
            CreateFunctionUrlConfigResponse created = await this.lambda
                .CreateFunctionUrlConfigAsync(
                    new CreateFunctionUrlConfigRequest { FunctionName = functionName, AuthType = FunctionUrlAuthType.AWS_IAM },
                    cancellationToken)
                .ConfigureAwait(false);
            return created.FunctionUrl;
        }
    }
}