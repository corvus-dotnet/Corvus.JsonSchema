// <copyright file="KeycloakResponseParseBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Directories.Keycloak;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using StjDocument = System.Text.Json.JsonDocument;
using StjElement = System.Text.Json.JsonElement;
using StjProperty = System.Text.Json.JsonProperty;
using StjValueKind = System.Text.Json.JsonValueKind;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Locks the allocation floor of <see cref="KeycloakPrincipalDirectory"/>'s Admin-response parse (design §16.5.4) — the
/// per-search hot operation once the admin token is cached. The production path reads the response <c>byte[]</c> in place
/// with the Corvus <c>Utf8JsonReader</c> (no STJ DOM, no second copy of the body); the naive <c>JsonDocument</c> shape is
/// the contrast.
/// </summary>
/// <remarks>
/// Honest finding (5-user page, ShortRun): the two parse strategies allocate within 1% of each other (Corvus ≈ 8.75 KB
/// vs DOM ≈ 8.82 KB) — <c>JsonDocument</c> POOLS its DOM internally, so the parse strategy is NOT the dominant allocator;
/// the Corvus path's win is ~20% time and shedding the STJ dependency the production assembly forbids, not bytes. The
/// ~8.75 KB floor is the API-forced residual: the returned list + each escaping <see cref="ResolvedPrincipal"/>, the
/// short-lived per-row <c>DirectoryRecord</c> + attribute scratch the mapper contract forces, and chiefly the per-identity
/// <see cref="SecurityTagSet"/> construction (<c>FromTags</c> — the still-unpooled identity build tracked by the wider
/// durability-alloc campaign, shared across the control plane, not specific to this adapter). The benchmark's value is the
/// regression guard: a change that adds a body copy, a double-parse, or an owned (non-pooled) DOM moves the number. The
/// roles case additionally locks the mapper-drop path (Keycloak's built-in realm roles project to no <c>sys:</c> grant).
/// </remarks>
public class KeycloakResponseParseBenchmarks
{
    // A representative Admin "list users" page (briefRepresentation=false, so attributes are present) and a "list realm
    // roles" page that mixes the two recognised roles with Keycloak's built-in ones (which the mapper drops).
    private static readonly byte[] UsersBody = Encoding.UTF8.GetBytes(
        """
        [
          {"id":"11111111","username":"alice","enabled":true,"firstName":"Alice","lastName":"Smith","email":"alice@acme.example","attributes":{"tenant":["acme"]}},
          {"id":"22222222","username":"albert","enabled":true,"firstName":"Albert","lastName":"Jones","email":"albert@acme.example","attributes":{"tenant":["acme"]}},
          {"id":"33333333","username":"bob","enabled":true,"firstName":"Bob","lastName":"Brown","email":"bob@globex.example","attributes":{"tenant":["globex"]}},
          {"id":"44444444","username":"carol","enabled":true,"firstName":"Carol","lastName":"White","email":"carol@acme.example","attributes":{"tenant":["acme"]}},
          {"id":"55555555","username":"dave","enabled":true,"firstName":"Dave","lastName":"Green","email":"dave@globex.example","attributes":{"tenant":["globex"]}}
        ]
        """);

    private static readonly byte[] RolesBody = Encoding.UTF8.GetBytes(
        """
        [
          {"id":"r1","name":"workflow-admin","composite":false,"clientRole":false,"containerId":"corvus"},
          {"id":"r2","name":"viewer","composite":false,"clientRole":false,"containerId":"corvus"},
          {"id":"r3","name":"offline_access","composite":false,"clientRole":false,"containerId":"corvus"},
          {"id":"r4","name":"uma_authorization","composite":false,"clientRole":false,"containerId":"corvus"},
          {"id":"r5","name":"default-roles-corvus","composite":true,"clientRole":false,"containerId":"corvus"}
        ]
        """);

    // The deployment projector: the same shape the conformance test wires (the adapter stamps sys:iss on top, so the
    // mapper omits it). Built once — it is configuration, not per-search work.
    private static readonly DirectoryPrincipalProjector Projector = new(
        DirectoryIdentityMapper.FromFunc(static record => record.Kind switch
        {
            GranteeKind.Person => new ResolvedPrincipal(GranteeKind.Person, record.Id, record.DisplayName, SecurityTagSet.FromTags([new SecurityTag("sys:tenant", record.Attribute("tenant") ?? string.Empty), new SecurityTag("sys:sub", record.Id)])),
            GranteeKind.Team => new ResolvedPrincipal(GranteeKind.Team, record.Id, record.DisplayName, SecurityTagSet.FromTags([new SecurityTag("sys:team", record.Id)])),
            GranteeKind.Role when record.Id is "workflow-admin" or "viewer" => new ResolvedPrincipal(GranteeKind.Role, record.Id, record.DisplayName, SecurityTagSet.FromTags([new SecurityTag("sys:role", record.Id)])),
            _ => (ResolvedPrincipal?)null,
        }),
        "bench-issuer");

    /// <summary>The naive shape — materialise the whole response as a <see cref="JsonDocument"/> DOM, then project. The A/B reference the production path must stay below.</summary>
    /// <returns>The resolved-principal count (prevents dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int ParseUsers_JsonDocumentNaive() => ProjectUsersViaDocument(UsersBody, 10);

    /// <summary>The production path — the Corvus reader over the borrowed response, no DOM.</summary>
    /// <returns>The resolved-principal count.</returns>
    [Benchmark]
    public int ParseUsers() => KeycloakPrincipalDirectory.ProjectResponse(GranteeKind.Person, KeycloakResource.Users, UsersBody, 10, Projector).Count;

    /// <summary>The production roles path — locks the mapper-drop floor (three of five roles project to null).</summary>
    /// <returns>The resolved-principal count (2 — the built-in roles are dropped).</returns>
    [Benchmark]
    public int ParseRoles() => KeycloakPrincipalDirectory.ProjectResponse(GranteeKind.Role, KeycloakResource.Roles, RolesBody, 10, Projector).Count;

    // The naive baseline: a JsonDocument DOM + property lookups, reproducing the adapter's user projection so only the
    // parse strategy (DOM vs in-place reader) differs. The escaping records are identical to the production path.
    private static int ProjectUsersViaDocument(byte[] body, int limit)
    {
        var results = new List<ResolvedPrincipal>(limit);
        using StjDocument doc = StjDocument.Parse(body);
        foreach (StjElement user in doc.RootElement.EnumerateArray())
        {
            if (results.Count >= limit)
            {
                break;
            }

            if (!user.TryGetProperty("username", out StjElement usernameElement) || usernameElement.GetString() is not { } username)
            {
                continue;
            }

            string? first = user.TryGetProperty("firstName", out StjElement f) ? f.GetString() : null;
            string? last = user.TryGetProperty("lastName", out StjElement l) ? l.GetString() : null;
            string? email = user.TryGetProperty("email", out StjElement e) ? e.GetString() : null;
            string? id = user.TryGetProperty("id", out StjElement i) ? i.GetString() : null;

            var attributes = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            if (user.TryGetProperty("attributes", out StjElement attrs) && attrs.ValueKind == StjValueKind.Object)
            {
                foreach (StjProperty property in attrs.EnumerateObject())
                {
                    if (property.Value.ValueKind == StjValueKind.Array)
                    {
                        var values = new List<string>();
                        foreach (StjElement value in property.Value.EnumerateArray())
                        {
                            if (value.ValueKind == StjValueKind.String && value.GetString() is { } scalar)
                            {
                                values.Add(scalar);
                            }
                        }

                        attributes[property.Name] = values;
                    }
                }
            }

            if (id is not null)
            {
                attributes["id"] = [id];
            }

            if (email is not null)
            {
                attributes["email"] = [email];
            }

            if (first is not null)
            {
                attributes["firstName"] = [first];
            }

            if (last is not null)
            {
                attributes["lastName"] = [last];
            }

            string? display = (first, last) switch
            {
                (not null, not null) => $"{first} {last}",
                (not null, null) => first,
                (null, not null) => last,
                _ => email,
            };

            if (Projector.Project(new DirectoryRecord(GranteeKind.Person, username, display, attributes, [])) is { } principal)
            {
                results.Add(principal);
            }
        }

        return results.Count;
    }
}