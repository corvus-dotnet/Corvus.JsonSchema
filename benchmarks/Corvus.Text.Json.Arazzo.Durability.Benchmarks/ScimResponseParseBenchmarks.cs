// <copyright file="ScimResponseParseBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Directories.Scim;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using StjDocument = System.Text.Json.JsonDocument;
using StjElement = System.Text.Json.JsonElement;
using StjValueKind = System.Text.Json.JsonValueKind;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Locks the allocation floor of <see cref="ScimPrincipalDirectory"/>'s SCIM 2.0 ListResponse parse (design §16.5.4) —
/// the per-search hot operation. The production path reads the response <c>byte[]</c> in place with the Corvus
/// <c>Utf8JsonReader</c> (no STJ DOM, no second copy of the body), flattening SCIM's structured resources (the complex
/// <c>name</c>, multi-valued <c>emails</c>, the enterprise extension) into the record's attribute map; the naive
/// <c>JsonDocument</c> shape is the contrast.
/// </summary>
/// <remarks>
/// Honest finding (5-user page, ShortRun): the production path allocates ≈ 15.3 KB vs the naive DOM's ≈ 9.95 KB (≈ 1.54×)
/// — and the gap is NOT waste but generality. The adapter flattens the <em>full</em> attribute set (every core, extension,
/// and custom member) so a deployment mapper can key on any attribute it stamps; the <c>JsonDocument</c> baseline cheats
/// by hardcoding exactly the seven fields the bench mapper reads, which an adapter blind to the mapper cannot do. (It is
/// also faster despite allocating more, because it never builds the DOM.) The benchmark first reported ≈ 17.2 KB; skipping
/// SCIM's protocol-metadata common attributes — <c>schemas</c> (long URN strings) and <c>meta</c>, neither ever part of a
/// principal's identity — recovered ≈ 11% and is the one clearly-correct trim. The residual floor is the API-forced set
/// (the returned list + each escaping <see cref="ResolvedPrincipal"/>, the per-row attribute map the
/// <see cref="DirectoryRecord"/> contract requires, and the per-identity <see cref="SecurityTagSet"/> construction). The
/// benchmark's value is the regression guard: a change that re-flattens metadata, adds a body copy, or double-parses moves
/// the number.
/// </remarks>
public class ScimResponseParseBenchmarks
{
    private static readonly ScimResourceType UsersResource = ScimResourceType.Users;

    private static readonly byte[] UsersBody = Encoding.UTF8.GetBytes(
        """
        {"schemas":["urn:ietf:params:scim:api:messages:2.0:ListResponse"],"totalResults":5,"Resources":[
          {"schemas":["urn:ietf:params:scim:schemas:core:2.0:User","urn:ietf:params:scim:schemas:extension:enterprise:2.0:User"],"id":"id-alice","userName":"alice","name":{"givenName":"Alice","familyName":"Smith","formatted":"Alice Smith"},"displayName":"Alice Smith","emails":[{"value":"alice@acme.example","primary":true}],"active":true,"urn:ietf:params:scim:schemas:extension:enterprise:2.0:User":{"organization":"acme"}},
          {"schemas":["urn:ietf:params:scim:schemas:core:2.0:User","urn:ietf:params:scim:schemas:extension:enterprise:2.0:User"],"id":"id-albert","userName":"albert","name":{"givenName":"Albert","familyName":"Jones","formatted":"Albert Jones"},"displayName":"Albert Jones","emails":[{"value":"albert@acme.example","primary":true}],"active":true,"urn:ietf:params:scim:schemas:extension:enterprise:2.0:User":{"organization":"acme"}},
          {"schemas":["urn:ietf:params:scim:schemas:core:2.0:User","urn:ietf:params:scim:schemas:extension:enterprise:2.0:User"],"id":"id-bob","userName":"bob","name":{"givenName":"Bob","familyName":"Brown","formatted":"Bob Brown"},"displayName":"Bob Brown","emails":[{"value":"bob@globex.example","primary":true}],"active":true,"urn:ietf:params:scim:schemas:extension:enterprise:2.0:User":{"organization":"globex"}},
          {"schemas":["urn:ietf:params:scim:schemas:core:2.0:User","urn:ietf:params:scim:schemas:extension:enterprise:2.0:User"],"id":"id-carol","userName":"carol","name":{"givenName":"Carol","familyName":"White","formatted":"Carol White"},"displayName":"Carol White","emails":[{"value":"carol@acme.example","primary":true}],"active":true,"urn:ietf:params:scim:schemas:extension:enterprise:2.0:User":{"organization":"acme"}},
          {"schemas":["urn:ietf:params:scim:schemas:core:2.0:User","urn:ietf:params:scim:schemas:extension:enterprise:2.0:User"],"id":"id-dave","userName":"dave","name":{"givenName":"Dave","familyName":"Green","formatted":"Dave Green"},"displayName":"Dave Green","emails":[{"value":"dave@globex.example","primary":true}],"active":true,"urn:ietf:params:scim:schemas:extension:enterprise:2.0:User":{"organization":"globex"}}
        ]}
        """);

    // What the provider returns once the mapper declares its attributes (the projection seam): the SCIM `attributes`
    // request trims each resource to the value/label attributes + the declared one (plus the always-returned id/schemas),
    // so there is far less to flatten. Same five users as UsersBody, projected. Measured: parsing this allocates ≈ 9.0 KB
    // vs the full ≈ 15.3 KB — a ≈ 41% parse drop, now below the selective-DOM baseline — and that is only the local half of
    // the win; the larger saving is the smaller response the provider sends back over the wire.
    private static readonly byte[] ProjectedUsersBody = Encoding.UTF8.GetBytes(
        """
        {"schemas":["urn:ietf:params:scim:api:messages:2.0:ListResponse"],"totalResults":5,"Resources":[
          {"schemas":["urn:ietf:params:scim:schemas:core:2.0:User"],"id":"id-alice","userName":"alice","displayName":"Alice Smith","urn:ietf:params:scim:schemas:extension:enterprise:2.0:User":{"organization":"acme"}},
          {"schemas":["urn:ietf:params:scim:schemas:core:2.0:User"],"id":"id-albert","userName":"albert","displayName":"Albert Jones","urn:ietf:params:scim:schemas:extension:enterprise:2.0:User":{"organization":"acme"}},
          {"schemas":["urn:ietf:params:scim:schemas:core:2.0:User"],"id":"id-bob","userName":"bob","displayName":"Bob Brown","urn:ietf:params:scim:schemas:extension:enterprise:2.0:User":{"organization":"globex"}},
          {"schemas":["urn:ietf:params:scim:schemas:core:2.0:User"],"id":"id-carol","userName":"carol","displayName":"Carol White","urn:ietf:params:scim:schemas:extension:enterprise:2.0:User":{"organization":"acme"}},
          {"schemas":["urn:ietf:params:scim:schemas:core:2.0:User"],"id":"id-dave","userName":"dave","displayName":"Dave Green","urn:ietf:params:scim:schemas:extension:enterprise:2.0:User":{"organization":"globex"}}
        ]}
        """);

    private static readonly DirectoryPrincipalProjector Projector = new(
        DirectoryIdentityMapper.FromFunc(static record => record.Kind switch
        {
            GranteeKind.Person => new ResolvedPrincipal(GranteeKind.Person, record.Id, record.DisplayName, SecurityTagSet.FromTags([new SecurityTag("sys:tenant", record.Attribute("organization") ?? string.Empty), new SecurityTag("sys:sub", record.Id)])),
            _ => (ResolvedPrincipal?)null,
        }),
        "bench-issuer");

    /// <summary>The naive shape — materialise the whole ListResponse as a <see cref="StjDocument"/> DOM, then flatten + project.</summary>
    /// <returns>The resolved-principal count (prevents dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int ParseUsers_JsonDocumentNaive() => ProjectUsersViaDocument(UsersBody, 10);

    /// <summary>The production path — the Corvus reader over the borrowed (full) response, no DOM.</summary>
    /// <returns>The resolved-principal count.</returns>
    [Benchmark]
    public int ParseUsers() => ScimPrincipalDirectory.ProjectResponse(GranteeKind.Person, UsersResource, UsersBody, 10, Projector).Count;

    /// <summary>The production path over a PROJECTED response — the payload the provider returns once the mapper declares its attributes (the §16.5.4 projection seam). The wire saving lands here as fewer parse allocations.</summary>
    /// <returns>The resolved-principal count.</returns>
    [Benchmark]
    public int ParseUsers_Projected() => ScimPrincipalDirectory.ProjectResponse(GranteeKind.Person, UsersResource, ProjectedUsersBody, 10, Projector).Count;

    private static int ProjectUsersViaDocument(byte[] body, int limit)
    {
        var results = new List<ResolvedPrincipal>(limit);
        using StjDocument doc = StjDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("Resources", out StjElement resources) || resources.ValueKind != StjValueKind.Array)
        {
            return 0;
        }

        foreach (StjElement user in resources.EnumerateArray())
        {
            if (results.Count >= limit)
            {
                break;
            }

            if (!user.TryGetProperty("userName", out StjElement userNameElement) || userNameElement.GetString() is not { } userName)
            {
                continue;
            }

            var attributes = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase) { ["userName"] = [userName] };
            if (user.TryGetProperty("displayName", out StjElement display) && display.GetString() is { } displayName)
            {
                attributes["displayName"] = [displayName];
            }

            if (user.TryGetProperty("name", out StjElement name) && name.ValueKind == StjValueKind.Object)
            {
                foreach (var member in new[] { "givenName", "familyName", "formatted" })
                {
                    if (name.TryGetProperty(member, out StjElement v) && v.GetString() is { } scalar)
                    {
                        attributes[$"name.{member}"] = [scalar];
                    }
                }
            }

            if (user.TryGetProperty("emails", out StjElement emails) && emails.ValueKind == StjValueKind.Array)
            {
                var values = new List<string>();
                foreach (StjElement email in emails.EnumerateArray())
                {
                    if (email.TryGetProperty("value", out StjElement value) && value.GetString() is { } scalar)
                    {
                        values.Add(scalar);
                    }
                }

                if (values.Count > 0)
                {
                    attributes["emails"] = values;
                }
            }

            if (user.TryGetProperty("urn:ietf:params:scim:schemas:extension:enterprise:2.0:User", out StjElement enterprise) && enterprise.ValueKind == StjValueKind.Object
                && enterprise.TryGetProperty("organization", out StjElement organization) && organization.GetString() is { } org)
            {
                attributes["organization"] = [org];
            }

            if (Projector.Project(new DirectoryRecord(GranteeKind.Person, userName, attributes.TryGetValue("displayName", out var d) ? d[0] : userName, attributes, [])) is { } principal)
            {
                results.Add(principal);
            }
        }

        return results.Count;
    }
}