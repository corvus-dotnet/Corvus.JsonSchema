// <copyright file="InterfaceMetadataTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable CS0618 // Obsolete members are tested intentionally

using CanonTests32.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.OpenApi32.Runtime.Tests;

/// <summary>
/// Tests that exercise static interface members: DocumentIdentityUri, CreateServerUri,
/// and SecuritySchemes properties on all generated client interfaces.
/// </summary>
[TestClass]
public class InterfaceMetadataTests
{
    private const string ExpectedDocUri = "https://api.example.com/specs/runtime-tests.json";
    private static readonly Uri ExpectedServerUri = new("https://api.example.com/v1");

    // --- DocumentIdentityUri ---
    [TestMethod]
    public void IApiItemsClient_DocumentIdentityUri() => Assert.AreEqual(ExpectedDocUri, IApiItemsClient.DocumentIdentityUri);

    [TestMethod]
    public void IApiComplexClient_DocumentIdentityUri() => Assert.AreEqual(ExpectedDocUri, IApiComplexClient.DocumentIdentityUri);

    [TestMethod]
    public void IApiComplexParamsClient_DocumentIdentityUri() => Assert.AreEqual(ExpectedDocUri, IApiComplexParamsClient.DocumentIdentityUri);

    [TestMethod]
    public void IApiDocsClient_DocumentIdentityUri() => Assert.AreEqual(ExpectedDocUri, IApiDocsClient.DocumentIdentityUri);

    [TestMethod]
    public void IApiDocumentsClient_DocumentIdentityUri() => Assert.AreEqual(ExpectedDocUri, IApiDocumentsClient.DocumentIdentityUri);

    [TestMethod]
    public void IApiFilesClient_DocumentIdentityUri() => Assert.AreEqual(ExpectedDocUri, IApiFilesClient.DocumentIdentityUri);

    [TestMethod]
    public void IApiFlagsClient_DocumentIdentityUri() => Assert.AreEqual(ExpectedDocUri, IApiFlagsClient.DocumentIdentityUri);

    [TestMethod]
    public void IApiFormsClient_DocumentIdentityUri() => Assert.AreEqual(ExpectedDocUri, IApiFormsClient.DocumentIdentityUri);

    [TestMethod]
    public void IApiOrdersClient_DocumentIdentityUri() => Assert.AreEqual(ExpectedDocUri, IApiOrdersClient.DocumentIdentityUri);

    [TestMethod]
    public void IApiPagesClient_DocumentIdentityUri() => Assert.AreEqual(ExpectedDocUri, IApiPagesClient.DocumentIdentityUri);

    [TestMethod]
    public void IApiPreferencesClient_DocumentIdentityUri() => Assert.AreEqual(ExpectedDocUri, IApiPreferencesClient.DocumentIdentityUri);

    [TestMethod]
    public void IApiSearchClient_DocumentIdentityUri() => Assert.AreEqual(ExpectedDocUri, IApiSearchClient.DocumentIdentityUri);

    [TestMethod]
    public void IApiSessionClient_DocumentIdentityUri() => Assert.AreEqual(ExpectedDocUri, IApiSessionClient.DocumentIdentityUri);

    [TestMethod]
    public void IApiStreamingClient_DocumentIdentityUri() => Assert.AreEqual(ExpectedDocUri, IApiStreamingClient.DocumentIdentityUri);

    [TestMethod]
    public void IApiTextClient_DocumentIdentityUri() => Assert.AreEqual(ExpectedDocUri, IApiTextClient.DocumentIdentityUri);

    [TestMethod]
    public void IApiTrackingClient_DocumentIdentityUri() => Assert.AreEqual(ExpectedDocUri, IApiTrackingClient.DocumentIdentityUri);

    // --- CreateServerUri ---
    [TestMethod]
    public void IApiItemsClient_CreateServerUri() => Assert.AreEqual(ExpectedServerUri, IApiItemsClient.CreateServerUri());

    [TestMethod]
    public void IApiComplexClient_CreateServerUri() => Assert.AreEqual(ExpectedServerUri, IApiComplexClient.CreateServerUri());

    [TestMethod]
    public void IApiComplexParamsClient_CreateServerUri() => Assert.AreEqual(ExpectedServerUri, IApiComplexParamsClient.CreateServerUri());

    [TestMethod]
    public void IApiDocsClient_CreateServerUri() => Assert.AreEqual(ExpectedServerUri, IApiDocsClient.CreateServerUri());

    [TestMethod]
    public void IApiDocumentsClient_CreateServerUri() => Assert.AreEqual(ExpectedServerUri, IApiDocumentsClient.CreateServerUri());

    [TestMethod]
    public void IApiFilesClient_CreateServerUri() => Assert.AreEqual(ExpectedServerUri, IApiFilesClient.CreateServerUri());

    [TestMethod]
    public void IApiFlagsClient_CreateServerUri() => Assert.AreEqual(ExpectedServerUri, IApiFlagsClient.CreateServerUri());

    [TestMethod]
    public void IApiFormsClient_CreateServerUri() => Assert.AreEqual(ExpectedServerUri, IApiFormsClient.CreateServerUri());

    [TestMethod]
    public void IApiOrdersClient_CreateServerUri() => Assert.AreEqual(ExpectedServerUri, IApiOrdersClient.CreateServerUri());

    [TestMethod]
    public void IApiPagesClient_CreateServerUri() => Assert.AreEqual(ExpectedServerUri, IApiPagesClient.CreateServerUri());

    [TestMethod]
    public void IApiPreferencesClient_CreateServerUri() => Assert.AreEqual(ExpectedServerUri, IApiPreferencesClient.CreateServerUri());

    [TestMethod]
    public void IApiSearchClient_CreateServerUri() => Assert.AreEqual(ExpectedServerUri, IApiSearchClient.CreateServerUri());

    [TestMethod]
    public void IApiSessionClient_CreateServerUri() => Assert.AreEqual(ExpectedServerUri, IApiSessionClient.CreateServerUri());

    [TestMethod]
    public void IApiStreamingClient_CreateServerUri() => Assert.AreEqual(ExpectedServerUri, IApiStreamingClient.CreateServerUri());

    [TestMethod]
    public void IApiTextClient_CreateServerUri() => Assert.AreEqual(ExpectedServerUri, IApiTextClient.CreateServerUri());

    [TestMethod]
    public void IApiTrackingClient_CreateServerUri() => Assert.AreEqual(ExpectedServerUri, IApiTrackingClient.CreateServerUri());

    // --- SecuritySchemes (each interface has its own nested class; must access all for full coverage) ---
    [TestMethod]
    public void SecuritySchemes_Items_AllProperties()
    {
        Assert.AreEqual("bearerAuth", IApiItemsClient.SecuritySchemes.BearerAuthName);
        Assert.AreEqual("http", IApiItemsClient.SecuritySchemes.BearerAuthType);
        Assert.AreEqual("bearer", IApiItemsClient.SecuritySchemes.BearerAuthScheme);
        Assert.AreEqual("apiKeyAuth", IApiItemsClient.SecuritySchemes.ApiKeyAuthName);
        Assert.AreEqual("apiKey", IApiItemsClient.SecuritySchemes.ApiKeyAuthType);
        Assert.AreEqual("X-API-Key", IApiItemsClient.SecuritySchemes.ApiKeyAuthKeyName);
        Assert.AreEqual("header", IApiItemsClient.SecuritySchemes.ApiKeyAuthKeyLocation);
        Assert.AreEqual("legacyApiKey", IApiItemsClient.SecuritySchemes.LegacyApiKeyName);
        Assert.AreEqual("apiKey", IApiItemsClient.SecuritySchemes.LegacyApiKeyType);
        Assert.AreEqual("api_key", IApiItemsClient.SecuritySchemes.LegacyApiKeyKeyName);
        Assert.AreEqual("query", IApiItemsClient.SecuritySchemes.LegacyApiKeyKeyLocation);
        Assert.AreEqual("oauth2", IApiItemsClient.SecuritySchemes.Oauth2Name);
        Assert.AreEqual("oauth2", IApiItemsClient.SecuritySchemes.Oauth2Type);
        Assert.AreEqual("https://auth.example.com/.well-known/oauth-authorization-server", IApiItemsClient.SecuritySchemes.Oauth2Oauth2MetadataUrl);
        Assert.AreEqual("https://auth.example.com/device", IApiItemsClient.SecuritySchemes.Oauth2DeviceAuthorizationUrl);
    }

    [TestMethod]
    public void SecuritySchemes_Complex_AllProperties()
    {
        Assert.AreEqual("bearerAuth", IApiComplexClient.SecuritySchemes.BearerAuthName);
        Assert.AreEqual("http", IApiComplexClient.SecuritySchemes.BearerAuthType);
        Assert.AreEqual("bearer", IApiComplexClient.SecuritySchemes.BearerAuthScheme);
        Assert.AreEqual("apiKeyAuth", IApiComplexClient.SecuritySchemes.ApiKeyAuthName);
        Assert.AreEqual("apiKey", IApiComplexClient.SecuritySchemes.ApiKeyAuthType);
        Assert.AreEqual("X-API-Key", IApiComplexClient.SecuritySchemes.ApiKeyAuthKeyName);
        Assert.AreEqual("header", IApiComplexClient.SecuritySchemes.ApiKeyAuthKeyLocation);
        Assert.AreEqual("legacyApiKey", IApiComplexClient.SecuritySchemes.LegacyApiKeyName);
        Assert.AreEqual("apiKey", IApiComplexClient.SecuritySchemes.LegacyApiKeyType);
        Assert.AreEqual("api_key", IApiComplexClient.SecuritySchemes.LegacyApiKeyKeyName);
        Assert.AreEqual("query", IApiComplexClient.SecuritySchemes.LegacyApiKeyKeyLocation);
        Assert.AreEqual("oauth2", IApiComplexClient.SecuritySchemes.Oauth2Name);
        Assert.AreEqual("oauth2", IApiComplexClient.SecuritySchemes.Oauth2Type);
        Assert.AreEqual("https://auth.example.com/.well-known/oauth-authorization-server", IApiComplexClient.SecuritySchemes.Oauth2Oauth2MetadataUrl);
        Assert.AreEqual("https://auth.example.com/device", IApiComplexClient.SecuritySchemes.Oauth2DeviceAuthorizationUrl);
    }

    [TestMethod]
    public void SecuritySchemes_ComplexParams_AllProperties()
    {
        Assert.AreEqual("bearerAuth", IApiComplexParamsClient.SecuritySchemes.BearerAuthName);
        Assert.AreEqual("http", IApiComplexParamsClient.SecuritySchemes.BearerAuthType);
        Assert.AreEqual("bearer", IApiComplexParamsClient.SecuritySchemes.BearerAuthScheme);
        Assert.AreEqual("apiKeyAuth", IApiComplexParamsClient.SecuritySchemes.ApiKeyAuthName);
        Assert.AreEqual("apiKey", IApiComplexParamsClient.SecuritySchemes.ApiKeyAuthType);
        Assert.AreEqual("X-API-Key", IApiComplexParamsClient.SecuritySchemes.ApiKeyAuthKeyName);
        Assert.AreEqual("header", IApiComplexParamsClient.SecuritySchemes.ApiKeyAuthKeyLocation);
        Assert.AreEqual("legacyApiKey", IApiComplexParamsClient.SecuritySchemes.LegacyApiKeyName);
        Assert.AreEqual("apiKey", IApiComplexParamsClient.SecuritySchemes.LegacyApiKeyType);
        Assert.AreEqual("api_key", IApiComplexParamsClient.SecuritySchemes.LegacyApiKeyKeyName);
        Assert.AreEqual("query", IApiComplexParamsClient.SecuritySchemes.LegacyApiKeyKeyLocation);
        Assert.AreEqual("oauth2", IApiComplexParamsClient.SecuritySchemes.Oauth2Name);
        Assert.AreEqual("oauth2", IApiComplexParamsClient.SecuritySchemes.Oauth2Type);
        Assert.AreEqual("https://auth.example.com/.well-known/oauth-authorization-server", IApiComplexParamsClient.SecuritySchemes.Oauth2Oauth2MetadataUrl);
        Assert.AreEqual("https://auth.example.com/device", IApiComplexParamsClient.SecuritySchemes.Oauth2DeviceAuthorizationUrl);
    }

    [TestMethod]
    public void SecuritySchemes_Docs_AllProperties()
    {
        Assert.AreEqual("bearerAuth", IApiDocsClient.SecuritySchemes.BearerAuthName);
        Assert.AreEqual("http", IApiDocsClient.SecuritySchemes.BearerAuthType);
        Assert.AreEqual("bearer", IApiDocsClient.SecuritySchemes.BearerAuthScheme);
        Assert.AreEqual("apiKeyAuth", IApiDocsClient.SecuritySchemes.ApiKeyAuthName);
        Assert.AreEqual("apiKey", IApiDocsClient.SecuritySchemes.ApiKeyAuthType);
        Assert.AreEqual("X-API-Key", IApiDocsClient.SecuritySchemes.ApiKeyAuthKeyName);
        Assert.AreEqual("header", IApiDocsClient.SecuritySchemes.ApiKeyAuthKeyLocation);
        Assert.AreEqual("legacyApiKey", IApiDocsClient.SecuritySchemes.LegacyApiKeyName);
        Assert.AreEqual("apiKey", IApiDocsClient.SecuritySchemes.LegacyApiKeyType);
        Assert.AreEqual("api_key", IApiDocsClient.SecuritySchemes.LegacyApiKeyKeyName);
        Assert.AreEqual("query", IApiDocsClient.SecuritySchemes.LegacyApiKeyKeyLocation);
        Assert.AreEqual("oauth2", IApiDocsClient.SecuritySchemes.Oauth2Name);
        Assert.AreEqual("oauth2", IApiDocsClient.SecuritySchemes.Oauth2Type);
        Assert.AreEqual("https://auth.example.com/.well-known/oauth-authorization-server", IApiDocsClient.SecuritySchemes.Oauth2Oauth2MetadataUrl);
        Assert.AreEqual("https://auth.example.com/device", IApiDocsClient.SecuritySchemes.Oauth2DeviceAuthorizationUrl);
    }

    [TestMethod]
    public void SecuritySchemes_Documents_AllProperties()
    {
        Assert.AreEqual("bearerAuth", IApiDocumentsClient.SecuritySchemes.BearerAuthName);
        Assert.AreEqual("http", IApiDocumentsClient.SecuritySchemes.BearerAuthType);
        Assert.AreEqual("bearer", IApiDocumentsClient.SecuritySchemes.BearerAuthScheme);
        Assert.AreEqual("apiKeyAuth", IApiDocumentsClient.SecuritySchemes.ApiKeyAuthName);
        Assert.AreEqual("apiKey", IApiDocumentsClient.SecuritySchemes.ApiKeyAuthType);
        Assert.AreEqual("X-API-Key", IApiDocumentsClient.SecuritySchemes.ApiKeyAuthKeyName);
        Assert.AreEqual("header", IApiDocumentsClient.SecuritySchemes.ApiKeyAuthKeyLocation);
        Assert.AreEqual("legacyApiKey", IApiDocumentsClient.SecuritySchemes.LegacyApiKeyName);
        Assert.AreEqual("apiKey", IApiDocumentsClient.SecuritySchemes.LegacyApiKeyType);
        Assert.AreEqual("api_key", IApiDocumentsClient.SecuritySchemes.LegacyApiKeyKeyName);
        Assert.AreEqual("query", IApiDocumentsClient.SecuritySchemes.LegacyApiKeyKeyLocation);
        Assert.AreEqual("oauth2", IApiDocumentsClient.SecuritySchemes.Oauth2Name);
        Assert.AreEqual("oauth2", IApiDocumentsClient.SecuritySchemes.Oauth2Type);
        Assert.AreEqual("https://auth.example.com/.well-known/oauth-authorization-server", IApiDocumentsClient.SecuritySchemes.Oauth2Oauth2MetadataUrl);
        Assert.AreEqual("https://auth.example.com/device", IApiDocumentsClient.SecuritySchemes.Oauth2DeviceAuthorizationUrl);
    }

    [TestMethod]
    public void SecuritySchemes_Files_AllProperties()
    {
        Assert.AreEqual("bearerAuth", IApiFilesClient.SecuritySchemes.BearerAuthName);
        Assert.AreEqual("http", IApiFilesClient.SecuritySchemes.BearerAuthType);
        Assert.AreEqual("bearer", IApiFilesClient.SecuritySchemes.BearerAuthScheme);
        Assert.AreEqual("apiKeyAuth", IApiFilesClient.SecuritySchemes.ApiKeyAuthName);
        Assert.AreEqual("apiKey", IApiFilesClient.SecuritySchemes.ApiKeyAuthType);
        Assert.AreEqual("X-API-Key", IApiFilesClient.SecuritySchemes.ApiKeyAuthKeyName);
        Assert.AreEqual("header", IApiFilesClient.SecuritySchemes.ApiKeyAuthKeyLocation);
        Assert.AreEqual("legacyApiKey", IApiFilesClient.SecuritySchemes.LegacyApiKeyName);
        Assert.AreEqual("apiKey", IApiFilesClient.SecuritySchemes.LegacyApiKeyType);
        Assert.AreEqual("api_key", IApiFilesClient.SecuritySchemes.LegacyApiKeyKeyName);
        Assert.AreEqual("query", IApiFilesClient.SecuritySchemes.LegacyApiKeyKeyLocation);
        Assert.AreEqual("oauth2", IApiFilesClient.SecuritySchemes.Oauth2Name);
        Assert.AreEqual("oauth2", IApiFilesClient.SecuritySchemes.Oauth2Type);
        Assert.AreEqual("https://auth.example.com/.well-known/oauth-authorization-server", IApiFilesClient.SecuritySchemes.Oauth2Oauth2MetadataUrl);
        Assert.AreEqual("https://auth.example.com/device", IApiFilesClient.SecuritySchemes.Oauth2DeviceAuthorizationUrl);
    }

    [TestMethod]
    public void SecuritySchemes_Flags_AllProperties()
    {
        Assert.AreEqual("bearerAuth", IApiFlagsClient.SecuritySchemes.BearerAuthName);
        Assert.AreEqual("http", IApiFlagsClient.SecuritySchemes.BearerAuthType);
        Assert.AreEqual("bearer", IApiFlagsClient.SecuritySchemes.BearerAuthScheme);
        Assert.AreEqual("apiKeyAuth", IApiFlagsClient.SecuritySchemes.ApiKeyAuthName);
        Assert.AreEqual("apiKey", IApiFlagsClient.SecuritySchemes.ApiKeyAuthType);
        Assert.AreEqual("X-API-Key", IApiFlagsClient.SecuritySchemes.ApiKeyAuthKeyName);
        Assert.AreEqual("header", IApiFlagsClient.SecuritySchemes.ApiKeyAuthKeyLocation);
        Assert.AreEqual("legacyApiKey", IApiFlagsClient.SecuritySchemes.LegacyApiKeyName);
        Assert.AreEqual("apiKey", IApiFlagsClient.SecuritySchemes.LegacyApiKeyType);
        Assert.AreEqual("api_key", IApiFlagsClient.SecuritySchemes.LegacyApiKeyKeyName);
        Assert.AreEqual("query", IApiFlagsClient.SecuritySchemes.LegacyApiKeyKeyLocation);
        Assert.AreEqual("oauth2", IApiFlagsClient.SecuritySchemes.Oauth2Name);
        Assert.AreEqual("oauth2", IApiFlagsClient.SecuritySchemes.Oauth2Type);
        Assert.AreEqual("https://auth.example.com/.well-known/oauth-authorization-server", IApiFlagsClient.SecuritySchemes.Oauth2Oauth2MetadataUrl);
        Assert.AreEqual("https://auth.example.com/device", IApiFlagsClient.SecuritySchemes.Oauth2DeviceAuthorizationUrl);
    }

    [TestMethod]
    public void SecuritySchemes_Forms_AllProperties()
    {
        Assert.AreEqual("bearerAuth", IApiFormsClient.SecuritySchemes.BearerAuthName);
        Assert.AreEqual("http", IApiFormsClient.SecuritySchemes.BearerAuthType);
        Assert.AreEqual("bearer", IApiFormsClient.SecuritySchemes.BearerAuthScheme);
        Assert.AreEqual("apiKeyAuth", IApiFormsClient.SecuritySchemes.ApiKeyAuthName);
        Assert.AreEqual("apiKey", IApiFormsClient.SecuritySchemes.ApiKeyAuthType);
        Assert.AreEqual("X-API-Key", IApiFormsClient.SecuritySchemes.ApiKeyAuthKeyName);
        Assert.AreEqual("header", IApiFormsClient.SecuritySchemes.ApiKeyAuthKeyLocation);
        Assert.AreEqual("legacyApiKey", IApiFormsClient.SecuritySchemes.LegacyApiKeyName);
        Assert.AreEqual("apiKey", IApiFormsClient.SecuritySchemes.LegacyApiKeyType);
        Assert.AreEqual("api_key", IApiFormsClient.SecuritySchemes.LegacyApiKeyKeyName);
        Assert.AreEqual("query", IApiFormsClient.SecuritySchemes.LegacyApiKeyKeyLocation);
        Assert.AreEqual("oauth2", IApiFormsClient.SecuritySchemes.Oauth2Name);
        Assert.AreEqual("oauth2", IApiFormsClient.SecuritySchemes.Oauth2Type);
        Assert.AreEqual("https://auth.example.com/.well-known/oauth-authorization-server", IApiFormsClient.SecuritySchemes.Oauth2Oauth2MetadataUrl);
        Assert.AreEqual("https://auth.example.com/device", IApiFormsClient.SecuritySchemes.Oauth2DeviceAuthorizationUrl);
    }

    [TestMethod]
    public void SecuritySchemes_Orders_AllProperties()
    {
        Assert.AreEqual("bearerAuth", IApiOrdersClient.SecuritySchemes.BearerAuthName);
        Assert.AreEqual("http", IApiOrdersClient.SecuritySchemes.BearerAuthType);
        Assert.AreEqual("bearer", IApiOrdersClient.SecuritySchemes.BearerAuthScheme);
        Assert.AreEqual("apiKeyAuth", IApiOrdersClient.SecuritySchemes.ApiKeyAuthName);
        Assert.AreEqual("apiKey", IApiOrdersClient.SecuritySchemes.ApiKeyAuthType);
        Assert.AreEqual("X-API-Key", IApiOrdersClient.SecuritySchemes.ApiKeyAuthKeyName);
        Assert.AreEqual("header", IApiOrdersClient.SecuritySchemes.ApiKeyAuthKeyLocation);
        Assert.AreEqual("legacyApiKey", IApiOrdersClient.SecuritySchemes.LegacyApiKeyName);
        Assert.AreEqual("apiKey", IApiOrdersClient.SecuritySchemes.LegacyApiKeyType);
        Assert.AreEqual("api_key", IApiOrdersClient.SecuritySchemes.LegacyApiKeyKeyName);
        Assert.AreEqual("query", IApiOrdersClient.SecuritySchemes.LegacyApiKeyKeyLocation);
        Assert.AreEqual("oauth2", IApiOrdersClient.SecuritySchemes.Oauth2Name);
        Assert.AreEqual("oauth2", IApiOrdersClient.SecuritySchemes.Oauth2Type);
        Assert.AreEqual("https://auth.example.com/.well-known/oauth-authorization-server", IApiOrdersClient.SecuritySchemes.Oauth2Oauth2MetadataUrl);
        Assert.AreEqual("https://auth.example.com/device", IApiOrdersClient.SecuritySchemes.Oauth2DeviceAuthorizationUrl);
    }

    [TestMethod]
    public void SecuritySchemes_Pages_AllProperties()
    {
        Assert.AreEqual("bearerAuth", IApiPagesClient.SecuritySchemes.BearerAuthName);
        Assert.AreEqual("http", IApiPagesClient.SecuritySchemes.BearerAuthType);
        Assert.AreEqual("bearer", IApiPagesClient.SecuritySchemes.BearerAuthScheme);
        Assert.AreEqual("apiKeyAuth", IApiPagesClient.SecuritySchemes.ApiKeyAuthName);
        Assert.AreEqual("apiKey", IApiPagesClient.SecuritySchemes.ApiKeyAuthType);
        Assert.AreEqual("X-API-Key", IApiPagesClient.SecuritySchemes.ApiKeyAuthKeyName);
        Assert.AreEqual("header", IApiPagesClient.SecuritySchemes.ApiKeyAuthKeyLocation);
        Assert.AreEqual("legacyApiKey", IApiPagesClient.SecuritySchemes.LegacyApiKeyName);
        Assert.AreEqual("apiKey", IApiPagesClient.SecuritySchemes.LegacyApiKeyType);
        Assert.AreEqual("api_key", IApiPagesClient.SecuritySchemes.LegacyApiKeyKeyName);
        Assert.AreEqual("query", IApiPagesClient.SecuritySchemes.LegacyApiKeyKeyLocation);
        Assert.AreEqual("oauth2", IApiPagesClient.SecuritySchemes.Oauth2Name);
        Assert.AreEqual("oauth2", IApiPagesClient.SecuritySchemes.Oauth2Type);
        Assert.AreEqual("https://auth.example.com/.well-known/oauth-authorization-server", IApiPagesClient.SecuritySchemes.Oauth2Oauth2MetadataUrl);
        Assert.AreEqual("https://auth.example.com/device", IApiPagesClient.SecuritySchemes.Oauth2DeviceAuthorizationUrl);
    }

    [TestMethod]
    public void SecuritySchemes_Preferences_AllProperties()
    {
        Assert.AreEqual("bearerAuth", IApiPreferencesClient.SecuritySchemes.BearerAuthName);
        Assert.AreEqual("http", IApiPreferencesClient.SecuritySchemes.BearerAuthType);
        Assert.AreEqual("bearer", IApiPreferencesClient.SecuritySchemes.BearerAuthScheme);
        Assert.AreEqual("apiKeyAuth", IApiPreferencesClient.SecuritySchemes.ApiKeyAuthName);
        Assert.AreEqual("apiKey", IApiPreferencesClient.SecuritySchemes.ApiKeyAuthType);
        Assert.AreEqual("X-API-Key", IApiPreferencesClient.SecuritySchemes.ApiKeyAuthKeyName);
        Assert.AreEqual("header", IApiPreferencesClient.SecuritySchemes.ApiKeyAuthKeyLocation);
        Assert.AreEqual("legacyApiKey", IApiPreferencesClient.SecuritySchemes.LegacyApiKeyName);
        Assert.AreEqual("apiKey", IApiPreferencesClient.SecuritySchemes.LegacyApiKeyType);
        Assert.AreEqual("api_key", IApiPreferencesClient.SecuritySchemes.LegacyApiKeyKeyName);
        Assert.AreEqual("query", IApiPreferencesClient.SecuritySchemes.LegacyApiKeyKeyLocation);
        Assert.AreEqual("oauth2", IApiPreferencesClient.SecuritySchemes.Oauth2Name);
        Assert.AreEqual("oauth2", IApiPreferencesClient.SecuritySchemes.Oauth2Type);
        Assert.AreEqual("https://auth.example.com/.well-known/oauth-authorization-server", IApiPreferencesClient.SecuritySchemes.Oauth2Oauth2MetadataUrl);
        Assert.AreEqual("https://auth.example.com/device", IApiPreferencesClient.SecuritySchemes.Oauth2DeviceAuthorizationUrl);
    }

    [TestMethod]
    public void SecuritySchemes_Search_AllProperties()
    {
        Assert.AreEqual("bearerAuth", IApiSearchClient.SecuritySchemes.BearerAuthName);
        Assert.AreEqual("http", IApiSearchClient.SecuritySchemes.BearerAuthType);
        Assert.AreEqual("bearer", IApiSearchClient.SecuritySchemes.BearerAuthScheme);
        Assert.AreEqual("apiKeyAuth", IApiSearchClient.SecuritySchemes.ApiKeyAuthName);
        Assert.AreEqual("apiKey", IApiSearchClient.SecuritySchemes.ApiKeyAuthType);
        Assert.AreEqual("X-API-Key", IApiSearchClient.SecuritySchemes.ApiKeyAuthKeyName);
        Assert.AreEqual("header", IApiSearchClient.SecuritySchemes.ApiKeyAuthKeyLocation);
        Assert.AreEqual("legacyApiKey", IApiSearchClient.SecuritySchemes.LegacyApiKeyName);
        Assert.AreEqual("apiKey", IApiSearchClient.SecuritySchemes.LegacyApiKeyType);
        Assert.AreEqual("api_key", IApiSearchClient.SecuritySchemes.LegacyApiKeyKeyName);
        Assert.AreEqual("query", IApiSearchClient.SecuritySchemes.LegacyApiKeyKeyLocation);
        Assert.AreEqual("oauth2", IApiSearchClient.SecuritySchemes.Oauth2Name);
        Assert.AreEqual("oauth2", IApiSearchClient.SecuritySchemes.Oauth2Type);
        Assert.AreEqual("https://auth.example.com/.well-known/oauth-authorization-server", IApiSearchClient.SecuritySchemes.Oauth2Oauth2MetadataUrl);
        Assert.AreEqual("https://auth.example.com/device", IApiSearchClient.SecuritySchemes.Oauth2DeviceAuthorizationUrl);
    }

    [TestMethod]
    public void SecuritySchemes_Session_AllProperties()
    {
        Assert.AreEqual("bearerAuth", IApiSessionClient.SecuritySchemes.BearerAuthName);
        Assert.AreEqual("http", IApiSessionClient.SecuritySchemes.BearerAuthType);
        Assert.AreEqual("bearer", IApiSessionClient.SecuritySchemes.BearerAuthScheme);
        Assert.AreEqual("apiKeyAuth", IApiSessionClient.SecuritySchemes.ApiKeyAuthName);
        Assert.AreEqual("apiKey", IApiSessionClient.SecuritySchemes.ApiKeyAuthType);
        Assert.AreEqual("X-API-Key", IApiSessionClient.SecuritySchemes.ApiKeyAuthKeyName);
        Assert.AreEqual("header", IApiSessionClient.SecuritySchemes.ApiKeyAuthKeyLocation);
        Assert.AreEqual("legacyApiKey", IApiSessionClient.SecuritySchemes.LegacyApiKeyName);
        Assert.AreEqual("apiKey", IApiSessionClient.SecuritySchemes.LegacyApiKeyType);
        Assert.AreEqual("api_key", IApiSessionClient.SecuritySchemes.LegacyApiKeyKeyName);
        Assert.AreEqual("query", IApiSessionClient.SecuritySchemes.LegacyApiKeyKeyLocation);
        Assert.AreEqual("oauth2", IApiSessionClient.SecuritySchemes.Oauth2Name);
        Assert.AreEqual("oauth2", IApiSessionClient.SecuritySchemes.Oauth2Type);
        Assert.AreEqual("https://auth.example.com/.well-known/oauth-authorization-server", IApiSessionClient.SecuritySchemes.Oauth2Oauth2MetadataUrl);
        Assert.AreEqual("https://auth.example.com/device", IApiSessionClient.SecuritySchemes.Oauth2DeviceAuthorizationUrl);
    }

    [TestMethod]
    public void SecuritySchemes_Streaming_AllProperties()
    {
        Assert.AreEqual("bearerAuth", IApiStreamingClient.SecuritySchemes.BearerAuthName);
        Assert.AreEqual("http", IApiStreamingClient.SecuritySchemes.BearerAuthType);
        Assert.AreEqual("bearer", IApiStreamingClient.SecuritySchemes.BearerAuthScheme);
        Assert.AreEqual("apiKeyAuth", IApiStreamingClient.SecuritySchemes.ApiKeyAuthName);
        Assert.AreEqual("apiKey", IApiStreamingClient.SecuritySchemes.ApiKeyAuthType);
        Assert.AreEqual("X-API-Key", IApiStreamingClient.SecuritySchemes.ApiKeyAuthKeyName);
        Assert.AreEqual("header", IApiStreamingClient.SecuritySchemes.ApiKeyAuthKeyLocation);
        Assert.AreEqual("legacyApiKey", IApiStreamingClient.SecuritySchemes.LegacyApiKeyName);
        Assert.AreEqual("apiKey", IApiStreamingClient.SecuritySchemes.LegacyApiKeyType);
        Assert.AreEqual("api_key", IApiStreamingClient.SecuritySchemes.LegacyApiKeyKeyName);
        Assert.AreEqual("query", IApiStreamingClient.SecuritySchemes.LegacyApiKeyKeyLocation);
        Assert.AreEqual("oauth2", IApiStreamingClient.SecuritySchemes.Oauth2Name);
        Assert.AreEqual("oauth2", IApiStreamingClient.SecuritySchemes.Oauth2Type);
        Assert.AreEqual("https://auth.example.com/.well-known/oauth-authorization-server", IApiStreamingClient.SecuritySchemes.Oauth2Oauth2MetadataUrl);
        Assert.AreEqual("https://auth.example.com/device", IApiStreamingClient.SecuritySchemes.Oauth2DeviceAuthorizationUrl);
    }

    [TestMethod]
    public void SecuritySchemes_Text_AllProperties()
    {
        Assert.AreEqual("bearerAuth", IApiTextClient.SecuritySchemes.BearerAuthName);
        Assert.AreEqual("http", IApiTextClient.SecuritySchemes.BearerAuthType);
        Assert.AreEqual("bearer", IApiTextClient.SecuritySchemes.BearerAuthScheme);
        Assert.AreEqual("apiKeyAuth", IApiTextClient.SecuritySchemes.ApiKeyAuthName);
        Assert.AreEqual("apiKey", IApiTextClient.SecuritySchemes.ApiKeyAuthType);
        Assert.AreEqual("X-API-Key", IApiTextClient.SecuritySchemes.ApiKeyAuthKeyName);
        Assert.AreEqual("header", IApiTextClient.SecuritySchemes.ApiKeyAuthKeyLocation);
        Assert.AreEqual("legacyApiKey", IApiTextClient.SecuritySchemes.LegacyApiKeyName);
        Assert.AreEqual("apiKey", IApiTextClient.SecuritySchemes.LegacyApiKeyType);
        Assert.AreEqual("api_key", IApiTextClient.SecuritySchemes.LegacyApiKeyKeyName);
        Assert.AreEqual("query", IApiTextClient.SecuritySchemes.LegacyApiKeyKeyLocation);
        Assert.AreEqual("oauth2", IApiTextClient.SecuritySchemes.Oauth2Name);
        Assert.AreEqual("oauth2", IApiTextClient.SecuritySchemes.Oauth2Type);
        Assert.AreEqual("https://auth.example.com/.well-known/oauth-authorization-server", IApiTextClient.SecuritySchemes.Oauth2Oauth2MetadataUrl);
        Assert.AreEqual("https://auth.example.com/device", IApiTextClient.SecuritySchemes.Oauth2DeviceAuthorizationUrl);
    }

    [TestMethod]
    public void SecuritySchemes_Tracking_AllProperties()
    {
        Assert.AreEqual("bearerAuth", IApiTrackingClient.SecuritySchemes.BearerAuthName);
        Assert.AreEqual("http", IApiTrackingClient.SecuritySchemes.BearerAuthType);
        Assert.AreEqual("bearer", IApiTrackingClient.SecuritySchemes.BearerAuthScheme);
        Assert.AreEqual("apiKeyAuth", IApiTrackingClient.SecuritySchemes.ApiKeyAuthName);
        Assert.AreEqual("apiKey", IApiTrackingClient.SecuritySchemes.ApiKeyAuthType);
        Assert.AreEqual("X-API-Key", IApiTrackingClient.SecuritySchemes.ApiKeyAuthKeyName);
        Assert.AreEqual("header", IApiTrackingClient.SecuritySchemes.ApiKeyAuthKeyLocation);
        Assert.AreEqual("legacyApiKey", IApiTrackingClient.SecuritySchemes.LegacyApiKeyName);
        Assert.AreEqual("apiKey", IApiTrackingClient.SecuritySchemes.LegacyApiKeyType);
        Assert.AreEqual("api_key", IApiTrackingClient.SecuritySchemes.LegacyApiKeyKeyName);
        Assert.AreEqual("query", IApiTrackingClient.SecuritySchemes.LegacyApiKeyKeyLocation);
        Assert.AreEqual("oauth2", IApiTrackingClient.SecuritySchemes.Oauth2Name);
        Assert.AreEqual("oauth2", IApiTrackingClient.SecuritySchemes.Oauth2Type);
        Assert.AreEqual("https://auth.example.com/.well-known/oauth-authorization-server", IApiTrackingClient.SecuritySchemes.Oauth2Oauth2MetadataUrl);
        Assert.AreEqual("https://auth.example.com/device", IApiTrackingClient.SecuritySchemes.Oauth2DeviceAuthorizationUrl);
    }
}