// <copyright file="FileSystemDocumentResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json;

/// <summary>
/// Resolves a <see cref="JsonDocument"/> from the local filesystem.
/// </summary>
public class FileSystemDocumentResolver : IDocumentResolver
{
    private readonly string baseDirectory;
    private readonly Dictionary<string, JsonDocument> documents = new();
    private bool disposedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemDocumentResolver"/> class.
    /// </summary>
    /// <param name="baseDirectory">The base directory for the file system resolver.</param>
    public FileSystemDocumentResolver(string baseDirectory)
    {
        if (string.IsNullOrEmpty(baseDirectory))
        {
            throw new ArgumentException($"'{nameof(baseDirectory)}' cannot be null or empty", nameof(baseDirectory));
        }

        this.baseDirectory = baseDirectory;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemDocumentResolver"/> class.
    /// </summary>
    /// <remarks>The default base directory is <see cref="Environment.CurrentDirectory"/>.</remarks>
    public FileSystemDocumentResolver()
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
    public async Task<JsonElement?> TryResolve(JsonReference reference)
    {
        this.CheckDisposed();

        string path = Path.Combine(this.baseDirectory, new string(reference.Uri));

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
            this.documents.Add(path, result);
            return JsonPointerUtilities.ResolvePointer(result, reference.Fragment);
        }
        catch (Exception)
        {
            return default;
        }
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