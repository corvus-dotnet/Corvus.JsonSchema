// <copyright file="UriTemplateRegexBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Corvus.UriTemplates.Internal;

namespace Corvus.UriTemplates;

/// <summary>
/// Builds a regular expression that can parse the parameters from a UriTemplate.
/// </summary>
/// <remarks>
/// Note that we have a non-regex-based (low-allocation) equivalent to this in <see cref="UriTemplateParserFactory"/>.
/// This is provided for applications that specifically require a regex.
/// </remarks>
public static class UriTemplateRegexBuilder
{
    private const string Varname = "[a-zA-Z0-9_]*";
    private const string Op = "(?<op>[+#./;?&]?)";
    private const string Var = "(?<var>(?:(?<lvar>" + Varname + ")[*]?,?)*)";
    private const string Varspec = "(?<varspec>{" + Op + Var + "})";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
    private static readonly Regex FindParam = new(Varspec, RegexOptions.Compiled, DefaultTimeout);
    private static readonly Regex TemplateConversion = new(@"([^{]|^)\?", RegexOptions.Compiled, DefaultTimeout);

    /// <summary>
    /// Creates a regular expression matching the given URI template.
    /// </summary>
    /// <param name="uriTemplate">The uri template.</param>
    /// <returns>The regular expression string matching the URI template.</returns>
    /// <remarks>
    /// As this is an allocation-heavy operation, you should ensure that you cache
    /// the results in some way. Ideally, this can be done at code generation/compile time
    /// for URI templates that can be discovered at the time (e.g. when processing OpenAPI documents.)
    /// </remarks>
    public static string CreateMatchingRegex(string uriTemplate)
    {
        string template = TemplateConversion.Replace(uriTemplate, @"$+\?");

        MatchCollection matches = FindParam.Matches(template);

        string regex = FindParam.Replace(template, Match);
        return regex + "$";

        static string Match(Match m)
        {
            CaptureCollection captures = m.Groups["lvar"].Captures;
            string[] paramNames = ArrayPool<string>.Shared.Rent(captures.Count);
            try
            {
                int written = 0;
                foreach (Capture capture in captures.Cast<Capture>())
                {
                    if (!string.IsNullOrEmpty(capture.Value))
                    {
                        paramNames[written++] = capture.Value;
                    }
                }

                ReadOnlySpan<string> paramNamesSpan = paramNames.AsSpan()[0..written];

                string op = m.Groups["op"].Value;
                return op switch
                {
                    "?" => GetQueryExpression(paramNamesSpan, prefix: "?"),
                    "&" => GetQueryExpression(paramNamesSpan, prefix: "&"),
                    "#" => GetExpression(paramNamesSpan, prefix: "#"),
                    "/" => GetExpression(paramNamesSpan, prefix: "/"),
                    "+" => GetExpression(paramNamesSpan),
                    _ => GetExpression(paramNamesSpan),
                };
            }
            finally
            {
                ArrayPool<string>.Shared.Return(paramNames);
            }
        }
    }

    private static string GetQueryExpression(ReadOnlySpan<string> paramNames, string prefix)
    {
        StringBuilder sb = StringBuilderPool.Shared.Get();

        try
        {
            foreach (string paramname in paramNames)
            {
                sb.Append('\\');
                sb.Append(prefix);
                sb.Append('?');
                if (prefix == "?")
                {
                    prefix = "&";
                }

                sb.Append("(?:");
                sb.Append(paramname);
                sb.Append('=');

                sb.Append("(?<");
                sb.Append(paramname);
                sb.Append('>');
                sb.Append("[^/?&]+");
                sb.Append(')');
                sb.Append(")?");
            }

            return sb.ToString();
        }
        finally
        {
            StringBuilderPool.Shared.Return(sb);
        }
    }

    private static string GetExpression(ReadOnlySpan<string> paramNames, string? prefix = null)
    {
        StringBuilder sb = StringBuilderPool.Shared.Get();

        try
        {
            string paramDelim = prefix switch
            {
                "#" => "[^,]+",
                "/" => "[^/?]+",
                "?" or "&" => "[^&#]+",
                ";" => "[^;/?#]+",
                "." => "[^./?#]+",
                _ => "[^/?&]+",
            };

            foreach (string paramname in paramNames)
            {
                if (string.IsNullOrEmpty(paramname))
                {
                    continue;
                }

                if (prefix != null)
                {
                    sb.Append('\\');
                    sb.Append(prefix);
                    sb.Append('?');
                    if (prefix == "#")
                    {
                        prefix = ",";
                    }
                }

                sb.Append("(?<");
                sb.Append(paramname);
                sb.Append('>');
                sb.Append(paramDelim); // Param Value
                sb.Append(")?");
            }

            return sb.ToString();
        }
        finally
        {
            StringBuilderPool.Shared.Return(sb);
        }
    }
}