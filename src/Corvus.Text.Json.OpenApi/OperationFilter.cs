// <copyright file="OperationFilter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.RegularExpressions;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Filters operations by path (OpenAPI) or channel (AsyncAPI) using glob-style patterns.
/// Include patterns are additive (union); exclude patterns subtract from the include set.
/// If no include patterns are specified, all operations are included.
/// </summary>
public sealed class OperationFilter
{
    private readonly Regex[] includeRegexes;
    private readonly Regex[] excludeRegexes;

    /// <summary>
    /// Initializes a new instance of the <see cref="OperationFilter"/> class.
    /// </summary>
    /// <param name="includePaths">
    /// Glob patterns for paths/channels to include. If empty, all paths are included.
    /// Supports <c>*</c> (single segment), <c>**</c> (any depth), and <c>{name}</c> (path parameter).
    /// </param>
    /// <param name="excludePaths">
    /// Glob patterns for paths/channels to exclude from the include set.
    /// </param>
    public OperationFilter(
        IReadOnlyList<string>? includePaths = null,
        IReadOnlyList<string>? excludePaths = null)
    {
        this.IncludePaths = includePaths ?? Array.Empty<string>();
        this.ExcludePaths = excludePaths ?? Array.Empty<string>();
        this.includeRegexes = CompilePatterns(this.IncludePaths);
        this.excludeRegexes = CompilePatterns(this.ExcludePaths);
    }

    /// <summary>
    /// Gets the include patterns.
    /// </summary>
    public IReadOnlyList<string> IncludePaths { get; }

    /// <summary>
    /// Gets the exclude patterns.
    /// </summary>
    public IReadOnlyList<string> ExcludePaths { get; }

    /// <summary>
    /// Determines whether the given path matches the filter.
    /// </summary>
    /// <param name="path">The API path or channel name to test.</param>
    /// <returns><see langword="true"/> if the path matches the filter; otherwise, <see langword="false"/>.</returns>
    public bool Matches(ReadOnlySpan<char> path)
    {
        bool included = this.includeRegexes.Length == 0;

        if (!included)
        {
            for (int i = 0; i < this.includeRegexes.Length; i++)
            {
                if (this.includeRegexes[i].IsMatch(path))
                {
                    included = true;
                    break;
                }
            }
        }

        if (!included)
        {
            return false;
        }

        for (int i = 0; i < this.excludeRegexes.Length; i++)
        {
            if (this.excludeRegexes[i].IsMatch(path))
            {
                return false;
            }
        }

        return true;
    }

    private static Regex[] CompilePatterns(IReadOnlyList<string> patterns)
    {
        if (patterns.Count == 0)
        {
            return [];
        }

        var result = new Regex[patterns.Count];
        for (int i = 0; i < patterns.Count; i++)
        {
            result[i] = new Regex(
                GlobToRegex(patterns[i]),
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        return result;
    }

    /// <summary>
    /// Converts a glob pattern to a regex pattern string.
    /// </summary>
    private static string GlobToRegex(string pattern)
    {
        // Escape the pattern for use in regex
        string escaped = Regex.Escape(pattern);

        // Replace ** (escaped as \*\*) before * to avoid double replacement.
        // When preceded by /, make the trailing /... optional so /pets/** matches /pets too.
        escaped = escaped.Replace(@"/\*\*", "(/.*)?");
        escaped = escaped.Replace(@"\*\*", ".*");
        escaped = escaped.Replace(@"\*", "[^/]*");

        string regexPattern = "^" + escaped + "$";

        // Handle {param} patterns that were escaped by Regex.Escape
        regexPattern = Regex.Replace(regexPattern, @"\\{[^}]*\\}", "[^/]+");

        return regexPattern;
    }
}