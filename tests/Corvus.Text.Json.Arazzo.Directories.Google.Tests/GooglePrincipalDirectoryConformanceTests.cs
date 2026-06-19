// <copyright file="GooglePrincipalDirectoryConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Security.Cryptography;
using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Directories.Conformance;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Directories.Google.Tests;

/// <summary>
/// Runs the shared <see cref="PrincipalDirectoryConformance"/> suite against <see cref="GooglePrincipalDirectory"/> over a
/// mock Google Admin SDK Directory API (a <see cref="StubHttpMessageHandler"/>, no network), which both mints the
/// service-account access token (it accepts the signed JWT without verifying it) and serves the
/// <see cref="DirectoryFixture"/> principals as Directory entities — users carrying an <c>orgUnitPath</c> (the mapper's
/// <c>sys:tenant</c> source), groups, and roles. The adapter signs the JWT with a real (test-generated) RSA key, so the
/// auth path runs for real; the mock just doesn't validate it.
/// </summary>
[TestClass]
public sealed class GooglePrincipalDirectoryConformanceTests : PrincipalDirectoryConformance
{
    private static readonly Uri DirectoryBaseUrl = new("https://admin.example.org/admin/directory/v1");
    private static readonly Uri TokenEndpoint = new("https://oauth2.example.org/token");
    private static readonly string PrivateKeyPem = CreatePrivateKeyPem();

    protected override ValueTask<IPrincipalDirectory> CreateAsync()
    {
        var options = new GoogleDirectoryOptions
        {
            Issuer = DirectoryFixture.Issuer,
            Authentication = new GoogleServiceAccount("svc@project.iam.gserviceaccount.com", DirectoryCredential.Parse("env://GOOGLE_KEY"), "admin@example.org"),
            DirectoryBaseUrl = DirectoryBaseUrl,
            TokenEndpoint = TokenEndpoint,
            Kinds = new Dictionary<GranteeKind, GoogleResource>
            {
                [GranteeKind.Person] = GoogleResource.Users,
                [GranteeKind.Team] = GoogleResource.Groups,
                [GranteeKind.Role] = new("roles", "items", "roleName", "roleName", "roleName"),
            },
        };

        // The deployment mapper — a SPAN mapper exercising the bytes-to-bytes path, including a SPAN TRANSFORM: persons take
        // sys:tenant from `orgUnitPath` with the leading '/' sliced off (no string), sys:sub from the value (primaryEmail);
        // teams/roles from the value.
        var mapper = DirectorySpanIdentityMapper.FromIdentity(
            ["orgUnitPath"],
            static (DirectoryRecordView record, ref IdentityBuilder identity) =>
            {
                switch (record.Kind)
                {
                    case GranteeKind.Person:
                        ReadOnlySpan<byte> orgUnit = record.AttributeUtf8("orgUnitPath"u8);
                        if (orgUnit.Length > 0 && orgUnit[0] == (byte)'/')
                        {
                            orgUnit = orgUnit[1..];
                        }

                        identity.Add("sys:tenant"u8, orgUnit);
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

        var httpClient = new HttpClient(new StubHttpMessageHandler(GoogleMockBackend.Respond));
        return new ValueTask<IPrincipalDirectory>(new GooglePrincipalDirectory(options, new FixedSecretResolver(PrivateKeyPem), mapper, httpClient));
    }

    private static string CreatePrivateKeyPem()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportPkcs8PrivateKeyPem();
    }

    // A minimal Google endpoint: mints the access token (ignoring the JWT), then serves the fixture principals as Directory
    // entities, honouring the `<field>:<prefix>*` query and `maxResults`. Users carry orgUnitPath (= /tenant); groups and
    // roles are name-only entities under their results arrays (users / groups / items).
    private static class GoogleMockBackend
    {
        private static readonly (string Email, string Display, string OrgUnit)[] Users =
        [
            ("alice", "Alice Smith", "/acme"),
            ("albert", "Albert Jones", "/acme"),
            ("bob", "Bob Brown", "/globex"),
        ];

        private static readonly string[] Groups = ["payments", "billing"];
        private static readonly string[] Roles = ["workflow-admin", "viewer"];

        public static HttpResponseMessage Respond(HttpRequestMessage request)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/token", StringComparison.Ordinal))
            {
                return StubHttpMessageHandler.Json("""{"token_type":"Bearer","expires_in":3600,"access_token":"mock-google-access-token"}""");
            }

            if (request.Headers.Authorization is not { Scheme: "Bearer", Parameter: { Length: > 0 } })
            {
                return StubHttpMessageHandler.Json("""{"error":"unauthorized"}""", "application/json", HttpStatusCode.Unauthorized);
            }

            string path = request.RequestUri.AbsolutePath;
            (string? field, string? prefix) = ParseQuery(request.RequestUri.Query);
            int max = ParseMax(request.RequestUri.Query);

            if (path.EndsWith("/users", StringComparison.Ordinal))
            {
                string users = Join(Users.Where(u => Matches(field, prefix, "email", u.Email)).Select(u => UserJson(u.Email, u.Display, u.OrgUnit)).Take(max));
                return StubHttpMessageHandler.Json($$"""{"users":[{{users}}]}""");
            }

            if (path.EndsWith("/groups", StringComparison.Ordinal))
            {
                string groups = Join(Groups.Where(g => Matches(field, prefix, "email", g)).Select(GroupJson).Take(max));
                return StubHttpMessageHandler.Json($$"""{"groups":[{{groups}}]}""");
            }

            if (path.EndsWith("/roles", StringComparison.Ordinal))
            {
                string items = Join(Roles.Where(r => Matches(field, prefix, "roleName", r)).Select(RoleJson).Take(max));
                return StubHttpMessageHandler.Json($$"""{"items":[{{items}}]}""");
            }

            return StubHttpMessageHandler.Json("""{}""");
        }

        private static bool Matches(string? field, string? prefix, string queryField, string value)
            => prefix is null || (string.Equals(field, queryField, StringComparison.OrdinalIgnoreCase) && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        // Parses `<field>:<prefix>*` from the (URL-encoded) `query` parameter into (field, prefix); (null, null) when absent.
        private static (string? Field, string? Prefix) ParseQuery(string query)
        {
            string? expression = QueryValue(query, "query");
            if (expression is null)
            {
                return (null, null);
            }

            int colon = expression.IndexOf(':', StringComparison.Ordinal);
            if (colon < 0)
            {
                return (null, null);
            }

            return (expression[..colon], expression[(colon + 1)..].TrimEnd('*'));
        }

        private static int ParseMax(string query) => int.TryParse(QueryValue(query, "maxResults"), out int max) && max > 0 ? max : int.MaxValue;

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

        private static string UserJson(string email, string display, string orgUnit)
        {
            string[] parts = display.Split(' ', 2);
            return $$"""{"primaryEmail":"{{email}}","name":{"givenName":"{{parts[0]}}","familyName":"{{parts[1]}}","fullName":"{{display}}"},"id":"id-{{email}}","orgUnitPath":"{{orgUnit}}"}""";
        }

        private static string GroupJson(string name) => $$"""{"email":"{{name}}","name":"{{name}}","id":"id-{{name}}"}""";

        private static string RoleJson(string name) => $$"""{"roleName":"{{name}}","roleId":"id-{{name}}"}""";

        private static string Join(IEnumerable<string> items) => string.Join(",", items);
    }

    // The conformance harness exercises search, not the secret store: a fixed resolver returns the service-account private
    // key for any reference (the production resolver chain is exercised by the §13 secret-resolver tests).
    private sealed class FixedSecretResolver(string secret) : ISecretResolver
    {
        public bool CanResolve(SecretScheme scheme) => true;

        public ValueTask<SecretMaterial> ResolveAsync(SecretRef reference, CancellationToken cancellationToken)
            => new(SecretMaterial.FromString(secret));
    }
}