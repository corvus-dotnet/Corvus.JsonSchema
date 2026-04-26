// <copyright file="BaseSchemaNameHeuristic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using Corvus.Json;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// A name heuristic based on a base schema that will need to get its name from the path or reference.
/// </summary>
public sealed class BaseSchemaNameHeuristic : INameHeuristicBeforeSubschema
{
    private BaseSchemaNameHeuristic()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="BaseSchemaNameHeuristic"/>.
    /// </summary>
    public static BaseSchemaNameHeuristic Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsOptional => false;

    /// <inheritdoc/>
    public uint Priority => 1000;

    /// <inheritdoc/>
    public bool TryGetName(ILanguageProvider languageProvider, TypeDeclaration typeDeclaration, JsonReferenceBuilder reference, Span<char> typeNameBuffer, out int written)
    {
        if (typeDeclaration.Parent() is null || typeDeclaration.IsInDefinitionsContainer())
        {
            written = GetCandidateNameFromReference(typeDeclaration, reference, typeNameBuffer);
            if (written == 0)
            {
                return false;
            }

            written = Formatting.FormatTypeNameComponent(typeDeclaration, typeNameBuffer[..written], typeNameBuffer);

            if (!typeDeclaration.CollidesWithParent(typeNameBuffer[..written]))
            {
                return true;
            }

            written = Formatting.ApplyStandardSuffix(typeDeclaration, typeNameBuffer, typeNameBuffer[..written]);
            return true;
        }

        written = 0;
        return false;
    }

    private static int GetCandidateNameFromReference(
        TypeDeclaration typeDeclaration,
        JsonReferenceBuilder reference,
        Span<char> typeNameBuffer)
    {
        if (reference.HasFragment)
        {
            int lastSlash = reference.Fragment.LastIndexOf('/');
            ReadOnlySpan<char> lastSegment = reference.Fragment[(lastSlash + 1)..];

            // If the segment is a JSON-Pointer-encoded URI (e.g. a definition key like
            // "https://www.krakend.io/schema/v2.7/async_agent.json"), decode it and
            // extract the filename stem to produce a readable type name like "AsyncAgent".
            if (TryGetNameFromEncodedUri(typeDeclaration, lastSegment, typeNameBuffer, out int written))
            {
                return written;
            }

            return Formatting.FormatTypeNameComponent(typeDeclaration, lastSegment, typeNameBuffer);
        }
        else if (reference.HasPath)
        {
            return GetNameFromPath(typeDeclaration, reference, typeNameBuffer);
        }

        return 0;
    }

    /// <summary>
    /// The maximum number of URI path segments to consider for disambiguation.
    /// </summary>
    private const int MaxPathSegments = 16;

    private static bool TryGetNameFromEncodedUri(
        TypeDeclaration typeDeclaration,
        ReadOnlySpan<char> segment,
        Span<char> typeNameBuffer,
        out int written)
    {
        // In JSON Pointer encoding, "://" becomes ":~1~1", so we detect
        // http(s) URI definition keys by looking for that pattern.
        if (!segment.StartsWith("https:~1~1".AsSpan()) &&
            !segment.StartsWith("http:~1~1".AsSpan()))
        {
            written = 0;
            return false;
        }

        // Decode JSON Pointer escaping (~0 → ~, ~1 → /) to recover the original URI.
        Span<char> decodeBuffer = stackalloc char[segment.Length];
        int decodedLen = JsonPointerUtilities.DecodePointer(segment, decodeBuffer);

        // Extract meaningful path segments in reverse order (stem first, then parent, grandparent, etc.).
        // Version-like segments (e.g. "v2.7") are excluded as they add noise without disambiguation value.
        Span<int> segStarts = stackalloc int[MaxPathSegments];
        Span<int> segEnds = stackalloc int[MaxPathSegments];
        int segCount = ExtractMeaningfulPathSegments(decodeBuffer[..decodedLen], segStarts, segEnds);

        if (segCount == 0)
        {
            written = 0;
            return false;
        }

        // Try progressively longer path suffixes until the name doesn't collide
        // with any already-named sibling. Start with stem only, then add parent, grandparent, etc.
        for (int numSegs = 1; numSegs <= segCount; numSegs++)
        {
            // Build the raw candidate into typeNameBuffer in forward order (grandparent-parent-stem).
            // Use '-' separators which ToPascalCase treats as word boundaries.
            written = BuildCandidateFromSegments(decodeBuffer, segStarts, segEnds, numSegs, typeNameBuffer);

            // Format to PascalCase so we can compare with already-named siblings.
            // The caller will format again, but PascalCase is idempotent.
            written = Formatting.FormatTypeNameComponent(typeDeclaration, typeNameBuffer[..written], typeNameBuffer);

            if (!HasSiblingWithName(typeDeclaration, typeNameBuffer[..written]))
            {
                return true;
            }
        }

        // All path segments exhausted and still colliding — fall back to default behavior.
        // The post-hoc collision resolution will add a numeric suffix.
        written = 0;
        return false;
    }

    private static bool HasSiblingWithName(TypeDeclaration typeDeclaration, ReadOnlySpan<char> candidateName)
    {
        if (typeDeclaration.Parent() is not TypeDeclaration parent)
        {
            return false;
        }

        foreach (TypeDeclaration sibling in parent.Children())
        {
            if (sibling == typeDeclaration || !sibling.IsInDefinitionsContainer())
            {
                continue;
            }

            if (sibling.TryGetDotnetTypeName(out string? existingName) &&
                candidateName.SequenceEqual(existingName.AsSpan()))
            {
                return true;
            }
        }

        return false;
    }

    private static int BuildCandidateFromSegments(
        ReadOnlySpan<char> decodeBuffer,
        ReadOnlySpan<int> segStarts,
        ReadOnlySpan<int> segEnds,
        int numSegs,
        Span<char> output)
    {
        int written = 0;

        // Segments are stored in reverse order (0 = stem, 1 = parent, ...),
        // so write them forward: [numSegs-1], [numSegs-2], ..., [0].
        for (int s = numSegs - 1; s >= 0; s--)
        {
            if (written > 0)
            {
                output[written++] = '-';
            }

            ReadOnlySpan<char> seg = decodeBuffer[segStarts[s]..segEnds[s]];
            seg.CopyTo(output[written..]);
            written += seg.Length;
        }

        return written;
    }

    /// <summary>
    /// Extracts meaningful path segments from a decoded URI in reverse order
    /// (last segment first). Version-like segments are skipped. The file
    /// extension is stripped from the last segment.
    /// </summary>
    private static int ExtractMeaningfulPathSegments(
        ReadOnlySpan<char> decodedUri,
        Span<int> starts,
        Span<int> ends)
    {
        int lastSlash = decodedUri.LastIndexOf('/');
        if (lastSlash < 0 || lastSlash >= decodedUri.Length - 1)
        {
            return 0;
        }

        // First segment is the filename stem (extension stripped).
        ReadOnlySpan<char> filename = decodedUri[(lastSlash + 1)..];
        int extDot = filename.LastIndexOf('.');
        int stemEnd = extDot > 0 ? lastSlash + 1 + extDot : decodedUri.Length;

        if (stemEnd <= lastSlash + 1)
        {
            return 0;
        }

        int count = 0;
        starts[count] = lastSlash + 1;
        ends[count] = stemEnd;
        count++;

        // Walk backwards through parent directories.
        int pos = lastSlash;
        while (pos > 0 && count < starts.Length)
        {
            int prevSlash = decodedUri[..pos].LastIndexOf('/');
            int segStart = prevSlash + 1;

            if (segStart < pos)
            {
                ReadOnlySpan<char> seg = decodedUri[segStart..pos];
                if (!IsVersionSegment(seg))
                {
                    starts[count] = segStart;
                    ends[count] = pos;
                    count++;
                }
            }

            pos = prevSlash;
            if (pos <= 0)
            {
                break;
            }
        }

        return count;
    }

    private static bool IsVersionSegment(ReadOnlySpan<char> segment)
    {
        // Matches patterns like "v2", "v2.7", "v2.7.1"
        return segment.Length >= 2 &&
               (segment[0] == 'v' || segment[0] == 'V') &&
               char.IsDigit(segment[1]);
    }

    private static int GetNameFromPath(
        TypeDeclaration typeDeclaration,
        JsonReferenceBuilder reference,
        Span<char> typeNameBuffer)
    {
        if (typeDeclaration.Parent()?.DotnetTypeName() is string name)
        {
            name.AsSpan().CopyTo(typeNameBuffer);
            return name.Length;
        }

        int lastSlash = reference.Path.LastIndexOf('/');
        if (lastSlash == reference.Path.Length - 1 && lastSlash > 0)
        {
            lastSlash = reference.Path[..(lastSlash - 1)].LastIndexOf('/');
            return Formatting.FormatTypeNameComponent(typeDeclaration, reference.Path[(lastSlash + 1)..], typeNameBuffer);
        }
        else if (lastSlash == reference.Path.Length - 1)
        {
            return 0;
        }

        int lastDot = reference.Path.LastIndexOf('.');
        if (lastDot > 0 && lastSlash < lastDot)
        {
            return Formatting.FormatTypeNameComponent(typeDeclaration, reference.Path[(lastSlash + 1)..lastDot], typeNameBuffer);
        }

        return Formatting.FormatTypeNameComponent(typeDeclaration, reference.Path[(lastSlash + 1)..], typeNameBuffer);
    }
}