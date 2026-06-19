// <copyright file="GraphResponseParseBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Directories.EntraId;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using StjDocument = System.Text.Json.JsonDocument;
using StjElement = System.Text.Json.JsonElement;
using StjValueKind = System.Text.Json.JsonValueKind;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Locks the allocation floor of <see cref="EntraIdPrincipalDirectory"/>'s Microsoft Graph collection parse (design
/// §16.5.4) — the per-search hot operation once the access token is cached. The production path reads the response
/// <c>byte[]</c> in place with the Corvus <c>Utf8JsonReader</c> (no STJ DOM, no second copy of the body); the naive
/// <c>JsonDocument</c> shape is the contrast. Measured (5-user page, ShortRun): production ≈ 8.35 KB vs the naive DOM
/// ≈ 6.04 KB (≈ 1.38×) — the gap is generality, not waste (the adapter surfaces every entity attribute, e.g. the immutable
/// <c>id</c> a mapper may want for <c>sys:sub</c>, where the baseline hardcodes the three fields the bench mapper reads),
/// and it is smaller than SCIM's because Graph entities are flat. No DOM, no body copy; the benchmark is the regression
/// guard — a change that adds a copy, double-parse, or owned DOM moves the number.
/// </summary>
public class GraphResponseParseBenchmarks
{
    private static readonly EntraIdResource UsersResource = EntraIdResource.Users;

    private static readonly byte[] UsersBody = Encoding.UTF8.GetBytes(
        """
        {"@odata.context":"https://graph.microsoft.com/v1.0/$metadata#users","value":[
          {"id":"id-alice","mailNickname":"alice","displayName":"Alice Smith","department":"acme"},
          {"id":"id-albert","mailNickname":"albert","displayName":"Albert Jones","department":"acme"},
          {"id":"id-bob","mailNickname":"bob","displayName":"Bob Brown","department":"globex"},
          {"id":"id-carol","mailNickname":"carol","displayName":"Carol White","department":"acme"},
          {"id":"id-dave","mailNickname":"dave","displayName":"Dave Green","department":"globex"}
        ]}
        """);

    private static readonly DirectoryPrincipalProjector Projector = new(
        DirectoryIdentityMapper.FromFunc(static record => record.Kind switch
        {
            GranteeKind.Person => new ResolvedPrincipal(GranteeKind.Person, record.Id, record.DisplayName, SecurityTagSet.FromTags([new SecurityTag("sys:tenant", record.Attribute("department") ?? string.Empty), new SecurityTag("sys:sub", record.Id)])),
            _ => (ResolvedPrincipal?)null,
        }),
        "bench-issuer");

    /// <summary>The naive shape — materialise the whole collection as a <see cref="StjDocument"/> DOM, then project.</summary>
    /// <returns>The resolved-principal count (prevents dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int ParseUsers_JsonDocumentNaive() => ProjectUsersViaDocument(UsersBody, 10);

    /// <summary>The production path — the Corvus reader over the borrowed response, no DOM.</summary>
    /// <returns>The resolved-principal count.</returns>
    [Benchmark]
    public int ParseUsers() => EntraIdPrincipalDirectory.ProjectResponse(GranteeKind.Person, UsersResource, UsersBody, 10, Projector).Count;

    private static int ProjectUsersViaDocument(byte[] body, int limit)
    {
        var results = new List<ResolvedPrincipal>(limit);
        using StjDocument doc = StjDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("value", out StjElement value) || value.ValueKind != StjValueKind.Array)
        {
            return 0;
        }

        foreach (StjElement user in value.EnumerateArray())
        {
            if (results.Count >= limit)
            {
                break;
            }

            if (!user.TryGetProperty("mailNickname", out StjElement nicknameElement) || nicknameElement.GetString() is not { } mailNickname)
            {
                continue;
            }

            var attributes = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase) { ["mailNickname"] = [mailNickname] };
            if (user.TryGetProperty("displayName", out StjElement display) && display.GetString() is { } displayName)
            {
                attributes["displayName"] = [displayName];
            }

            if (user.TryGetProperty("department", out StjElement department) && department.GetString() is { } dept)
            {
                attributes["department"] = [dept];
            }

            if (Projector.Project(new DirectoryRecord(GranteeKind.Person, mailNickname, attributes.TryGetValue("displayName", out var d) ? d[0] : mailNickname, attributes, [])) is { } principal)
            {
                results.Add(principal);
            }
        }

        return results.Count;
    }
}