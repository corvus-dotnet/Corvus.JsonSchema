// <copyright file="SecuritySchemeReader.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Operations;

/// <summary>
/// Reads a document's security schemes into <see cref="SecuritySchemeInfo"/> records, from the raw document, for the
/// OpenAPI versions whose typed document models do not prepare them themselves.
/// </summary>
public static class SecuritySchemeReader
{
    /// <summary>
    /// Reads <c>components.securitySchemes</c> of an OpenAPI 3.x document.
    /// </summary>
    /// <param name="specRoot">The root element of the document.</param>
    /// <returns>The schemes in document order; empty when the document declares none.</returns>
    public static SecuritySchemeInfo[] ReadOpenApi3(JsonElement specRoot)
    {
        if (specRoot.ValueKind != JsonValueKind.Object
            || !specRoot.TryGetProperty("components"u8, out JsonElement components)
            || components.ValueKind != JsonValueKind.Object
            || !components.TryGetProperty("securitySchemes"u8, out JsonElement schemes)
            || schemes.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        List<SecuritySchemeInfo> result = [];
        foreach (var schemeProperty in schemes.EnumerateObject())
        {
            JsonElement scheme = schemeProperty.Value;
            if (scheme.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string schemeType = SpecificationInfoReader.ReadString(scheme, "type"u8) ?? "unknown";
            string? apiKeyName = null;
            string? apiKeyIn = null;
            string? httpScheme = null;
            string? bearerFormat = null;
            string? openIdConnectUrl = null;
            string? oauth2MetadataUrl = null;
            string? deviceAuthorizationUrl = null;
            string? tokenUrl = null;
            string? authorizationUrl = null;
            string[]? availableScopes = null;

            switch (schemeType)
            {
                case "apiKey":
                    apiKeyName = SpecificationInfoReader.ReadString(scheme, "name"u8);
                    apiKeyIn = SpecificationInfoReader.ReadString(scheme, "in"u8);
                    break;
                case "http":
                    httpScheme = SpecificationInfoReader.ReadString(scheme, "scheme"u8);
                    bearerFormat = SpecificationInfoReader.ReadString(scheme, "bearerFormat"u8);
                    break;
                case "openIdConnect":
                    openIdConnectUrl = SpecificationInfoReader.ReadString(scheme, "openIdConnectUrl"u8);
                    break;
                case "oauth2":
                    oauth2MetadataUrl = SpecificationInfoReader.ReadString(scheme, "oauth2MetadataUrl"u8);
                    HashSet<string> scopes = new(StringComparer.Ordinal);
                    if (scheme.TryGetProperty("flows"u8, out JsonElement flows) && flows.ValueKind == JsonValueKind.Object)
                    {
                        // The same order as the OpenAPI 3.2 generator: the first flow that has a URL supplies it.
                        ReadFlow(flows, "authorizationCode"u8, ref tokenUrl, ref authorizationUrl, scopes);
                        ReadFlow(flows, "clientCredentials"u8, ref tokenUrl, ref authorizationUrl, scopes);
                        ReadFlow(flows, "implicit"u8, ref tokenUrl, ref authorizationUrl, scopes);
                        ReadFlow(flows, "password"u8, ref tokenUrl, ref authorizationUrl, scopes);
                        if (flows.TryGetProperty("deviceAuthorization"u8, out JsonElement deviceFlow) && deviceFlow.ValueKind == JsonValueKind.Object)
                        {
                            deviceAuthorizationUrl = SpecificationInfoReader.ReadString(deviceFlow, "deviceAuthorizationUrl"u8);
                        }

                        ReadFlow(flows, "deviceAuthorization"u8, ref tokenUrl, ref authorizationUrl, scopes);
                    }

                    availableScopes = [.. scopes];
                    break;
                default:
                    break;
            }

            result.Add(new SecuritySchemeInfo(
                schemeProperty.Name,
                schemeType,
                IsTrue(scheme, "deprecated"u8),
                SpecificationInfoReader.ReadString(scheme, "description"u8),
                apiKeyName,
                apiKeyIn,
                httpScheme,
                bearerFormat,
                openIdConnectUrl,
                oauth2MetadataUrl,
                deviceAuthorizationUrl,
                tokenUrl,
                authorizationUrl,
                availableScopes));
        }

        return [.. result];
    }

    /// <summary>
    /// Reads <c>securityDefinitions</c> of an OpenAPI 2.0 document. A <c>basic</c> definition is reported with the
    /// scheme type <c>basic</c> and <see cref="SecuritySchemeInfo.HttpScheme"/> <c>basic</c>; an <c>oauth2</c>
    /// definition's single <c>flow</c> supplies the URLs and scopes.
    /// </summary>
    /// <param name="specRoot">The root element of the document.</param>
    /// <returns>The definitions in document order; empty when the document declares none.</returns>
    public static SecuritySchemeInfo[] ReadOpenApi2(JsonElement specRoot)
    {
        if (specRoot.ValueKind != JsonValueKind.Object
            || !specRoot.TryGetProperty("securityDefinitions"u8, out JsonElement definitions)
            || definitions.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        List<SecuritySchemeInfo> result = [];
        foreach (var definitionProperty in definitions.EnumerateObject())
        {
            JsonElement definition = definitionProperty.Value;
            if (definition.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string schemeType = SpecificationInfoReader.ReadString(definition, "type"u8) ?? "unknown";
            string? apiKeyName = null;
            string? apiKeyIn = null;
            string? httpScheme = null;
            string? tokenUrl = null;
            string? authorizationUrl = null;
            string[]? availableScopes = null;

            switch (schemeType)
            {
                case "apiKey":
                    apiKeyName = SpecificationInfoReader.ReadString(definition, "name"u8);
                    apiKeyIn = SpecificationInfoReader.ReadString(definition, "in"u8);
                    break;
                case "basic":
                    httpScheme = "basic";
                    break;
                case "oauth2":
                    tokenUrl = SpecificationInfoReader.ReadString(definition, "tokenUrl"u8);
                    authorizationUrl = SpecificationInfoReader.ReadString(definition, "authorizationUrl"u8);
                    HashSet<string> scopes = new(StringComparer.Ordinal);
                    CollectScopes(definition, scopes);
                    availableScopes = [.. scopes];
                    break;
                default:
                    break;
            }

            result.Add(new SecuritySchemeInfo(
                definitionProperty.Name,
                schemeType,
                false,
                SpecificationInfoReader.ReadString(definition, "description"u8),
                apiKeyName,
                apiKeyIn,
                httpScheme,
                null,
                null,
                null,
                null,
                tokenUrl,
                authorizationUrl,
                availableScopes));
        }

        return [.. result];
    }

    private static void ReadFlow(JsonElement flows, ReadOnlySpan<byte> flowName, ref string? tokenUrl, ref string? authorizationUrl, HashSet<string> scopes)
    {
        if (!flows.TryGetProperty(flowName, out JsonElement flow) || flow.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        tokenUrl ??= SpecificationInfoReader.ReadString(flow, "tokenUrl"u8);
        authorizationUrl ??= SpecificationInfoReader.ReadString(flow, "authorizationUrl"u8);
        CollectScopes(flow, scopes);
    }

    private static void CollectScopes(JsonElement owner, HashSet<string> scopes)
    {
        if (owner.TryGetProperty("scopes"u8, out JsonElement scopeMap) && scopeMap.ValueKind == JsonValueKind.Object)
        {
            foreach (var scope in scopeMap.EnumerateObject())
            {
                scopes.Add(scope.Name);
            }
        }
    }

    private static bool IsTrue(JsonElement element, ReadOnlySpan<byte> name)
        => element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.True;
}