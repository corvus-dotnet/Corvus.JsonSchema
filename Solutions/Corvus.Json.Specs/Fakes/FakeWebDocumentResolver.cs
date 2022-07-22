// <copyright file="FakeWebDocumentResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json;

/// <summary>
/// An <see cref="IDocumentResolver"/> that provides
/// documents based on a url.
/// </summary>
public class FakeWebDocumentResolver : IDocumentResolver
{
    private readonly string baseDirectory;
    private readonly Dictionary<string, JsonDocument> documents = new();
    private bool disposedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeWebDocumentResolver"/> class.
    /// </summary>
    /// <param name="baseDirectory">The base directory for the file system resolver.</param>
    public FakeWebDocumentResolver(string baseDirectory)
    {
        if (string.IsNullOrEmpty(baseDirectory))
        {
            throw new ArgumentException($"'{nameof(baseDirectory)}' cannot be null or empty", nameof(baseDirectory));
        }

        this.baseDirectory = baseDirectory;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeWebDocumentResolver"/> class.
    /// </summary>
    /// <remarks>The default base directory is <see cref="Environment.CurrentDirectory"/>.</remarks>
    public FakeWebDocumentResolver()
    {
        this.baseDirectory = Environment.CurrentDirectory;
    }

    /// <inheritdoc/>
    public bool AddDocument(string uri, JsonDocument document)
    {
        this.CheckDisposed();

        return this.documents.TryAdd(uri, document);
    }

    /// <inheritdoc/>
    public void Reset()
    {
        this.CheckDisposed();
        this.DisposeDocumentsAndClear();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        this.Dispose(disposing: true);
        System.GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public async Task<JsonElement?> TryResolve(JsonReference reference)
    {
        this.CheckDisposed();

        if (!IsMatchForFakeUri(reference))
        {
            return null;
        }

        string path = GetPath(reference);

        if (this.documents.TryGetValue(path, out JsonDocument? result))
        {
            if (JsonPointerUtilities.TryResolvePointer(result, reference.Fragment, out JsonElement? element))
            {
                return element;
            }

            return default;
        }

        try
        {
            using Stream stream = File.OpenRead(path);
            result = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
            return JsonPointerUtilities.ResolvePointer(result, reference.Fragment);
        }
        catch (Exception)
        {
            return default;
        }

        static bool IsMatchForFakeUri(JsonReference reference)
        {
            JsonReferenceBuilder builder = reference.AsBuilder();

            if (!builder.Host.SequenceEqual("localhost".AsSpan()) || !builder.Port.SequenceEqual("1234".AsSpan()))
            {
                return false;
            }

            return true;
        }

        string GetPath(JsonReference reference)
        {
            JsonReferenceBuilder builder = reference.AsBuilder();

            if (builder.Path[0] == '/')
            {
                return Path.Combine(this.baseDirectory, builder.Path[1..].ToString());
            }

            return Path.Combine(this.baseDirectory, builder.Path.ToString());
        }
    }

    /// <summary>
    /// Implements the dispose pattern.
    /// </summary>
    /// <param name="disposing">True if we are disposing.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this.disposedValue)
        {
            if (disposing)
            {
                this.DisposeDocumentsAndClear();
            }

            this.disposedValue = true;
        }
    }

    private void DisposeDocumentsAndClear()
    {
        foreach (KeyValuePair<string, JsonDocument> document in this.documents)
        {
            document.Value.Dispose();
        }

        this.documents.Clear();
    }

    private void CheckDisposed()
    {
        if (this.disposedValue)
        {
            throw new ObjectDisposedException(nameof(CompoundDocumentResolver));
        }
    }
}