// <copyright file="OpenApi32CSharpEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;

namespace Corvus.Text.Json.OpenApi32;

/// <summary>
/// The C# <see cref="IClientEmitter"/> for OpenAPI 3.2: a thin subclass of the shared
/// <see cref="OpenApiCSharpEmitterBase"/>.
/// </summary>
/// <remarks>
/// <para>
/// OpenAPI 3.2 is the superset the shared emitter is shaped around, so this subclass adds only the
/// one piece of emit that needs the strongly-typed model: <see cref="PrepareContext"/> dereferences
/// the typed <see cref="OpenApiDocument"/> to extract the document identity (<c>$self</c>) and the
/// security schemes into the <see cref="ClientEmitContext"/>. Everything else is inherited from
/// <see cref="OpenApiCSharpEmitterBase"/>.
/// </para>
/// </remarks>
internal sealed class OpenApi32CSharpEmitter : OpenApiCSharpEmitterBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApi32CSharpEmitter"/> class.
    /// </summary>
    /// <param name="rootNamespace">The root namespace for generated code.</param>
    /// <param name="clientNamePrefix">Optional prefix for client type names.</param>
    /// <param name="ignoreEmptyFormUrlEncodedBody">
    /// When <see langword="true"/>, form-urlencoded request bodies whose schema defines no
    /// properties are treated as if the body were absent.
    /// </param>
    /// <param name="schemaTypeResolver">The schema-type resolver.</param>
    /// <param name="walker">The walker (used for client-name synthesis).</param>
    public OpenApi32CSharpEmitter(
        string rootNamespace,
        string? clientNamePrefix,
        bool ignoreEmptyFormUrlEncodedBody,
        ISchemaTypeResolver schemaTypeResolver,
        OpenApi32Walker walker)
        : base(rootNamespace, clientNamePrefix, ignoreEmptyFormUrlEncodedBody, schemaTypeResolver, walker)
    {
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The 3.2 implementation additionally dereferences the strongly-typed
    /// <see cref="OpenApiDocument"/> to extract the document identity (<c>$self</c>) and the
    /// security schemes that the shared intermediate representation does not carry.
    /// </remarks>
    public override ClientEmitContext PrepareContext(
        JsonElement specRoot,
        IOpenApiReferenceResolver referenceResolver,
        ServerInfo? rootServer)
        => new(
            specRoot,
            referenceResolver,
            rootServer,
            GetDocumentSelf(specRoot),
            PrepareSecuritySchemes(specRoot));

    private static string? GetDocumentSelf(JsonElement specRoot)
    {
        OpenApiDocument doc = specRoot;
        return doc.Self.IsNotUndefined() ? doc.Self.GetString() : null;
    }

    private static SecuritySchemeInfo[] PrepareSecuritySchemes(JsonElement specRoot)
    {
        OpenApiDocument doc = specRoot;
        if (doc.ComponentsValue.IsUndefined() || doc.ComponentsValue.SecuritySchemes.IsUndefined())
        {
            return [];
        }

        List<SecuritySchemeInfo> result = [];

        foreach (var schemeProp in doc.ComponentsValue.SecuritySchemes.EnumerateObject())
        {
            OpenApiDocument.SecuritySchemeOrReference schemeOrRef = schemeProp.Value;
            OpenApiDocument.SecurityScheme scheme = OpenApiDocument.SecurityScheme.From(schemeOrRef);

            string schemeName = schemeProp.Name;
            string schemeType = scheme.Type.IsNotUndefined() ? scheme.Type.GetString()! : "unknown";
            bool isDeprecated = scheme.Deprecated.IsNotUndefined() && (bool)scheme.Deprecated;
            string? description = scheme.Description.IsNotUndefined() ? scheme.Description.GetString() : null;

            string? apiKeyName = null;
            string? apiKeyIn = null;
            string? httpScheme = null;
            string? bearerFormat = null;
            string? openIdConnectUrl = null;
            string? oauth2MetadataUrl = null;
            string? deviceAuthorizationUrl = null;

            if (string.Equals(schemeType, "apiKey", StringComparison.Ordinal))
            {
                apiKeyName = scheme.Name.IsNotUndefined() ? scheme.Name.GetString() : null;
                apiKeyIn = scheme.In.IsNotUndefined() ? scheme.In.GetString() : null;
            }
            else if (string.Equals(schemeType, "http", StringComparison.Ordinal))
            {
                httpScheme = scheme.Scheme.IsNotUndefined() ? scheme.Scheme.GetString() : null;
                bearerFormat = scheme.BearerFormat.IsNotUndefined() ? scheme.BearerFormat.GetString() : null;
            }
            else if (string.Equals(schemeType, "openIdConnect", StringComparison.Ordinal))
            {
                openIdConnectUrl = scheme.OpenIdConnectUrl.IsNotUndefined() ? scheme.OpenIdConnectUrl.GetString() : null;
            }
            else if (string.Equals(schemeType, "oauth2", StringComparison.Ordinal))
            {
                oauth2MetadataUrl = scheme.Oauth2MetadataUrl.IsNotUndefined() ? scheme.Oauth2MetadataUrl.GetString() : null;

                if (scheme.Flows.IsNotUndefined() && scheme.Flows.DeviceAuthorization.IsNotUndefined())
                {
                    var deviceFlow = scheme.Flows.DeviceAuthorization;
                    if (deviceFlow.DeviceAuthorizationUrl.IsNotUndefined())
                    {
                        deviceAuthorizationUrl = deviceFlow.DeviceAuthorizationUrl.GetString();
                    }
                }

                // Extract tokenUrl and authorizationUrl from the first flow that has them
                string? tokenUrl = null;
                string? authorizationUrl = null;
                HashSet<string> allScopes = [];

                if (scheme.Flows.IsNotUndefined())
                {
                    ExtractOAuth2FlowDetails(scheme.Flows, ref tokenUrl, ref authorizationUrl, allScopes);
                }

                result.Add(new SecuritySchemeInfo(
                    schemeName,
                    schemeType,
                    isDeprecated,
                    description,
                    apiKeyName,
                    apiKeyIn,
                    httpScheme,
                    bearerFormat,
                    openIdConnectUrl,
                    oauth2MetadataUrl,
                    deviceAuthorizationUrl,
                    tokenUrl,
                    authorizationUrl,
                    allScopes.Count > 0 ? [.. allScopes.Order(StringComparer.Ordinal)] : null));
                continue;
            }

            result.Add(new SecuritySchemeInfo(
                schemeName,
                schemeType,
                isDeprecated,
                description,
                apiKeyName,
                apiKeyIn,
                httpScheme,
                bearerFormat,
                openIdConnectUrl,
                oauth2MetadataUrl,
                deviceAuthorizationUrl));
        }

        return [.. result];
    }

    private static void ExtractOAuth2FlowDetails(
        OpenApiDocument.OauthFlows flows,
        ref string? tokenUrl,
        ref string? authorizationUrl,
        HashSet<string> allScopes)
    {
        // AuthorizationCode flow
        if (flows.AuthorizationCode.IsNotUndefined())
        {
            var flow = flows.AuthorizationCode;
            tokenUrl ??= flow.TokenUrl.IsNotUndefined() ? flow.TokenUrl.GetString() : null;
            authorizationUrl ??= flow.AuthorizationUrl.IsNotUndefined() ? flow.AuthorizationUrl.GetString() : null;
            CollectScopes(flow.Scopes, allScopes);
        }

        // ClientCredentials flow
        if (flows.ClientCredentials.IsNotUndefined())
        {
            var flow = flows.ClientCredentials;
            tokenUrl ??= flow.TokenUrl.IsNotUndefined() ? flow.TokenUrl.GetString() : null;
            CollectScopes(flow.Scopes, allScopes);
        }

        // Implicit flow
        if (flows.Implicit.IsNotUndefined())
        {
            var flow = flows.Implicit;
            authorizationUrl ??= flow.AuthorizationUrl.IsNotUndefined() ? flow.AuthorizationUrl.GetString() : null;
            CollectScopes(flow.Scopes, allScopes);
        }

        // Password flow
        if (flows.Password.IsNotUndefined())
        {
            var flow = flows.Password;
            tokenUrl ??= flow.TokenUrl.IsNotUndefined() ? flow.TokenUrl.GetString() : null;
            CollectScopes(flow.Scopes, allScopes);
        }

        // DeviceAuthorization flow
        if (flows.DeviceAuthorization.IsNotUndefined())
        {
            var flow = flows.DeviceAuthorization;
            tokenUrl ??= flow.TokenUrl.IsNotUndefined() ? flow.TokenUrl.GetString() : null;
            CollectScopes(flow.Scopes, allScopes);
        }
    }

    private static void CollectScopes(OpenApiDocument.MapOfStrings scopes, HashSet<string> allScopes)
    {
        if (scopes.IsNotUndefined())
        {
            foreach (var scopeProp in scopes.EnumerateObject())
            {
                allScopes.Add(scopeProp.Name);
            }
        }
    }
}