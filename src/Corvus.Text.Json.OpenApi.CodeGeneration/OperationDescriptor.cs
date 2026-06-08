// <copyright file="OperationDescriptor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// A fully-resolved description of a generated operation: its identity, the generated
/// request/response type names, and the request parameters — everything a downstream generator (such
/// as the Arazzo workflow generator) needs to call the operation, taken verbatim from the generator
/// so the convention is never re-derived. Unlike <see cref="OperationSummary"/> this is produced by a
/// generator <em>instance</em>, because the parameter and type names depend on the schema type map
/// and root namespace the instance was configured with.
/// </summary>
/// <param name="Path">The API path template (e.g. <c>/pets/{petId}</c>).</param>
/// <param name="Method">The HTTP method.</param>
/// <param name="OperationId">The <c>operationId</c>, or <see langword="null"/> if not specified.</param>
/// <param name="MethodName">The generated method name (PascalCase, without the <c>Async</c> suffix).</param>
/// <param name="RequestTypeName">The fully-qualified generated request type name.</param>
/// <param name="ResponseTypeName">The fully-qualified generated response type name.</param>
/// <param name="RequestParameters">The request parameters, in document order.</param>
/// <param name="HasRequestBody">Whether the operation declares a request body.</param>
/// <param name="Responses">The operation's responses, in document order.</param>
/// <param name="ClientTypeName">
/// The fully-qualified type of the generated client class that exposes this operation (e.g.
/// <c>Acme.Pets.PetsClient</c>). An operation grouped under several tags is exposed on a client per
/// tag; this is the canonical client (the operation's first tag, or the default client when it has
/// none). Constructed with a single <c>IApiTransport</c> argument.
/// </param>
/// <param name="ClientMethodName">
/// The name of the generated client method that invokes this operation (e.g.
/// <c>GetPetByIdAsync</c>) — the <see cref="MethodName"/> with the <c>Async</c> suffix the generator
/// actually emitted. It builds and validates the request, sends it, and validates the response,
/// returning the generated response type.
/// </param>
/// <param name="RequestBodyTypeName">
/// The fully-qualified generated type whose <c>.Source</c> is the client method's <c>body</c>
/// parameter (so a caller binds a body via <c>{RequestBodyTypeName}.From(source)</c>), or
/// <see langword="null"/> when the operation has no JSON request body (no body, or a raw stream body).
/// </param>
/// <param name="ResponseHeaders">
/// The response headers the operation declares, each described by the generated response property that
/// exposes it — so a caller can resolve <c>$response.header.&lt;name&gt;</c> against the response object
/// without introspecting the emitted struct. <see langword="null"/> (or empty) when the operation
/// declares no response headers. Deduplicated by generated property name across all responses (the
/// generated response struct flattens headers, so one property serves every status that declares it).
/// </param>
public readonly record struct OperationDescriptor(
    string Path,
    OperationMethod Method,
    string? OperationId,
    string MethodName,
    string RequestTypeName,
    string ResponseTypeName,
    IReadOnlyList<RequestParameterInfo> RequestParameters,
    bool HasRequestBody,
    IReadOnlyList<ResponseDescriptor> Responses,
    string ClientTypeName,
    string ClientMethodName,
    string? RequestBodyTypeName,
    IReadOnlyList<ResponseHeaderInfo>? ResponseHeaders = null);

/// <summary>
/// A response of a generated operation, described by the generated type of its JSON body — so a
/// caller can infer the type of a value projected from <c>$response.body</c> without introspecting
/// the emitted struct.
/// </summary>
/// <param name="StatusCode">The response status code (e.g. <c>200</c>) or <c>default</c>.</param>
/// <param name="BodyTypeName">The fully-qualified generated type of the JSON response body, or <see langword="null"/> if the response has no JSON body.</param>
/// <param name="BodyPropertyName">The name of the generated response property that holds the JSON body (e.g. <c>OkBody</c>), or <see langword="null"/> if the response has no JSON body.</param>
public readonly record struct ResponseDescriptor(
    string StatusCode,
    string? BodyTypeName,
    string? BodyPropertyName);

/// <summary>
/// A response header of a generated operation, described by the generated response property that
/// exposes it — so a caller can resolve <c>$response.header.&lt;name&gt;</c> without re-deriving the
/// header-to-property naming convention.
/// </summary>
/// <param name="HeaderName">The OpenAPI header name (e.g. <c>X-Total-Count</c>).</param>
/// <param name="PropertyName">The generated response property that returns the header value (e.g. <c>XTotalCountHeader</c>).</param>
/// <param name="TypeName">The fully-qualified type of that property — the header's generated schema type, or <c>string</c> for a header with no schema.</param>
/// <param name="IsString">Whether the property is a plain <c>string?</c> (a header with no schema) rather than a generated JSON type.</param>
public readonly record struct ResponseHeaderInfo(
    string HeaderName,
    string PropertyName,
    string TypeName,
    bool IsString);

/// <summary>
/// A request parameter of a generated operation, described by the names and type the generator
/// actually emitted for it — so a caller can bind a value to the right request property via
/// <c>TTarget.From(source)</c> without re-deriving the naming or type convention.
/// </summary>
/// <param name="Name">The OpenAPI parameter name.</param>
/// <param name="Location">The parameter location (path, query, header, or cookie).</param>
/// <param name="PropertyName">The name of the generated request property (e.g. <c>PetId</c>).</param>
/// <param name="TypeName">The fully-qualified type of the generated request property (e.g. <c>Acme.Pets.Models.JsonInt64</c>).</param>
/// <param name="IsRequired">Whether the parameter is required (and therefore a constructor argument).</param>
/// <param name="ParameterName">
/// The C# identifier the generator emitted for this parameter on the client method (e.g. <c>petId</c>,
/// keyword-escaped where necessary) — so a caller can pass it as a named argument without re-deriving
/// the naming convention.
/// </param>
public readonly record struct RequestParameterInfo(
    string Name,
    ParameterLocation Location,
    string PropertyName,
    string TypeName,
    bool IsRequired,
    string ParameterName);