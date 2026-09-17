// <copyright file="StagingFileSink.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGenerator;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Writes each generated file as it is completed into a staging directory under the output folder, then moves the
/// files over the output on <see cref="Commit"/> or deletes them on <see cref="Discard"/>, so that the whole output
/// is never held in memory and a failed run leaves the output folder as it was.
/// </summary>
/// <param name="outputPath">The output folder.</param>
/// <param name="onWritten">Called for each file with its index, the file and its final path, after it is written.</param>
internal sealed class StagingFileSink(string outputPath, Action<int, GeneratedCodeFile, string>? onWritten = null) : IGeneratedCodeFileSink
{
    private readonly string stagingPath = Path.Combine(outputPath, ".corvusjson-staging-" + Guid.NewGuid().ToString("N").Substring(0, 8));
    private readonly HashSet<string> writtenFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Staged, string Final)> files = [];

    /// <summary>
    /// Gets the number of files written so far.
    /// </summary>
    public int Count => this.files.Count;

    /// <summary>
    /// Gets the final paths of the files written so far, relative to the output folder as the lock records them.
    /// </summary>
    public IEnumerable<string> GeneratedFiles => this.files.Select(f => JsonSchemaLockFile.ToGeneratedFile(f.Final, outputPath));

    /// <inheritdoc/>
    public void Add(GeneratedCodeFile file)
    {
        if (this.files.Count == 0)
        {
            Directory.CreateDirectory(this.stagingPath);
        }

        // The final name is chosen against the output folder (truncation and collisions apply there); the staged copy
        // takes the same name inside the staging directory.
        string finalPath = GenerationDriverV5.TruncateFileNameIfRequired(outputPath, this.writtenFiles, file);
        string stagedPath = Path.Combine(this.stagingPath, Path.GetFileName(finalPath));
        GeneratedFileWriter.Write(file, stagedPath);
        onWritten?.Invoke(this.files.Count, file, finalPath);
        this.files.Add((stagedPath, finalPath));
    }

    /// <summary>
    /// Moves every staged file over the output folder and removes the staging directory.
    /// </summary>
    public void Commit()
    {
        foreach ((string staged, string final) in this.files)
        {
            File.Move(staged, final, overwrite: true);
        }

        this.RemoveStaging();
    }

    /// <summary>
    /// Deletes the staged files, leaving the output folder as it was.
    /// </summary>
    public void Discard()
    {
        this.RemoveStaging();
    }

    private void RemoveStaging()
    {
        if (Directory.Exists(this.stagingPath))
        {
            Directory.Delete(this.stagingPath, recursive: true);
        }
    }
}