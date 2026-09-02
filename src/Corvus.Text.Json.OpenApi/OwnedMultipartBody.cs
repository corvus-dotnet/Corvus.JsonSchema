// <copyright file="OwnedMultipartBody.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// A parsed multipart body whose raw bytes remain valid until disposed, so binary
/// part slices can be handed to a handler without copying.
/// </summary>
/// <typeparam name="T">The JSON element type of the body's JSON projection.</typeparam>
/// <remarks>
/// <para>
/// Returned by
/// <see cref="MultipartFormDataSerializer.DeserializeOwnedAsync{T}(System.IO.Stream, string?, MultipartFormReader.BinaryPartHandler?, long, System.Threading.CancellationToken)"/>
/// and
/// <see cref="MultipartMixedSerializer.DeserializeOwnedAsync{T}(System.IO.Stream, string?, MultipartMixedReader.BinaryPartHandler?, long, System.Threading.CancellationToken)"/>.
/// The owner holds both the parsed JSON projection of the non-binary parts and the
/// rented buffer containing the whole multipart body, so slices recorded by a binary
/// part callback (via <c>BodyOffset</c> and the part length) stay valid until the
/// owner is disposed.
/// </para>
/// <para>
/// Dispose exactly once: disposing returns the body buffer to the pool and disposes
/// the document, invalidating <see cref="Document"/>, <see cref="BodyBytes"/> and
/// every slice taken from it.
/// </para>
/// </remarks>
public readonly struct OwnedMultipartBody<T> : IDisposable
    where T : struct, IJsonElement<T>
{
    private readonly byte[] bodyBuffer;
    private readonly int bodyLength;

    internal OwnedMultipartBody(ParsedJsonDocument<T> document, byte[] bodyBuffer, int bodyLength)
    {
        this.Document = document;
        this.bodyBuffer = bodyBuffer;
        this.bodyLength = bodyLength;
    }

    /// <summary>
    /// Gets the parsed JSON projection of the body's non-binary parts.
    /// </summary>
    public ParsedJsonDocument<T> Document { get; }

    /// <summary>
    /// Gets the raw bytes of the whole multipart body.
    /// </summary>
    public ReadOnlyMemory<byte> BodyBytes => this.bodyBuffer.AsMemory(0, this.bodyLength);

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Document.Dispose();
        FormFieldReader.Return(this.bodyBuffer);
    }
}