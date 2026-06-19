// <copyright file="OktaPrincipalDirectoryConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Directories.Conformance;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Directories.Okta.Tests;

/// <summary>
/// Runs the shared <see cref="PrincipalDirectoryConformance"/> suite against <see cref="OktaPrincipalDirectory"/> over a
/// mock Okta Management API (a <see cref="StubHttpMessageHandler"/>, no network), seeded with the
/// <see cref="DirectoryFixture"/> principals — users carrying <c>profile.department</c> (the mapper's <c>sys:tenant</c>
/// source) returned as a bare array, profile-named groups, and roles wrapped in a <c>roles</c> property. The mock honours
/// the Okta <c>search</c> <c>sw</c> expression and <c>limit</c>, so the adapter's real request-building and
/// response-parsing are under test.
/// </summary>
[TestClass]
public sealed class OktaPrincipalDirectoryConformanceTests : PrincipalDirectoryConformance
{
    private const string Token = "ssws-api-token";
    private static readonly Uri BaseUrl = new("https://example.okta.com");

    protected override ValueTask<IPrincipalDirectory> CreateAsync()
    {
        var options = new OktaDirectoryOptions
        {
            Issuer = DirectoryFixture.Issuer,
            BaseUrl = BaseUrl,
            Authentication = new OktaApiToken(DirectoryCredential.Parse("env://OKTA_TOKEN")),
            Kinds = new Dictionary<GranteeKind, OktaResource>
            {
                [GranteeKind.Person] = OktaResource.Users,
                [GranteeKind.Team] = OktaResource.Groups,
                [GranteeKind.Role] = new("iam/roles", "label", "label", "roles"),
            },
        };

        // The deployment mapper — a SPAN mapper exercising the bytes-to-bytes path: persons take sys:tenant from
        // profile.department (declared, read by its leaf) and sys:sub from the searchable value (profile.login); teams from
        // the value (profile.name); roles from the value (label). No attribute string.
        var mapper = DirectorySpanIdentityMapper.FromIdentity(
            ["profile.department"],
            static (DirectoryRecordView record, ref IdentityBuilder identity) =>
            {
                switch (record.Kind)
                {
                    case GranteeKind.Person:
                        identity.Add("sys:tenant"u8, record.AttributeUtf8("department"u8));
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

        var httpClient = new HttpClient(new StubHttpMessageHandler(OktaMockBackend.Respond));
        return new ValueTask<IPrincipalDirectory>(new OktaPrincipalDirectory(options, new FixedSecretResolver(Token), mapper, httpClient));
    }

    // A minimal Okta Management API: serves the fixture principals, honouring `<attr> sw "x"` and `limit`. Users / groups
    // are bare arrays with profile-nested attributes; roles are wrapped in a `roles` property with a top-level label.
    private static class OktaMockBackend
    {
        private static readonly (string Login, string Display, string Tenant)[] Users =
        [
            ("alice", "Alice Smith", "acme"),
            ("albert", "Albert Jones", "acme"),
            ("bob", "Bob Brown", "globex"),
        ];

        private static readonly string[] Groups = ["payments", "billing"];
        private static readonly string[] Roles = ["workflow-admin", "viewer"];

        public static HttpResponseMessage Respond(HttpRequestMessage request)
        {
            // Prove the adapter resolved and presented the SSWS token.
            if (request.Headers.Authorization is not { Scheme: "SSWS", Parameter: { Length: > 0 } })
            {
                return StubHttpMessageHandler.Json("""{"errorCode":"E0000011"}""", "application/json", HttpStatusCode.Unauthorized);
            }

            Uri uri = request.RequestUri!;
            string path = uri.AbsolutePath;
            (string? filterAttribute, string? prefix) = ParseSearch(uri.Query);
            int limit = ParseLimit(uri.Query);

            if (path.EndsWith("/users", StringComparison.Ordinal))
            {
                return StubHttpMessageHandler.Json(Array(Users.Where(u => Matches(filterAttribute, prefix, "profile.login", u.Login)).Select(u => UserJson(u.Login, u.Display, u.Tenant)).Take(limit)));
            }

            if (path.EndsWith("/groups", StringComparison.Ordinal))
            {
                return StubHttpMessageHandler.Json(Array(Groups.Where(g => Matches(filterAttribute, prefix, "profile.name", g)).Select(GroupJson).Take(limit)));
            }

            if (path.EndsWith("/iam/roles", StringComparison.Ordinal))
            {
                string roles = Array(Roles.Where(r => Matches(filterAttribute, prefix, "label", r)).Select(RoleJson).Take(limit));
                return StubHttpMessageHandler.Json($$"""{"roles":{{roles}}}""");
            }

            return StubHttpMessageHandler.Json("[]");
        }

        private static bool Matches(string? filterAttribute, string? prefix, string attribute, string value)
            => prefix is null || (string.Equals(filterAttribute, attribute, StringComparison.OrdinalIgnoreCase) && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        // Parses `<attr> sw "x"` from the (URL-encoded) `search` query into (attribute, prefix); (null, null) when absent.
        private static (string? Attribute, string? Prefix) ParseSearch(string query)
        {
            string? search = QueryValue(query, "search");
            if (search is null)
            {
                return (null, null);
            }

            string[] parts = search.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3 || !string.Equals(parts[1], "sw", StringComparison.OrdinalIgnoreCase))
            {
                return (null, null);
            }

            string literal = parts[2].Trim('"').Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);
            return (parts[0], literal);
        }

        private static int ParseLimit(string query) => int.TryParse(QueryValue(query, "limit"), out int limit) && limit > 0 ? limit : int.MaxValue;

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

        private static string UserJson(string login, string display, string tenant)
            => $$$"""{"id":"id-{{{login}}}","status":"ACTIVE","profile":{"login":"{{{login}}}","displayName":"{{{display}}}","email":"{{{login}}}@{{{tenant}}}.example","department":"{{{tenant}}}"}}""";

        private static string GroupJson(string name)
            => $$$"""{"id":"id-{{{name}}}","profile":{"name":"{{{name}}}"}}""";

        private static string RoleJson(string label)
            => $$"""{"id":"id-{{label}}","label":"{{label}}"}""";

        private static string Array(IEnumerable<string> items) => $"[{string.Join(",", items)}]";
    }

    // The conformance harness exercises search, not the secret store: a fixed resolver returns the API token for any
    // reference (the production resolver chain is exercised by the §13 secret-resolver tests).
    private sealed class FixedSecretResolver(string secret) : ISecretResolver
    {
        public bool CanResolve(SecretScheme scheme) => true;

        public ValueTask<SecretMaterial> ResolveAsync(SecretRef reference, CancellationToken cancellationToken)
            => new(SecretMaterial.FromString(secret));
    }
}