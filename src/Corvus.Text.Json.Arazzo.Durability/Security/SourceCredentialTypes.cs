// <copyright file="SourceCredentialTypes.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>A named, role-tagged reference to one secret supplied on add/update (design §13). The <see cref="Ref"/>
/// is a <see cref="SecretRef"/> string; it is a pointer, never the secret itself.</summary>
/// <param name="Name">The role this secret plays (e.g. <c>value</c>, <c>password</c>, <c>clientSecret</c>).</param>
/// <param name="Ref">The secret reference (<c>scheme://locator[#version]</c>).</param>
public readonly record struct SecretReferenceDefinition(string Name, string Ref);

/// <summary>A single non-secret auth configuration value supplied on add/update.</summary>
/// <param name="Key">The configuration key (e.g. <c>headerName</c>, <c>tokenUrl</c>, <c>scopes</c>, <c>username</c>).</param>
/// <param name="Value">The non-secret configuration value.</param>
public readonly record struct CredentialConfigDefinition(string Key, string Value);

/// <summary>
/// The mutable content of a <see cref="SourceCredentialBinding"/> supplied on add/update. Carries only references and
/// non-secret metadata — never secret material (design §13).
/// </summary>
/// <param name="SourceName">The Arazzo source description name this credential authenticates calls to.</param>
/// <param name="Environment">The deployment environment the binding applies to.</param>
/// <param name="AuthKind">The auth scheme the resolved secret(s) build into a provider.</param>
/// <param name="SecretRefs">The named references to secret material in the external store (at least one).</param>
/// <param name="Config">The non-secret auth configuration, if any.</param>
/// <param name="Description">An optional human description.</param>
/// <param name="SecurityTags">The security tags (KVP labels) scoping the binding for row authorization (§14.2),
/// stamped at create and immutable thereafter; default (empty) for an unscoped (shared) binding.</param>
public readonly record struct SourceCredentialDefinition(
    string SourceName,
    string Environment,
    SourceCredentialKind AuthKind,
    IReadOnlyList<SecretReferenceDefinition> SecretRefs,
    IReadOnlyList<CredentialConfigDefinition>? Config = null,
    string? Description = null,
    SecurityTagSet SecurityTags = default);