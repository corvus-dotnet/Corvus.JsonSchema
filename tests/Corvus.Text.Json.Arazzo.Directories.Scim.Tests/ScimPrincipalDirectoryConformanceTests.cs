// <copyright file="ScimPrincipalDirectoryConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Directories.Conformance;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Directories.Scim.Tests;

/// <summary>
/// Runs the shared <see cref="PrincipalDirectoryConformance"/> suite against <see cref="ScimPrincipalDirectory"/> over a
/// mock SCIM 2.0 service provider (a <see cref="StubHttpMessageHandler"/>, no network), seeded with the
/// <see cref="DirectoryFixture"/> principals as SCIM resources — users carrying the enterprise extension's
/// <c>organization</c> (the mapper's <c>sys:tenant</c> source), groups, and a custom <c>Roles</c> resource. The mock
/// honours the SCIM <c>sw</c> filter and <c>count</c>, so the adapter's real request-building and response-parsing are
/// under test; only the wire is faked.
/// </summary>
[TestClass]
public sealed class ScimPrincipalDirectoryConformanceTests : PrincipalDirectoryConformance
{
    private const string Token = "scim-bearer-token";
    private static readonly Uri BaseUrl = new("https://scim.example.org/scim/v2");

    protected override ValueTask<IPrincipalDirectory> CreateAsync()
    {
        var options = new ScimDirectoryOptions
        {
            Issuer = DirectoryFixture.Issuer,
            BaseUrl = BaseUrl,
            Authentication = new ScimBearerToken(DirectoryCredential.Parse("env://SCIM_TOKEN")),
            Kinds = new Dictionary<GranteeKind, ScimResourceType>
            {
                [GranteeKind.Person] = ScimResourceType.Users,
                [GranteeKind.Team] = ScimResourceType.Groups,
                [GranteeKind.Role] = new("Roles", "displayName", "displayName"),
            },
        };

        // The deployment mapper — a SPAN mapper exercising the bytes-to-bytes path: persons take sys:tenant from the
        // enterprise extension's `organization` (declared as its SCIM path for the wire $select; read by its leaf) and
        // sys:sub from the userName (the searchable value); teams/roles from the value (displayName). No attribute string.
        var mapper = DirectorySpanIdentityMapper.FromIdentity(
            ["urn:ietf:params:scim:schemas:extension:enterprise:2.0:User:organization"],
            static (DirectoryRecordView record, ref IdentityBuilder identity) =>
            {
                switch (record.Kind)
                {
                    case GranteeKind.Person:
                        identity.Add("sys:tenant"u8, record.AttributeUtf8("organization"u8));
                        identity.Add("sys:sub"u8, record.ValueUtf8);
                        return true;
                    case GranteeKind.Team:
                        identity.Add("sys:team"u8, record.ValueUtf8);
                        return true;
                    case GranteeKind.Role:
                        identity.Add("sys:role"u8, record.ValueUtf8);
                        return true;
                    default:
                        return false;
                }
            });

        var httpClient = new HttpClient(new StubHttpMessageHandler(ScimMockBackend.Respond));
        return new ValueTask<IPrincipalDirectory>(new ScimPrincipalDirectory(options, new FixedSecretResolver(Token), mapper, httpClient));
    }

    [TestMethod]
    public async Task A_declaring_mapper_projects_the_request_and_resolves_a_projected_response()
    {
        const string OrgPath = "urn:ietf:params:scim:schemas:extension:enterprise:2.0:User:organization";

        // A provider that returns ONLY what was asked for (a projected resource — no id/name/emails/active/schemas).
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            captured = request;
            return StubHttpMessageHandler.Json(
                """{"schemas":["urn:ietf:params:scim:api:messages:2.0:ListResponse"],"totalResults":1,"Resources":[{"userName":"alice","displayName":"Alice Smith","urn:ietf:params:scim:schemas:extension:enterprise:2.0:User":{"organization":"acme"}}]}""",
                "application/scim+json");
        });

        // A mapper that declares the one attribute it reads (in SCIM path notation), beyond the value/label the adapter needs.
        var mapper = DirectoryIdentityMapper.FromFunc(
            [OrgPath],
            record => new ResolvedPrincipal(GranteeKind.Person, record.Id, record.DisplayName, DirectoryFixture.Identity(("sys:tenant", record.Attribute("organization") ?? string.Empty), ("sys:sub", record.Id))));

        var options = new ScimDirectoryOptions
        {
            Issuer = DirectoryFixture.Issuer,
            BaseUrl = BaseUrl,
            Authentication = new ScimBearerToken(DirectoryCredential.Parse("env://SCIM_TOKEN")),
            Kinds = new Dictionary<GranteeKind, ScimResourceType> { [GranteeKind.Person] = ScimResourceType.Users },
        };

        using var directory = new ScimPrincipalDirectory(options, new FixedSecretResolver(Token), mapper, new HttpClient(handler));
        ResolvedPrincipal alice = (await directory.SearchAsync(GranteeKind.Person, "alice", 1, default)).Single();

        // The request asked the provider (server-side) for exactly the declared attribute plus the value/label attributes.
        captured.ShouldNotBeNull();
        string requestUrl = Uri.UnescapeDataString(captured!.RequestUri!.ToString());
        requestUrl.ShouldContain("attributes=");
        requestUrl.ShouldContain("userName");
        requestUrl.ShouldContain("displayName");
        requestUrl.ShouldContain(OrgPath);

        // ...and the projected response (only those attributes) still resolves to the exact identity.
        alice.Value.ShouldBe("alice");
        alice.Label.ShouldBe("Alice Smith");
        alice.Identity.ToList().ShouldContain(new SecurityTag("sys:tenant", "acme"));
        alice.Identity.ToList().ShouldContain(new SecurityTag("sys:sub", "alice"));
    }

    // A minimal SCIM 2.0 service provider: serves the fixture principals as SCIM resources, honouring `<attr> sw "x"` and
    // `count`. Users carry the enterprise extension organization (= tenant); groups and roles are name-only resources.
    private static class ScimMockBackend
    {
        private static readonly (string UserName, string Given, string Family, string Tenant)[] Users =
        [
            ("alice", "Alice", "Smith", "acme"),
            ("albert", "Albert", "Jones", "acme"),
            ("bob", "Bob", "Brown", "globex"),
        ];

        private static readonly string[] Groups = ["payments", "billing"];
        private static readonly string[] Roles = ["workflow-admin", "viewer"];

        public static HttpResponseMessage Respond(HttpRequestMessage request)
        {
            // Prove the adapter resolved and presented the bearer token (the §13 SecretRef path).
            if (request.Headers.Authorization is not { Scheme: "Bearer", Parameter: { Length: > 0 } })
            {
                return StubHttpMessageHandler.Json("""{"detail":"missing bearer token"}""", "application/scim+json", HttpStatusCode.Unauthorized);
            }

            Uri uri = request.RequestUri!;
            string resource = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)[^1];
            (string? filterAttribute, string? prefix) = ParseFilter(uri.Query);
            int count = ParseCount(uri.Query);

            IEnumerable<string> resources = resource switch
            {
                "Users" => Users.Where(u => Matches(filterAttribute, prefix, "userName", u.UserName)).Select(u => UserJson(u.UserName, u.Given, u.Family, u.Tenant)),
                "Groups" => Groups.Where(g => Matches(filterAttribute, prefix, "displayName", g)).Select(g => NamedJson(g, "Group")),
                "Roles" => Roles.Where(r => Matches(filterAttribute, prefix, "displayName", r)).Select(r => NamedJson(r, "Role")),
                _ => [],
            };

            return StubHttpMessageHandler.Json(ListResponse(resources.Take(count)), "application/scim+json");
        }

        private static bool Matches(string? filterAttribute, string? prefix, string attribute, string value)
            => prefix is null || (string.Equals(filterAttribute, attribute, StringComparison.OrdinalIgnoreCase) && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        // Parses `<attr> sw "x"` from the (URL-encoded) query into (attribute, prefix); (null, null) when absent.
        private static (string? Attribute, string? Prefix) ParseFilter(string query)
        {
            string? filter = QueryValue(query, "filter");
            if (filter is null)
            {
                return (null, null);
            }

            string[] parts = filter.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3 || !string.Equals(parts[1], "sw", StringComparison.OrdinalIgnoreCase))
            {
                return (null, null);
            }

            string literal = parts[2].Trim('"').Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);
            return (parts[0], literal);
        }

        private static int ParseCount(string query) => int.TryParse(QueryValue(query, "count"), out int count) && count > 0 ? count : int.MaxValue;

        private static string? QueryValue(string query, string key)
        {
            foreach (string pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = pair.IndexOf('=', StringComparison.Ordinal);
                if (eq > 0 && string.Equals(pair[..eq], key, StringComparison.Ordinal))
                {
                    return Uri.UnescapeDataString(pair[(eq + 1)..]);
                }
            }

            return null;
        }

        private static string UserJson(string userName, string given, string family, string tenant)
            => $$$"""
                {"schemas":["urn:ietf:params:scim:schemas:core:2.0:User","urn:ietf:params:scim:schemas:extension:enterprise:2.0:User"],"id":"id-{{{userName}}}","userName":"{{{userName}}}","name":{"givenName":"{{{given}}}","familyName":"{{{family}}}","formatted":"{{{given}}} {{{family}}}"},"displayName":"{{{given}}} {{{family}}}","emails":[{"value":"{{{userName}}}@{{{tenant}}}.example","primary":true}],"active":true,"urn:ietf:params:scim:schemas:extension:enterprise:2.0:User":{"organization":"{{{tenant}}}"}}
                """;

        private static string NamedJson(string displayName, string kind)
            => $$"""{"schemas":["urn:ietf:params:scim:schemas:core:2.0:{{kind}}"],"id":"id-{{displayName}}","displayName":"{{displayName}}"}""";

        private static string ListResponse(IEnumerable<string> resources)
        {
            string[] items = [.. resources];
            return $$"""{"schemas":["urn:ietf:params:scim:api:messages:2.0:ListResponse"],"totalResults":{{items.Length}},"Resources":[{{string.Join(",", items)}}]}""";
        }
    }

    // The conformance harness exercises search, not the secret store: a fixed resolver returns the bearer token for any
    // reference (the production resolver chain is exercised by the §13 secret-resolver tests).
    private sealed class FixedSecretResolver(string secret) : ISecretResolver
    {
        public bool CanResolve(SecretScheme scheme) => true;

        public ValueTask<SecretMaterial> ResolveAsync(SecretRef reference, CancellationToken cancellationToken)
            => new(SecretMaterial.FromString(secret));
    }
}