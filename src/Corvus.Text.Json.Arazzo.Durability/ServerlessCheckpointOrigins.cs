// <copyright file="ServerlessCheckpointOrigins.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The checkpoint origins a deployed serverless function will load a run from and save it back to (ADR 0059
/// decision 4). An invocation names its own <c>checkpointUrl</c>, and the function advances whatever it loads from
/// there with its own source credentials, so the function accepts that URL only when its origin (scheme, host and
/// port) is one the deployment stamped onto it. The invoke authenticator keeps strangers out. This keeps a caller who
/// does hold the invoke credential from pointing the function at a checkpoint surface of their own.
/// </summary>
/// <remarks>
/// The list is required and has no wildcard. It is stamped at deploy time as the <see cref="SettingName"/> function
/// setting: the dispatching runner's checkpoint surface, and those of its peers where several runners serve one
/// environment, since a run checkpoints back to the runner that dispatched it. A function with no list refuses to
/// start, and a deployer refuses settings that carry none.
/// </remarks>
public sealed class ServerlessCheckpointOrigins
{
    /// <summary>The name of the function setting that carries the list: absolute URLs separated by semicolons.</summary>
    public const string SettingName = "ARAZZO_CHECKPOINT_ORIGINS";

    private readonly (string Scheme, string Host, int Port)[] origins;

    private ServerlessCheckpointOrigins((string Scheme, string Host, int Port)[] origins) => this.origins = origins;

    /// <summary>Gets the number of distinct origins in the list.</summary>
    public int Count => this.origins.Length;

    /// <summary>Parses a list of checkpoint origins.</summary>
    /// <param name="value">Absolute <c>http</c> or <c>https</c> URLs separated by semicolons. Only each URL's origin is kept.</param>
    /// <returns>The list.</returns>
    /// <exception cref="FormatException">The value is empty, or an entry is not an absolute <c>http</c> or <c>https</c> URL.</exception>
    public static ServerlessCheckpointOrigins Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            ThrowHelper.ThrowServerlessCheckpointOriginsMissing();
        }

        string[] entries = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var parsed = new List<(string Scheme, string Host, int Port)>(entries.Length);
        foreach (string entry in entries)
        {
            if (!Uri.TryCreate(entry, UriKind.Absolute, out Uri? url) || !IsHttp(url))
            {
                ThrowHelper.ThrowServerlessCheckpointOriginMalformed(entry);
            }

            (string Scheme, string Host, int Port) origin = OriginOf(url);
            if (!parsed.Contains(origin))
            {
                parsed.Add(origin);
            }
        }

        if (parsed.Count == 0)
        {
            ThrowHelper.ThrowServerlessCheckpointOriginsMissing();
        }

        return new ServerlessCheckpointOrigins([.. parsed]);
    }

    /// <summary>Reads the list from this process's environment, as a deployed function does at start-up.</summary>
    /// <returns>The list.</returns>
    /// <exception cref="FormatException">The setting is absent, empty or malformed, so the function must not start.</exception>
    public static ServerlessCheckpointOrigins FromProcessEnvironment()
        => Parse(Environment.GetEnvironmentVariable(SettingName));

    /// <summary>
    /// Checks that a deployer's function settings carry a well-formed list, so that a misconfigured runner is told at
    /// start-up and not by a function that refuses every invocation.
    /// </summary>
    /// <param name="functionSettings">The settings a deployer stamps onto the function.</param>
    /// <exception cref="FormatException">The settings carry no list, or a malformed one.</exception>
    public static void RequireIn(IReadOnlyDictionary<string, string>? functionSettings)
        => Parse(functionSettings is not null && functionSettings.TryGetValue(SettingName, out string? value) ? value : null);

    /// <summary>Determines whether a checkpoint URL's origin is on the list.</summary>
    /// <param name="checkpointUrl">The absolute checkpoint URL an invocation named.</param>
    /// <returns><see langword="true"/> when the function may use it.</returns>
    public bool Allows(Uri checkpointUrl)
    {
        ArgumentNullException.ThrowIfNull(checkpointUrl);
        if (!checkpointUrl.IsAbsoluteUri || !IsHttp(checkpointUrl) || checkpointUrl.UserInfo.Length != 0)
        {
            return false;
        }

        (string Scheme, string Host, int Port) origin = OriginOf(checkpointUrl);
        foreach ((string Scheme, string Host, int Port) allowed in this.origins)
        {
            if (allowed == origin)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHttp(Uri url) => url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps;

    // Uri lower-cases the scheme, and IdnHost is the lower-case ASCII form, so an ordinal comparison of the tuple is exact.
    private static (string Scheme, string Host, int Port) OriginOf(Uri url) => (url.Scheme, url.IdnHost.ToLowerInvariant(), url.Port);
}