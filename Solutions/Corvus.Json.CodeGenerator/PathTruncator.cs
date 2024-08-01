/// <summary>
/// Truncates paths to a given length.
/// </summary>
public class PathTruncator
{
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
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        if (path.Length <= maxLength)
        {
            return path;
        }

        string? directory = Path.GetDirectoryName(path);
        string fileName = Path.GetFileName(path);
        string extension = Path.GetExtension(path);

        int maxFileNameLength = maxLength - (directory?.Length ?? 0) - 1; // 1 for the path separator
        if (maxFileNameLength <= extension.Length)
        {
            throw new ArgumentException("The directory path is too long to truncate the file name.", nameof(path));
        }

        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        string[] fileNameSegments = fileNameWithoutExtension.Split('.');

        if (fileNameSegments.Length == 1)
        {
            // No dot segments in the filename, just truncate the filename
            string truncatedFileName = string.Concat(fileNameWithoutExtension.AsSpan(0, maxFileNameLength - extension.Length - 3), "...", extension);
            return directory is string d1 ? Path.Combine(d1, truncatedFileName) : truncatedFileName;
        }

        string firstSegment = fileNameSegments[0];
        string lastSegment = fileNameSegments[^1];

        int remainingLength = maxFileNameLength - extension.Length - firstSegment.Length - lastSegment.Length - 1; // 1 for the dot separator

        if (remainingLength < 0)
        {
            // Not enough space to keep both first and last segments, truncate the first segment
            int firstSegmentLength = maxFileNameLength - extension.Length - lastSegment.Length - 4; // 4 for the dot and ellipsis
            if (firstSegmentLength > 0)
            {
                string truncatedFileName = $"{firstSegment}...{lastSegment}{extension}";
                return directory is string d2 ? Path.Combine(d2, truncatedFileName) : truncatedFileName;
            }

            // Not enough space to keep the first segment at all
            remainingLength = maxFileNameLength - extension.Length - lastSegment.Length - 3; // 3 for the ellipsis

            if (remainingLength < 0)
            {
                lastSegment = lastSegment.Substring(0, maxFileNameLength - extension.Length - 3); // 3 for the ellipsis

            }

        }

        remainingLength = remainingLength - (fileNameSegments.Length - 2); // Allow for the dots.

        List<string> remainingSegments = 
        for(int i = 0; i < fileNameSegments.Length / 2; )

        string truncatedMiddleSegments = string.Join(".", fileNameSegments, 1, fileNameSegments.Length - 2);
        string finalFileName = $"{firstSegment}.{truncatedMiddleSegments}.{lastSegment}{extension}";

        if (finalFileName.Length > maxFileNameLength)
        {
            finalFileName = $"{firstSegment}...{lastSegment}{extension}";
        }

        return directory is string d3 ? Path.Combine(d3, finalFileName) : finalFileName;
    }
}
