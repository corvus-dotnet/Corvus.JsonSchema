// <copyright file="LdapPrincipalDirectoryConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Directories.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Security;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Novell.Directory.Ldap;

namespace Corvus.Text.Json.Arazzo.Directories.Ldap.Tests;

/// <summary>
/// Runs the shared <see cref="PrincipalDirectoryConformance"/> suite against <see cref="LdapPrincipalDirectory"/> over a
/// real OpenLDAP server in a container, seeded once with the <see cref="DirectoryFixture"/> principals and searched with
/// a simple bind. The adapter is configured with a mapper that yields the fixture's expected <c>sys:</c> identities.
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class LdapPrincipalDirectoryConformanceTests : PrincipalDirectoryConformance
{
    private const string Root = "dc=example,dc=org";
    private const string AdminDn = "cn=admin," + Root;
    private const string AdminPassword = "adminpassword";
    private const int LdapPort = 389;

    private static IFutureDockerImage image = null!;
    private static IContainer container = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        // We build the OpenLDAP server image ourselves from Debian's maintained slapd (see openldap/Dockerfile) rather
        // than depend on a third-party registry namespace that can be purged (as docker.io/bitnami/openldap was). The
        // Dockerfile preseeds the suffix dc=example,dc=org + admin cn=admin,dc=example,dc=org, so the base entry already
        // exists at startup and the seed adds only the org-units and principals beneath it.
        image = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(CommonDirectoryPath.GetCallerFileDirectory(), "openldap")
            .WithDockerfile("Dockerfile")
            .WithName("corvus-test-openldap:local")
            .WithDeleteIfExists(false)
            .Build();
        await image.CreateAsync();

        // Gate readiness on slapd actually answering an authenticated LDAP query — not on a log line — so the seed never
        // races a socket that is open but not yet serving binds.
        container = new ContainerBuilder()
            .WithImage(image)
            .WithPortBinding(LdapPort, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted(
                "ldapsearch", "-x", "-D", AdminDn, "-w", AdminPassword, "-H", "ldap://127.0.0.1:389", "-b", Root, "-s", "base", "-LLL", "1.1"))
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
        var options = new LdapDirectoryOptions
        {
            Issuer = DirectoryFixture.Issuer,
            Host = container.Hostname,
            Port = container.GetMappedPublicPort(LdapPort),
            Security = LdapTransportSecurity.None,
            Bind = new LdapSimpleBind(AdminDn, DirectoryCredential.Parse("env://LDAP_BIND")),
            Kinds = new Dictionary<GranteeKind, LdapKindOptions>
            {
                [GranteeKind.Person] = new() { BaseDn = "ou=people," + Root, ObjectClass = "inetOrgPerson", ValueAttribute = "uid", DisplayAttribute = "cn", FetchAttributes = ["departmentNumber"] },
                [GranteeKind.Team] = new() { BaseDn = "ou=groups," + Root, ObjectClass = "organizationalRole", ValueAttribute = "cn", DisplayAttribute = "cn" },
                [GranteeKind.Role] = new() { BaseDn = "ou=roles," + Root, ObjectClass = "organizationalRole", ValueAttribute = "cn", DisplayAttribute = "cn" },
            },
        };

        // The deployment mapper: derive each kind's exact sys: identity from the fetched attributes (the §16.5.4 seam).
        var mapper = DirectoryIdentityMapper.FromFunc(record => record.Kind switch
        {
            GranteeKind.Person => new ResolvedPrincipal(GranteeKind.Person, record.Id, record.DisplayName, DirectoryFixture.Identity(("sys:tenant", record.Attribute("departmentNumber") ?? string.Empty), ("sys:sub", record.Id))),
            GranteeKind.Team => new ResolvedPrincipal(GranteeKind.Team, record.Id, record.DisplayName, DirectoryFixture.Identity(("sys:team", record.Id))),
            GranteeKind.Role => new ResolvedPrincipal(GranteeKind.Role, record.Id, record.DisplayName, DirectoryFixture.Identity(("sys:role", record.Id))),
            _ => (ResolvedPrincipal?)null,
        });

        return new ValueTask<IPrincipalDirectory>(new LdapPrincipalDirectory(options, new FixedSecretResolver(AdminPassword), mapper));
    }

    private static async Task SeedAsync()
    {
        string host = container.Hostname;
        int port = container.GetMappedPublicPort(LdapPort);

        // slapd may accept a TCP connection a moment before it serves binds; bound each attempt with a timeout so a
        // not-ready bind retries (rather than hanging) until the server is ready.
        using var connection = new LdapConnection();
        for (int attempt = 1; ; attempt++)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await connection.ConnectAsync(host, port, timeout.Token);
                await connection.BindAsync(AdminDn, AdminPassword, timeout.Token);
                break;
            }
            catch (Exception) when (attempt < 20)
            {
                await Task.Delay(500);
            }
        }

        // The Dockerfile's bootstrap already created the suffix entry; ensure it (and the org-units) tolerantly so the
        // structural seed is re-runnable and independent of whether a given server pre-creates the base entry.
        await EnsureAsync(connection, Root, ("objectClass", "dcObject"), ("objectClass", "organization"), ("dc", "example"), ("o", "Example Inc."));
        await EnsureAsync(connection, "ou=people," + Root, ("objectClass", "organizationalUnit"), ("ou", "people"));
        await EnsureAsync(connection, "ou=groups," + Root, ("objectClass", "organizationalUnit"), ("ou", "groups"));
        await EnsureAsync(connection, "ou=roles," + Root, ("objectClass", "organizationalUnit"), ("ou", "roles"));

        await AddPersonAsync(connection, "alice", "Alice Smith", "Smith", "acme");
        await AddPersonAsync(connection, "albert", "Albert Jones", "Jones", "acme");
        await AddPersonAsync(connection, "bob", "Bob Brown", "Brown", "globex");

        await AddRoleAsync(connection, "ou=groups," + Root, "payments");
        await AddRoleAsync(connection, "ou=groups," + Root, "billing");
        await AddRoleAsync(connection, "ou=roles," + Root, "workflow-admin");
        await AddRoleAsync(connection, "ou=roles," + Root, "viewer");
    }

    private static Task AddPersonAsync(LdapConnection connection, string uid, string cn, string sn, string department)
        => AddAsync(
            connection,
            $"uid={uid},ou=people,{Root}",
            ("objectClass", "inetOrgPerson"),
            ("uid", uid),
            ("cn", cn),
            ("sn", sn),
            ("departmentNumber", department));

    private static Task AddRoleAsync(LdapConnection connection, string baseDn, string cn)
        => AddAsync(connection, $"cn={cn},{baseDn}", ("objectClass", "organizationalRole"), ("cn", cn));

    private static async Task EnsureAsync(LdapConnection connection, string dn, params (string Name, string Value)[] attributes)
    {
        try
        {
            await AddAsync(connection, dn, attributes);
        }
        catch (LdapException e) when (e.ResultCode == LdapException.EntryAlreadyExists)
        {
            // The structural node already exists (e.g. the bootstrap-created base entry) — idempotent, nothing to do.
        }
    }

    private static async Task AddAsync(LdapConnection connection, string dn, params (string Name, string Value)[] attributes)
    {
        // LdapAttributeSet is keyed by attribute name, so a second Add for the same name overwrites the first. Coalesce
        // repeated names (e.g. multi-valued objectClass on the base entry) into one multi-valued LdapAttribute.
        var set = new LdapAttributeSet();
        foreach (var group in attributes.GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
        {
            set.Add(new LdapAttribute(group.Key, group.Select(a => a.Value).ToArray()));
        }

        await connection.AddAsync(new LdapEntry(dn, set));
    }

    // The conformance harness exercises search, not the secret store: a fixed resolver returns the bind password for any
    // reference (the production resolver chain is exercised by the §13 secret-resolver tests).
    private sealed class FixedSecretResolver(string secret) : ISecretResolver
    {
        public bool CanResolve(SecretScheme scheme) => true;

        public ValueTask<SecretMaterial> ResolveAsync(SecretRef reference, CancellationToken cancellationToken)
            => new(SecretMaterial.FromString(secret));
    }
}