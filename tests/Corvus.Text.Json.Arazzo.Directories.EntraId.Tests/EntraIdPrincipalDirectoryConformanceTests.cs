// <copyright file="EntraIdPrincipalDirectoryConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Directories.Conformance;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Directories.EntraId.Tests;

/// <summary>
/// Runs the shared <see cref="PrincipalDirectoryConformance"/> suite against <see cref="EntraIdPrincipalDirectory"/> over
/// a mock Microsoft Graph endpoint (a <see cref="StubHttpMessageHandler"/>, no network), which both mints the OAuth 2.0
/// client-credentials token and serves the <see cref="DirectoryFixture"/> principals as Graph entities — users carrying
/// <c>department</c> (the mapper's <c>sys:tenant</c> source), groups, and directory roles. The mock honours the Graph
/// <c>startsWith</c> filter and <c>$top</c>, so the adapter's real request-building and response-parsing are under test.
/// </summary>
[TestClass]
public sealed class EntraIdPrincipalDirectoryConformanceTests : PrincipalDirectoryConformance
{
    private const string TenantId = "00000000-0000-0000-0000-000000000000";
    private static readonly Uri GraphBaseUrl = new("https://graph.example.org/v1.0");
    private static readonly Uri LoginBaseUrl = new("https://login.example.org");

    protected override ValueTask<IPrincipalDirectory> CreateAsync()
    {
        var options = new EntraIdDirectoryOptions
        {
            Issuer = DirectoryFixture.Issuer,
            TenantId = TenantId,
            GraphBaseUrl = GraphBaseUrl,
            LoginBaseUrl = LoginBaseUrl,
            Authentication = new EntraIdClientCredentials("client-id", DirectoryCredential.Parse("env://GRAPH_SECRET")),
            Kinds = new Dictionary<GranteeKind, EntraIdResource>
            {
                [GranteeKind.Person] = EntraIdResource.Users,
                [GranteeKind.Team] = EntraIdResource.Groups,
                [GranteeKind.Role] = new("directoryRoles", "displayName", "displayName"),
            },
        };

        // The deployment mapper — a SPAN mapper exercising the bytes-to-bytes path: it writes each kind's sys: tags straight
        // into the IdentityBuilder from the record's UTF-8 spans, never materializing an attribute string (persons take
        // sys:tenant from `department` and sys:sub from the searchable value; teams/roles from the value). The adapter
        // appends sys:iss. Declaring `department` lets the adapter capture exactly it (plus the value/label) as spans.
        var mapper = DirectorySpanIdentityMapper.FromIdentity(
            ["department"],
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

        var httpClient = new HttpClient(new StubHttpMessageHandler(GraphMockBackend.Respond));
        return new ValueTask<IPrincipalDirectory>(new EntraIdPrincipalDirectory(options, new FixedSecretResolver("graph-secret"), mapper, httpClient));
    }

    // A minimal Microsoft Graph endpoint: mints the client-credentials token, then serves the fixture principals as Graph
    // entities, honouring `$filter=startsWith(<attr>,'x')` and `$top`. Users carry department (= tenant); groups and roles
    // are name-only entities.
    private static class GraphMockBackend
    {
        private static readonly (string MailNickname, string Display, string Tenant)[] Users =
        [
            ("alice", "Alice Smith", "acme"),
            ("albert", "Albert Jones", "acme"),
            ("bob", "Bob Brown", "globex"),
        ];

        private static readonly string[] Groups = ["payments", "billing"];
        private static readonly string[] Roles = ["workflow-admin", "viewer"];

        public static HttpResponseMessage Respond(HttpRequestMessage request)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/oauth2/v2.0/token", StringComparison.Ordinal))
            {
                return StubHttpMessageHandler.Json("""{"token_type":"Bearer","expires_in":3600,"access_token":"mock-graph-access-token"}""");
            }

            // Prove the adapter resolved and presented the access token on the Graph call.
            if (request.Headers.Authorization is not { Scheme: "Bearer", Parameter: { Length: > 0 } })
            {
                return StubHttpMessageHandler.Json("""{"error":{"code":"InvalidAuthenticationToken"}}""", "application/json", HttpStatusCode.Unauthorized);
            }

            Uri uri = request.RequestUri;
            string resource = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)[^1];
            (string? filterAttribute, string? prefix) = ParseFilter(uri.Query);
            int top = ParseTop(uri.Query);

            IEnumerable<string> entities = resource switch
            {
                "users" => Users.Where(u => Matches(filterAttribute, prefix, "mailNickname", u.MailNickname)).Select(u => UserJson(u.MailNickname, u.Display, u.Tenant)),
                "groups" => Groups.Where(g => Matches(filterAttribute, prefix, "displayName", g)).Select(NamedJson),
                "directoryRoles" => Roles.Where(r => Matches(filterAttribute, prefix, "displayName", r)).Select(NamedJson),
                _ => [],
            };

            return StubHttpMessageHandler.Json(CollectionResponse(entities.Take(top)));
        }

        private static bool Matches(string? filterAttribute, string? prefix, string attribute, string value)
            => prefix is null || (string.Equals(filterAttribute, attribute, StringComparison.OrdinalIgnoreCase) && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        // Parses `startsWith(<attr>,'x')` from the (URL-encoded) query into (attribute, prefix); (null, null) when absent.
        private static (string? Attribute, string? Prefix) ParseFilter(string query)
        {
            string? filter = QueryValue(query, "$filter");
            const string Prefix = "startsWith(";
            if (filter is null || !filter.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) || !filter.EndsWith(')'))
            {
                return (null, null);
            }

            string inner = filter[Prefix.Length..^1];
            int comma = inner.IndexOf(',', StringComparison.Ordinal);
            if (comma < 0)
            {
                return (null, null);
            }

            string attribute = inner[..comma].Trim();
            string literal = inner[(comma + 1)..].Trim().Trim('\'').Replace("''", "'", StringComparison.Ordinal);
            return (attribute, literal);
        }

        private static int ParseTop(string query) => int.TryParse(QueryValue(query, "$top"), out int top) && top > 0 ? top : int.MaxValue;

        private static string? QueryValue(string query, string key)
        {
            foreach (string pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = pair.IndexOf('=', StringComparison.Ordinal);
                if (eq > 0 && string.Equals(Uri.UnescapeDataString(pair[..eq]), key, StringComparison.Ordinal))
                {
                    return Uri.UnescapeDataString(pair[(eq + 1)..]);
                }
            }

            return null;
        }

        private static string UserJson(string mailNickname, string display, string tenant)
            => $$"""{"id":"id-{{mailNickname}}","mailNickname":"{{mailNickname}}","displayName":"{{display}}","department":"{{tenant}}"}""";

        private static string NamedJson(string displayName)
            => $$"""{"id":"id-{{displayName}}","displayName":"{{displayName}}"}""";

        private static string CollectionResponse(IEnumerable<string> entities)
            => $$"""{"@odata.context":"https://graph.example.org/v1.0/$metadata","value":[{{string.Join(",", entities)}}]}""";
    }

    // The conformance harness exercises search, not the secret store: a fixed resolver returns the client secret for any
    // reference (the production resolver chain is exercised by the §13 secret-resolver tests).
    private sealed class FixedSecretResolver(string secret) : ISecretResolver
    {
        public bool CanResolve(SecretScheme scheme) => true;

        public ValueTask<SecretMaterial> ResolveAsync(SecretRef reference, CancellationToken cancellationToken)
            => new(SecretMaterial.FromString(secret));
    }
}