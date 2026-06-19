// <copyright file="LdapPrincipalDirectory.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography.X509Certificates;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Novell.Directory.Ldap;
using Novell.Directory.Ldap.Sasl;

namespace Corvus.Text.Json.Arazzo.Directories.Ldap;

/// <summary>
/// An LDAP / Active Directory <see cref="IPrincipalDirectory"/> (design §16.5.4): searches a directory (AD, OpenLDAP,
/// FreeIPA, …) for people / teams / roles by a value-attribute prefix and projects each entry to its deployment-stamped
/// <c>sys:</c> identity via the supplied <see cref="IDirectoryIdentityMapper"/>. It binds as a read-only service account
/// using the configured <see cref="LdapBindMethod"/> (simple / DIGEST-MD5 / client-certificate), whose secret(s) are a
/// <c>SecretRef</c> resolved through the deployment's <see cref="ISecretResolver"/> — never stored.
/// </summary>
/// <remarks>
/// Read-only and fully async (the pure-managed client needs no native <c>libldap</c>, so it behaves identically on
/// Windows, Linux, and Alpine). Each search opens a short-lived bound connection (a grantee typeahead is low-frequency).
/// An entry that the mapper drops (returns <see langword="null"/>) or that lacks the value attribute is excluded.
/// </remarks>
public sealed class LdapPrincipalDirectory : IPrincipalDirectory
{
    private readonly LdapDirectoryOptions options;
    private readonly ISecretResolver resolver;
    private readonly DirectoryPrincipalProjector projector;

    /// <summary>Initializes a new instance of the <see cref="LdapPrincipalDirectory"/> class.</summary>
    /// <param name="options">The non-secret connection + schema + bind-method configuration.</param>
    /// <param name="resolver">The deployment's secret resolver, used to dereference the bind method's <c>SecretRef</c>(s).</param>
    /// <param name="mapper">The deployment's projection from a raw directory record to a <c>sys:</c> identity.</param>
    public LdapPrincipalDirectory(LdapDirectoryOptions options, ISecretResolver resolver, IDirectoryIdentityMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(mapper);
        this.options = options;
        this.resolver = resolver;

        // Every resolved principal is funnelled through the projector, so the configured issuer is stamped onto each
        // identity (mapper-immutable) — the adapter cannot return a principal without its sys:iss.
        this.projector = new DirectoryPrincipalProjector(mapper, options.Issuer);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ResolvedPrincipal>> SearchAsync(GranteeKind kind, string query, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!this.options.Kinds.TryGetValue(kind, out LdapKindOptions? kindOptions))
        {
            return [];
        }

        int pageSize = limit > 0 ? limit : 1;

        // A client certificate (SASL EXTERNAL) must be on the TLS handshake, so it is resolved before the connection is
        // built and lives until the search completes.
        X509Certificate2? clientCertificate = await this.ResolveClientCertificateAsync(cancellationToken).ConfigureAwait(false);
        using (clientCertificate)
        {
            using var connection = new LdapConnection(this.BuildConnectionOptions(clientCertificate));
            await connection.ConnectAsync(this.options.Host, this.options.Port, cancellationToken).ConfigureAwait(false);
            if (this.options.Security == LdapTransportSecurity.StartTls)
            {
                await connection.StartTlsAsync(cancellationToken).ConfigureAwait(false);
            }

            await this.BindAsync(connection, cancellationToken).ConfigureAwait(false);
            return await this.SearchBoundAsync(connection, kind, kindOptions, query, pageSize, cancellationToken).ConfigureAwait(false);
        }
    }

    // RFC 4515 assertion-value escaping for the user-supplied query (so a query can never alter the filter structure).
    private static string EscapeFilter(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            switch (c)
            {
                case '\\': builder.Append("\\5c"); break;
                case '*': builder.Append("\\2a"); break;
                case '(': builder.Append("\\28"); break;
                case ')': builder.Append("\\29"); break;
                case '\0': builder.Append("\\00"); break;
                default: builder.Append(c); break;
            }
        }

        return builder.ToString();
    }

    // LdapEntry.Get / LdapAttributeSet.GetAttribute THROW on a missing attribute, so look up case-insensitively over the
    // attribute set (LDAP attribute names are case-insensitive) and return null when absent.
    private static LdapAttribute? Find(LdapAttributeSet attributes, string name)
    {
        foreach (LdapAttribute attribute in attributes)
        {
            if (string.Equals(attribute.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return attribute;
            }
        }

        return null;
    }

    private static string? First(LdapAttributeSet attributes, string name)
        => Find(attributes, name)?.StringValue is { Length: > 0 } value ? value : null;

    private static IReadOnlyList<string> All(LdapAttributeSet attributes, string? name)
        => name is not null && Find(attributes, name)?.StringValueArray is { Length: > 0 } values ? values : [];

    private async ValueTask<X509Certificate2?> ResolveClientCertificateAsync(CancellationToken cancellationToken)
    {
        if (this.options.Bind is not LdapClientCertificateBind certificateBind)
        {
            return null;
        }

        byte[] pfx = await certificateBind.Certificate.ResolveBytesAsync(this.resolver, cancellationToken).ConfigureAwait(false);
        string? password = certificateBind.CertificatePassword is { } passwordRef
            ? await passwordRef.ResolveStringAsync(this.resolver, cancellationToken).ConfigureAwait(false)
            : null;
        return X509CertificateLoader.LoadPkcs12(pfx, password);
    }

    private LdapConnectionOptions BuildConnectionOptions(X509Certificate2? clientCertificate)
    {
        var connectionOptions = new LdapConnectionOptions();
        if (this.options.Security == LdapTransportSecurity.Ldaps)
        {
            connectionOptions.UseSsl();
        }

        if (this.options.ServerCertificateValidationCallback is { } validate)
        {
            connectionOptions.ConfigureRemoteCertificateValidationCallback(validate);
        }

        if (clientCertificate is not null)
        {
            connectionOptions.ConfigureClientCertificates([clientCertificate]);
            connectionOptions.ConfigureLocalCertificateSelectionCallback((_, _, _, _, _) => clientCertificate);
        }

        return connectionOptions;
    }

    private async ValueTask BindAsync(LdapConnection connection, CancellationToken cancellationToken)
    {
        switch (this.options.Bind)
        {
            case LdapSimpleBind simple:
                string password = await simple.Password.ResolveStringAsync(this.resolver, cancellationToken).ConfigureAwait(false);
                await connection.BindAsync(simple.BindDn, password, cancellationToken).ConfigureAwait(false);
                break;

            case LdapDigestMd5Bind digest:
                string digestPassword = await digest.Password.ResolveStringAsync(this.resolver, cancellationToken).ConfigureAwait(false);
                await connection.BindAsync(new SaslDigestMd5Request(digest.Username, digestPassword, digest.Realm ?? string.Empty, this.options.Host), cancellationToken).ConfigureAwait(false);
                break;

            case LdapClientCertificateBind certificate:
                var external = new SaslExternalRequest();
                if (certificate.AuthorizationId is { } authorizationId)
                {
                    external.AuthorizationId = authorizationId;
                }

                await connection.BindAsync(external, cancellationToken).ConfigureAwait(false);
                break;

            default:
                throw new InvalidOperationException($"Unsupported LDAP bind method '{this.options.Bind.GetType().Name}'.");
        }
    }

    private async ValueTask<IReadOnlyList<ResolvedPrincipal>> SearchBoundAsync(LdapConnection connection, GranteeKind kind, LdapKindOptions kindOptions, string query, int limit, CancellationToken cancellationToken)
    {
        string valueClause = query.Length == 0
            ? $"({kindOptions.ValueAttribute}=*)"
            : $"({kindOptions.ValueAttribute}={EscapeFilter(query)}*)";
        string filter = $"(&(objectClass={kindOptions.ObjectClass}){valueClause})";

        var results = new List<ResolvedPrincipal>(limit);
        ILdapSearchResults search = await connection.SearchAsync(kindOptions.BaseDn, LdapConnection.ScopeSub, filter, BuildAttributeList(kindOptions), typesOnly: false, cancellationToken).ConfigureAwait(false);
        while (results.Count < limit && await search.HasMoreAsync(cancellationToken).ConfigureAwait(false))
        {
            LdapEntry entry = await search.NextAsync(cancellationToken).ConfigureAwait(false);
            if (this.ToRecord(kind, kindOptions, entry) is { } record && this.projector.Project(record) is { } principal)
            {
                results.Add(principal);
            }
        }

        return results;
    }

    private static string[] BuildAttributeList(LdapKindOptions kindOptions)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { kindOptions.ValueAttribute };
        if (kindOptions.DisplayAttribute is { } display)
        {
            set.Add(display);
        }

        foreach (string attribute in kindOptions.FetchAttributes)
        {
            set.Add(attribute);
        }

        if (kindOptions.GroupAttribute is { } group)
        {
            set.Add(group);
        }

        return [.. set];
    }

    // An entry without the value attribute cannot be named as a grantee, so it is skipped (returns null).
    private DirectoryRecord? ToRecord(GranteeKind kind, LdapKindOptions kindOptions, LdapEntry entry)
    {
        LdapAttributeSet attributes = entry.GetAttributeSet();
        if (First(attributes, kindOptions.ValueAttribute) is not { } value)
        {
            return null;
        }

        var projected = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [kindOptions.ValueAttribute] = [value],
        };
        foreach (string attribute in kindOptions.FetchAttributes)
        {
            IReadOnlyList<string> values = All(attributes, attribute);
            if (values.Count > 0)
            {
                projected[attribute] = values;
            }
        }

        string? display = kindOptions.DisplayAttribute is { } d ? First(attributes, d) : null;
        return new DirectoryRecord(kind, value, display, projected, All(attributes, kindOptions.GroupAttribute));
    }
}