// <copyright file="ClientModel.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// The complete client model extracted from an OpenAPI or AsyncAPI specification.
/// This is the intermediate representation between the spec walker and the code emitter.
/// </summary>
/// <remarks>
/// <para>
/// The model stores <see cref="JsonElement"/> references into the parsed spec document.
/// No strings are allocated during model construction; UTF-16 conversion happens only
/// at the code-emitter boundary via the <c>Get*</c> methods on the model types.
/// </para>
/// <para>
/// All element references are valid only while the source document is alive. The caller
/// must keep the parsed document alive until the emitter has finished generating code.
/// </para>
/// </remarks>
public readonly struct ClientModel
{
    private static ReadOnlySpan<byte> InfoUtf8 => "info"u8;

    private static ReadOnlySpan<byte> TitleUtf8 => "title"u8;

    private static ReadOnlySpan<byte> VersionUtf8 => "version"u8;

    private static ReadOnlySpan<byte> DescriptionUtf8 => "description"u8;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientModel"/> struct.
    /// </summary>
    /// <param name="specRoot">The root element of the parsed spec document.</param>
    /// <param name="operations">The operations in the API.</param>
    /// <param name="schemaPointers">
    /// UTF-8 JSON pointers to all schemas reachable from the selected operations.
    /// These will be fed to the V5 <c>JsonSchemaTypeBuilder</c> for type generation.
    /// </param>
    public ClientModel(
        JsonElement specRoot,
        ClientOperation[] operations,
        byte[][] schemaPointers)
    {
        this.SpecRoot = specRoot;
        this.Operations = operations;
        this.SchemaPointers = schemaPointers;
    }

    /// <summary>
    /// Gets the root element of the parsed spec document.
    /// </summary>
    public JsonElement SpecRoot { get; }

    /// <summary>
    /// Gets the operations in the API.
    /// </summary>
    public ClientOperation[] Operations { get; }

    /// <summary>
    /// Gets the UTF-8 JSON pointers to all schemas reachable from the selected operations.
    /// </summary>
    public byte[][] SchemaPointers { get; }

    /// <summary>
    /// Gets the API title from the spec's info object.
    /// </summary>
    /// <returns>The title, or <see langword="null"/> if not present.</returns>
    /// <remarks>This allocates a string. Call only at the code-emission boundary.</remarks>
    public string? GetTitle() => this.TryGetInfoString(TitleUtf8);

    /// <summary>
    /// Gets the API version from the spec's info object.
    /// </summary>
    /// <returns>The version, or <see langword="null"/> if not present.</returns>
    /// <remarks>This allocates a string. Call only at the code-emission boundary.</remarks>
    public string? GetVersion() => this.TryGetInfoString(VersionUtf8);

    /// <summary>
    /// Gets the API description from the spec's info object.
    /// </summary>
    /// <returns>The description, or <see langword="null"/> if not present.</returns>
    /// <remarks>This allocates a string. Call only at the code-emission boundary.</remarks>
    public string? GetDescription() => this.TryGetInfoString(DescriptionUtf8);

    private string? TryGetInfoString(ReadOnlySpan<byte> property)
    {
        if (this.SpecRoot.TryGetProperty(InfoUtf8, out JsonElement info)
            && info.ValueKind == JsonValueKind.Object
            && info.TryGetProperty(property, out JsonElement value)
            && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }
}