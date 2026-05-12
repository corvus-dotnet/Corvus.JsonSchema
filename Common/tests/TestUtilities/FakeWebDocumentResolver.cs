// <copyright file="FakeWebDocumentResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Corvus.Json;

namespace TestUtilities;

/// <summary>
/// An <see cref="IDocumentResolver"/> that provides
/// documents based on a url.
/// </summary>
public class FakeWebDocumentResolver : IDocumentResolver
{
    private readonly string baseDirectory;

    private readonly Dictionary<string, JsonDocument> documents = [];

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
        baseDirectory = Environment.CurrentDirectory;
    }

    /// <inheritdoc/>
    public bool AddDocument(string uri, JsonDocument document)
    {
        CheckDisposed();

#if NET8_0_OR_GREATER
        return documents.TryAdd(uri, document);
#else
        if (!this.documents.ContainsKey(uri))
        {
            this.documents.Add(uri, document);
            return true;
        }

        return false;
#endif
    }

    /// <inheritdoc/>
    public void Reset()
    {
        CheckDisposed();
        DisposeDocumentsAndClear();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Do not change this code. Put clean-up code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        System.GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public async ValueTask<JsonElement?> TryResolve(JsonReference reference)
    {
        CheckDisposed();

        if (!IsMatchForFakeUri(reference))
        {
            return null;
        }

        string path = GetPath(reference);

        if (documents.TryGetValue(path, out JsonDocument? result))
        {
            if (JsonPointerUtilities.TryResolvePointer(result, reference.Fragment, out JsonElement? element))
            {
                return element;
            }

            return default;
        }

        try
        {
#if NET8_0_OR_GREATER
            await using Stream stream = File.OpenRead(path);
#else
            using Stream stream = File.OpenRead(path);
#endif
            result = await JsonDocument.ParseAsync(stream);
            return JsonPointerUtilities.ResolvePointer(result, reference.Fragment);
        }
        catch (Exception)
        {
            return default;
        }

        static bool IsMatchForFakeUri(JsonReference reference)
        {
            JsonReferenceBuilder builder = reference.AsBuilder();

            return builder.Host.SequenceEqual("localhost".AsSpan()) && builder.Port.SequenceEqual("1234".AsSpan());
        }

        string GetPath(JsonReference reference)
        {
            JsonReferenceBuilder builder = reference.AsBuilder();

            if (builder.Path[0] == '/')
            {
                return Path.Combine(baseDirectory, builder.Path[1..].ToString());
            }

            return Path.Combine(baseDirectory, builder.Path.ToString());
        }
    }

    /// <summary>
    /// Implements the dispose pattern.
    /// </summary>
    /// <param name="disposing">True if we are disposing.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                DisposeDocumentsAndClear();
            }

            disposedValue = true;
        }
    }

    private void DisposeDocumentsAndClear()
    {
        foreach (KeyValuePair<string, JsonDocument> document in documents)
        {
            document.Value.Dispose();
        }

        documents.Clear();
    }

    private void CheckDisposed()
    {
#if NET8_0_OR_GREATER
        ObjectDisposedException.ThrowIf(disposedValue, this);
#else
        if (this.disposedValue)
        {
            throw new ObjectDisposedException(nameof(FakeWebDocumentResolver));
        }
#endif
    }
}