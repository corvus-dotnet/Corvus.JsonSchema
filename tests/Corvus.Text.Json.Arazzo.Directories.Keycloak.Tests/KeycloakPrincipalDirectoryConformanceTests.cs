// <copyright file="KeycloakPrincipalDirectoryConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net.Http.Headers;
using System.Text;
using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Directories.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.Keycloak;

namespace Corvus.Text.Json.Arazzo.Directories.Keycloak.Tests;

/// <summary>
/// Runs the shared <see cref="PrincipalDirectoryConformance"/> suite against <see cref="KeycloakPrincipalDirectory"/> over
/// a real Keycloak server in a container, seeded once (over the Admin REST API) with the <see cref="DirectoryFixture"/>
/// principals — users carrying a <c>tenant</c> attribute, realm groups, and realm roles. The adapter is configured with a
/// mapper that yields the fixture's expected <c>sys:</c> identities, and authenticates with a password grant
/// (<c>admin-cli</c> on the <c>master</c> realm) whose credential is a <c>SecretRef</c>.
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class KeycloakPrincipalDirectoryConformanceTests : PrincipalDirectoryConformance
{
    // Pin a current Keycloak (the module's default 21.1 is obsolete); the module's wait strategy detects v25+ and gates
    // readiness on /health/ready (management port 9000) plus the admin-user-created log line, so seeding never races startup.
    private const string Image = "quay.io/keycloak/keycloak:26.0";
    private const string AdminUser = "admin";
    private const string AdminPassword = "admin";
    private const string Realm = "corvus";

    // The Keycloak user-profile config (KC 24+) gates custom attributes: declare the four standard attributes and enable
    // the unmanaged policy so the deployment's `tenant` attribute (the mapper's sys:tenant source) is accepted on create.
    private const string UserProfileBody = """
        {"unmanagedAttributePolicy":"ENABLED","attributes":[{"name":"username","permissions":{"view":["admin","user"],"edit":["admin","user"]}},{"name":"email","permissions":{"view":["admin","user"],"edit":["admin","user"]}},{"name":"firstName","permissions":{"view":["admin","user"],"edit":["admin","user"]}},{"name":"lastName","permissions":{"view":["admin","user"],"edit":["admin","user"]}}]}
        """;

    private static KeycloakContainer container = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        container = new KeycloakBuilder(Image)
            .WithUsername(AdminUser)
            .WithPassword(AdminPassword)
            .Build();
        await container.StartAsync();

        await SeedAsync();
    }

    [ClassCleanup]
    public static async Task ClassCleanupAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    protected override ValueTask<IPrincipalDirectory> CreateAsync()
    {
        var options = new KeycloakDirectoryOptions
        {
            Issuer = DirectoryFixture.Issuer,
            BaseUrl = new Uri(container.GetBaseAddress()),
            Realm = Realm,
            TokenRealm = "master",
            Authentication = new KeycloakPasswordGrant("admin-cli", AdminUser, DirectoryCredential.Parse("env://KC_ADMIN")),
            Kinds = new Dictionary<GranteeKind, KeycloakResource>
            {
                [GranteeKind.Person] = KeycloakResource.Users,
                [GranteeKind.Team] = KeycloakResource.Groups,
                [GranteeKind.Role] = KeycloakResource.Roles,
            },
        };

        // The deployment mapper: derive each kind's exact sys: identity (the §16.5.4 seam). Persons take sys:tenant from
        // the user's `tenant` attribute and sys:sub from the username; teams/roles from the group/role name. Keycloak's
        // built-in realm roles (offline_access, uma_authorization, default-roles-*) map to no sys: grant, so they drop.
        var mapper = DirectoryIdentityMapper.FromFunc(record => record.Kind switch
        {
            GranteeKind.Person => new ResolvedPrincipal(GranteeKind.Person, record.Id, record.DisplayName, DirectoryFixture.Identity(("sys:tenant", record.Attribute("tenant") ?? string.Empty), ("sys:sub", record.Id))),
            GranteeKind.Team => new ResolvedPrincipal(GranteeKind.Team, record.Id, record.DisplayName, DirectoryFixture.Identity(("sys:team", record.Id))),
            GranteeKind.Role when record.Id is "workflow-admin" or "viewer" => new ResolvedPrincipal(GranteeKind.Role, record.Id, record.DisplayName, DirectoryFixture.Identity(("sys:role", record.Id))),
            _ => (ResolvedPrincipal?)null,
        });

        return new ValueTask<IPrincipalDirectory>(new KeycloakPrincipalDirectory(options, new FixedSecretResolver(AdminPassword), mapper));
    }

    private static async Task SeedAsync()
    {
        using var http = new HttpClient { BaseAddress = new Uri(container.GetBaseAddress()) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetAdminTokenAsync(http));

        // A fresh realm to search, isolated from master.
        await PostAsync(http, "/admin/realms", $$"""{"realm":"{{Realm}}","enabled":true}""");
        await PutAsync(http, $"/admin/realms/{Realm}/users/profile", UserProfileBody);

        // People: alice + albert share tenant acme (and the "al" value prefix); bob is a second tenant.
        await PostAsync(http, $"/admin/realms/{Realm}/users", UserBody("alice", "Alice", "Smith", "acme"));
        await PostAsync(http, $"/admin/realms/{Realm}/users", UserBody("albert", "Albert", "Jones", "acme"));
        await PostAsync(http, $"/admin/realms/{Realm}/users", UserBody("bob", "Bob", "Brown", "globex"));

        // Teams (realm groups) and roles (realm roles).
        await PostAsync(http, $"/admin/realms/{Realm}/groups", """{"name":"payments"}""");
        await PostAsync(http, $"/admin/realms/{Realm}/groups", """{"name":"billing"}""");
        await PostAsync(http, $"/admin/realms/{Realm}/roles", """{"name":"workflow-admin"}""");
        await PostAsync(http, $"/admin/realms/{Realm}/roles", """{"name":"viewer"}""");
    }

    private static string UserBody(string username, string first, string last, string tenant)
        => $$$"""{"username":"{{{username}}}","enabled":true,"firstName":"{{{first}}}","lastName":"{{{last}}}","email":"{{{username}}}@{{{tenant}}}.example","attributes":{"tenant":["{{{tenant}}}"]}}""";

    // The seed authenticates the same way the adapter does — a password grant for the uber-admin via admin-cli on the
    // master realm — but retries, since Keycloak can answer /health/ready a moment before the token endpoint is live.
    private static async Task<string> GetAdminTokenAsync(HttpClient http)
    {
        for (int attempt = 1; ; attempt++)
        {
            using var content = new FormUrlEncodedContent(
            [
                new("grant_type", "password"),
                new("client_id", "admin-cli"),
                new("username", AdminUser),
                new("password", AdminPassword),
            ]);
            try
            {
                using HttpResponseMessage response = await http.PostAsync("/realms/master/protocol/openid-connect/token", content);
                response.EnsureSuccessStatusCode();
                return ReadAccessToken(await response.Content.ReadAsByteArrayAsync());
            }
            catch (Exception) when (attempt < 20)
            {
                await Task.Delay(500);
            }
        }
    }

    private static string ReadAccessToken(ReadOnlySpan<byte> body)
    {
        var reader = new Utf8JsonReader(body);
        if (reader.Read() && reader.TokenType == JsonTokenType.StartObject)
        {
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                if (reader.ValueTextEquals("access_token"u8))
                {
                    reader.Read();
                    return reader.GetString()!;
                }

                reader.Read();
                reader.Skip();
            }
        }

        throw new InvalidOperationException("The Keycloak token response did not contain an access_token.");
    }

    private static Task PostAsync(HttpClient http, string path, string json) => SendAsync(http, HttpMethod.Post, path, json);

    private static Task PutAsync(HttpClient http, string path, string json) => SendAsync(http, HttpMethod.Put, path, json);

    private static async Task SendAsync(HttpClient http, HttpMethod method, string path, string json)
    {
        using var request = new HttpRequestMessage(method, path) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        using HttpResponseMessage response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            string detail = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"{method} {path} failed: {(int)response.StatusCode} {response.StatusCode} {detail}");
        }
    }

    // The conformance harness exercises search, not the secret store: a fixed resolver returns the admin password for any
    // reference (the production resolver chain is exercised by the §13 secret-resolver tests).
    private sealed class FixedSecretResolver(string secret) : ISecretResolver
    {
        public bool CanResolve(SecretScheme scheme) => true;

        public ValueTask<SecretMaterial> ResolveAsync(SecretRef reference, CancellationToken cancellationToken)
            => new(SecretMaterial.FromString(secret));
    }
}