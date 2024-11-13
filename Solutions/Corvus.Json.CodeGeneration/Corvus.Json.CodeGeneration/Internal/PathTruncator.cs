// <copyright file="PathTruncator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET8_0_OR_GREATER
namespace Corvus.Json.Internal;

/// <summary>
/// Truncates paths to a given length.
/// </summary>
public static class PathTruncator
{
    private static ReadOnlySpan<char> TruncationIndicator => "_._".AsSpan();

    /// <summary>
    /// Truncate a path.
    /// </summary>
    /// <param name="path">The path to truncate.</param>
    /// <param name="maxLength">The maximum length of the path.</param>
    /// <returns>The truncated path.</returns>
    /// <exception cref="ArgumentException">The directory path is too long to truncation the file name.</exception>
    public static string TruncatePath(string path, int maxLength = 260)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        if (path.Length <= maxLength)
        {
            return NormalizePath(path, false);
        }

        ReadOnlySpan<char> pathSpan = path.AsSpan();
        Span<char> result = stackalloc char[maxLength];

        int currentIndex = 0;

        ReadOnlySpan<char> directoryName = Path.GetDirectoryName(pathSpan);
        ReadOnlySpan<char> extension = Path.GetExtension(pathSpan);
        ReadOnlySpan<char> fileName = Path.GetFileNameWithoutExtension(pathSpan);

        if (directoryName.Length > maxLength)
        {
            return path;
        }

        directoryName.CopyTo(result);

        currentIndex += directoryName.Length;

        if (directoryName[^1] != Path.DirectorySeparatorChar && directoryName[^1] != Path.AltDirectorySeparatorChar)
        {
            if (currentIndex + 1 > maxLength)
            {
                return path;
            }

            result[currentIndex++] = Path.DirectorySeparatorChar;
        }

        int remaining = maxLength - currentIndex - extension.Length - TruncationIndicator.Length;

        if (remaining <= 0)
        {
            return path;
        }

        // We are now going to work with the remaining length of the string as well as the current index

        // We are going to iterate segments, alternating from the end and the beginning, truncating as we go
        int previousFirstSegmentIndex = 0;
        int previousLastSegmentIndex = fileName.Length;

        Span<char> endBuffer = stackalloc char[remaining + 1];
        Span<char> startBuffer = stackalloc char[remaining + 1];

        int writtenEnd = 0;
        int writtenStart = 0;

        while (remaining > 0)
        {
            int lastSegmentIndex = fileName[previousFirstSegmentIndex..previousLastSegmentIndex].LastIndexOf('.');

            if (lastSegmentIndex == -1)
            {
                // We are dealing with the final segment
                writtenEnd += TruncateEndSegment(fileName[previousFirstSegmentIndex..previousLastSegmentIndex], endBuffer[..^writtenEnd], remaining);
                break;
            }
            else
            {
                lastSegmentIndex += previousFirstSegmentIndex;
                int written = TruncateEndSegment(fileName[lastSegmentIndex..previousLastSegmentIndex], endBuffer[..^writtenEnd], remaining);

                remaining -= written;

                writtenEnd += written;
                previousLastSegmentIndex = lastSegmentIndex;
            }

            if (remaining == 0 && endBuffer[^writtenEnd] != '.')
            {
                break;
            }

            // Having got one from the end, let's try one from the start
            int firstSegmentIndex = fileName[previousFirstSegmentIndex..previousLastSegmentIndex].IndexOf('.');

            if (firstSegmentIndex == -1)
            {
                // We are dealing with the final segment
                writtenStart += TruncateStartSegment(fileName[previousFirstSegmentIndex..previousLastSegmentIndex], startBuffer[writtenStart..], remaining, writtenStart == 0);
                break;
            }
            else
            {
                firstSegmentIndex += previousFirstSegmentIndex;
                int written = TruncateStartSegment(fileName[previousFirstSegmentIndex..firstSegmentIndex], startBuffer[writtenStart..], remaining, writtenStart == 0);

                remaining -= written;
                writtenStart += written;
                previousFirstSegmentIndex = firstSegmentIndex + 1;
            }
        }

        if (writtenStart > 0)
        {
            startBuffer[..writtenStart].CopyTo(result[currentIndex..]);
            currentIndex += writtenStart;
        }

        TruncationIndicator.CopyTo(result[currentIndex..]);
        currentIndex += TruncationIndicator.Length;

        if (writtenEnd > 0)
        {
            // Strip out the leading . if present
            int offset = endBuffer[^writtenEnd] == '.' ? -1 : 0;

            endBuffer[^(writtenEnd + offset)..].CopyTo(result[currentIndex..]);
            currentIndex += writtenEnd + offset;
        }

        if (extension.Length > 0)
        {
            // Finally, add the extension
            extension.CopyTo(result[currentIndex..]);
            currentIndex += extension.Length;
        }

        result[..currentIndex].Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        return result[..currentIndex].ToString();
    }

    /// <summary>
    /// Normalize the separators in a path.
    /// </summary>
    /// <param name="path">The path for which to normalize directory separators.</param>
    /// <param name="convertToFullPath">Determines whether to convert to a full path before normalization.</param>
    /// <returns>The normalized path.</returns>
    public static string NormalizePath(string path, bool convertToFullPath = true)
    {
        if (convertToFullPath)
        {
            path = Path.GetFullPath(path);
        }

        if (path.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
        {
            return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }

        return path;
    }

    private static int TruncateStartSegment(ReadOnlySpan<char> readOnlySpan, Span<char> span, int remaining, bool isFirstSegment)
    {
        int offset = 0;
        if (!isFirstSegment)
        {
            if (remaining == 0)
            {
                return 0;
            }

            offset = 1;
            span[0] = '.';
            remaining--;
        }

        // We are allowed one extra character
        remaining++;

        // Do a simple truncation;
        int length = Math.Min(remaining, readOnlySpan.Length);
        readOnlySpan[..length].CopyTo(span[offset..]);
        return length + offset;
    }

    private static int TruncateEndSegment(ReadOnlySpan<char> readOnlySpan, Span<char> span, int remaining)
    {
        // Do a simple truncation;
        int length = Math.Min(remaining, readOnlySpan.Length);
        readOnlySpan[^length..].CopyTo(span[^length..]);
        return length;
    }
}
#endif