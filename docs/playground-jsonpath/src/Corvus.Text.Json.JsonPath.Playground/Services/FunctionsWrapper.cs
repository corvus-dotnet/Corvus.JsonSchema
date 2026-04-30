namespace Corvus.Text.Json.JsonPath.Playground.Services;

/// <summary>
/// Shared wrapper template for custom function code. Used by both the compilation
/// service (with <c>#line</c> directives for diagnostic mapping) and the IntelliSense
/// service (without directives, for position mapping).
/// </summary>
internal static class FunctionsWrapper
{
    /// <summary>
    /// Marker that identifies where user code is inserted in the wrapper.
    /// </summary>
    internal const string UserCodeMarker = "/* __USER_CODE__ */";

    /// <summary>
    /// The file path used in <c>#line</c> directives to identify user-authored function code.
    /// Diagnostics with this mapped path are in the user's code region.
    /// </summary>
    internal const string UserFilePath = "UserFunctions.cs";

    /// <summary>
    /// The helper methods and class skeleton shared between compilation and IntelliSense.
    /// Contains a <see cref="UserCodeMarker"/> placeholder where the user's dictionary
    /// initializer body is inserted.
    /// </summary>
    private const string Template =
        """
        public static class FunctionsFactory
        {
            public static Dictionary<string, IJsonPathFunction> Create()
            {
                return new Dictionary<string, IJsonPathFunction>
        /* __USER_CODE__ */
                ;
            }
        }
        """;

    /// <summary>
    /// Gets the wrapper source for compilation, with <c>#line</c> directives
    /// around the user code for diagnostic position mapping.
    /// </summary>
    public static string ForCompilation(string userBody)
    {
        string replacement = $"""
        #line 1 "{UserFilePath}"
        {userBody}
        #line default
        """;
        return Template.Replace(UserCodeMarker, replacement);
    }

    /// <summary>
    /// Gets the wrapper source for IntelliSense (no <c>#line</c> directives).
    /// Returns the character offset where user code starts.
    /// </summary>
    public static (string Source, int UserCodeOffset) ForIntelliSense(string userBody)
    {
        int markerIndex = Template.IndexOf(UserCodeMarker);
        string source = Template.Replace(UserCodeMarker, userBody);
        return (source, markerIndex);
    }
}
