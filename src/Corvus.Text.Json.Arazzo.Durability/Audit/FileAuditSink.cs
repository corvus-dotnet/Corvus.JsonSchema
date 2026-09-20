// <copyright file="FileAuditSink.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// An audit sink over a directory (ADR 0069), for development: each chain is one JSON Lines file,
/// <c>{chainId}.jsonl</c>, created new and only ever appended to by this process. Every record is flushed to disk before
/// its append completes.
/// </summary>
/// <remarks>
/// The directory is evidence only as far as its file system permissions make it so: nothing here stops whoever can write
/// the directory from rewriting a file. What a rewrite cannot do is go unnoticed by <see cref="AuditChainVerifier"/>
/// once a signed head of the chain is held elsewhere. A production deployment uses immutable storage.
/// </remarks>
public sealed class FileAuditSink : IAuditSink
{
    /// <summary>The file extension of a chain file, with its dot.</summary>
    public const string ChainFileExtension = ".jsonl";

    private readonly string directory;

    /// <summary>Initializes a new instance of the <see cref="FileAuditSink"/> class.</summary>
    /// <param name="directory">The directory the chain files are created in. It is created where it does not exist.</param>
    public FileAuditSink(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        this.directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(this.directory);
    }

    /// <inheritdoc/>
    public ValueTask<IAuditChainStream> CreateChainAsync(ReadOnlyMemory<byte> chainId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // The file name is a string-typed sink (the file system API), once per chain.
        string path = Path.Combine(this.directory, Encoding.UTF8.GetString(chainId.Span) + ChainFileExtension);
        var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, bufferSize: 1, FileOptions.Asynchronous);
        return new ValueTask<IAuditChainStream>(new ChainFile(file));
    }

    private sealed class ChainFile(FileStream file) : IAuditChainStream
    {
        public async ValueTask AppendAsync(ReadOnlyMemory<byte> line, CancellationToken cancellationToken)
        {
            await file.WriteAsync(line, cancellationToken).ConfigureAwait(false);
            file.Flush(flushToDisk: true);
        }

        public ValueTask DisposeAsync() => file.DisposeAsync();
    }
}