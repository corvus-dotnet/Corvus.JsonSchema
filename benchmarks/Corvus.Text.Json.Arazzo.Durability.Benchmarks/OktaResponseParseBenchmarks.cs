// <copyright file="OktaResponseParseBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Directories.Okta;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using StjDocument = System.Text.Json.JsonDocument;
using StjElement = System.Text.Json.JsonElement;
using StjValueKind = System.Text.Json.JsonValueKind;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Locks the allocation floor of <see cref="OktaPrincipalDirectory"/>'s Okta list parse (design §16.5.4) — the per-search
/// hot operation. The production path reads the response <c>byte[]</c> in place with the Corvus <c>Utf8JsonReader</c> (no
/// STJ DOM, no second copy), flattening the user's <c>profile</c> object; the naive <c>JsonDocument</c> shape is the
/// contrast. Okta has no server-side field projection, so the projection seam is parse-side: with a declaring mapper the
/// same response materialises fewer attributes — shown by <c>ParseUsers_Projected</c> over the same body.
/// </summary>
/// <remarks>
/// Measured (5-user page, ShortRun): full ≈ 17.95 KB, projected ≈ 11.21 KB — a ≈ 38% parse drop from the declaring mapper
/// (the keep-check precedes <c>GetString</c>, so a filtered attribute never materialises its value string). Both sit above
/// the naive DOM's ≈ 6.04 KB because parse-side projection still builds the dotted composite key (<c>profile.firstName</c>)
/// for every attribute to test the filter, where the DOM navigates without it. That residual is the whole reason wire-side
/// projection (SCIM <c>attributes</c>, Graph <c>$select</c>, LDAP's search list) beats parse-side: it never sends, parses,
/// or keys the unread attributes at all. Okta exposes no field-select, so parse-side is the best available — the seam still
/// pays, just less than where the provider can project. <see cref="ParseUsers_Span"/> is the bytes-to-bytes path (a span
/// mapper): ≈ 1.83 KB — ≈ 0.10× of the full string path — by capturing only the value + the declared attribute (by leaf)
/// as UTF-8 and writing the identity straight into a pooled buffer; neither the flatten dictionary nor any per-value string
/// is built. The biggest drop of the HTTP adapters, since Okta's string path flattened the deepest profile. The benchmark
/// is the regression guard.
/// </remarks>
public class OktaResponseParseBenchmarks
{
    private static readonly OktaResource UsersResource = OktaResource.Users;

    // A representative Okta users page — bare array, profile-nested, with the attributes a real org carries (only some of
    // which any one mapper reads).
    private static readonly byte[] UsersBody = Encoding.UTF8.GetBytes(
        """
        [
          {"id":"id-alice","status":"ACTIVE","created":"2024-01-01T00:00:00.000Z","profile":{"login":"alice","firstName":"Alice","lastName":"Smith","displayName":"Alice Smith","email":"alice@acme.example","secondEmail":"alice@personal.example","mobilePhone":"555-0001","department":"acme","title":"Engineer"}},
          {"id":"id-albert","status":"ACTIVE","created":"2024-01-01T00:00:00.000Z","profile":{"login":"albert","firstName":"Albert","lastName":"Jones","displayName":"Albert Jones","email":"albert@acme.example","secondEmail":"albert@personal.example","mobilePhone":"555-0002","department":"acme","title":"Manager"}},
          {"id":"id-bob","status":"ACTIVE","created":"2024-01-01T00:00:00.000Z","profile":{"login":"bob","firstName":"Bob","lastName":"Brown","displayName":"Bob Brown","email":"bob@globex.example","secondEmail":"bob@personal.example","mobilePhone":"555-0003","department":"globex","title":"Analyst"}},
          {"id":"id-carol","status":"ACTIVE","created":"2024-01-01T00:00:00.000Z","profile":{"login":"carol","firstName":"Carol","lastName":"White","displayName":"Carol White","email":"carol@acme.example","secondEmail":"carol@personal.example","mobilePhone":"555-0004","department":"acme","title":"Engineer"}},
          {"id":"id-dave","status":"ACTIVE","created":"2024-01-01T00:00:00.000Z","profile":{"login":"dave","firstName":"Dave","lastName":"Green","displayName":"Dave Green","email":"dave@globex.example","secondEmail":"dave@personal.example","mobilePhone":"555-0005","department":"globex","title":"Analyst"}}
        ]
        """);

    private static readonly DirectoryPrincipalProjector GeneralProjector = new(DirectoryIdentityMapper.FromFunc(Map), "bench-issuer");

    // A declaring mapper: it reads only profile.department (sys:tenant) and profile.login (sys:sub, surfaced as the value).
    private static readonly DirectoryPrincipalProjector ProjectingProjector = new(
        DirectoryIdentityMapper.FromFunc(["profile.department"], Map),
        "bench-issuer");

    // The span (bytes-to-bytes) mapper — writes the identity from the record's UTF-8 spans (department/login by leaf).
    private static readonly DirectoryPrincipalProjector SpanProjector = new(
        DirectorySpanIdentityMapper.FromIdentity(
            ["profile.department"],
            static (DirectoryRecordView record, ref IdentityBuilder identity) =>
            {
                identity.Add("sys:tenant"u8, record.AttributeUtf8("department"u8));
                identity.Add("sys:sub"u8, record.ValueUtf8);
                return true;
            }),
        "bench-issuer");

    /// <summary>The naive shape — materialise the whole array as a <see cref="StjDocument"/> DOM, then flatten + project.</summary>
    /// <returns>The resolved-principal count (prevents dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int ParseUsers_JsonDocumentNaive() => ProjectUsersViaDocument(UsersBody, 10);

    /// <summary>The production path — the Corvus reader over the borrowed (full) response, surfacing every profile attribute.</summary>
    /// <returns>The resolved-principal count.</returns>
    [Benchmark]
    public int ParseUsers() => OktaPrincipalDirectory.ProjectResponse(GranteeKind.Person, UsersResource, UsersBody, 10, GeneralProjector).Count;

    /// <summary>The production path with a declaring mapper — Okta has no wire projection, so the SAME response materialises only the declared + value/label attributes (the §16.5.4 seam, parse-side).</summary>
    /// <returns>The resolved-principal count.</returns>
    [Benchmark]
    public int ParseUsers_Projected() => OktaPrincipalDirectory.ProjectResponse(GranteeKind.Person, UsersResource, UsersBody, 10, ProjectingProjector).Count;

    /// <summary>The bytes-to-bytes path — a span mapper captures only the value + the declared attribute (by leaf) and writes the identity straight from spans, over the same full response.</summary>
    /// <returns>The resolved-principal count.</returns>
    [Benchmark]
    public int ParseUsers_Span() => OktaPrincipalDirectory.ProjectResponseSpan(GranteeKind.Person, UsersResource, UsersBody, 10, SpanProjector).Count;

    private static ResolvedPrincipal? Map(DirectoryRecord record)
        => new(GranteeKind.Person, record.Id, record.DisplayName, SecurityTagSet.FromTags([new SecurityTag("sys:tenant", record.Attribute("profile.department") ?? string.Empty), new SecurityTag("sys:sub", record.Id)]));

    private static int ProjectUsersViaDocument(byte[] body, int limit)
    {
        var results = new List<ResolvedPrincipal>(limit);
        using StjDocument doc = StjDocument.Parse(body);
        if (doc.RootElement.ValueKind != StjValueKind.Array)
        {
            return 0;
        }

        foreach (StjElement user in doc.RootElement.EnumerateArray())
        {
            if (results.Count >= limit)
            {
                break;
            }

            if (!user.TryGetProperty("profile", out StjElement profile) || profile.ValueKind != StjValueKind.Object
                || !profile.TryGetProperty("login", out StjElement loginElement) || loginElement.GetString() is not { } login)
            {
                continue;
            }

            var attributes = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase) { ["profile.login"] = [login] };
            if (profile.TryGetProperty("displayName", out StjElement display) && display.GetString() is { } displayName)
            {
                attributes["profile.displayName"] = [displayName];
            }

            if (profile.TryGetProperty("department", out StjElement department) && department.GetString() is { } dept)
            {
                attributes["profile.department"] = [dept];
            }

            if (GeneralProjector.Project(new DirectoryRecord(GranteeKind.Person, login, attributes.TryGetValue("profile.displayName", out var d) ? d[0] : login, attributes, [])) is { } principal)
            {
                results.Add(principal);
            }
        }

        return results.Count;
    }
}