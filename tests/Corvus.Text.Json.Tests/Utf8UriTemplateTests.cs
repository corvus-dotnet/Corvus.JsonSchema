// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Collections.Generic;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for Utf8UriTemplate.Validate method covering RFC 6570 URI Template validation.
/// </summary>
public class Utf8UriTemplateTests
{
    #region Basic Template Structure Tests

    [Theory]
    [InlineData("", true)]                          // Empty template
    [InlineData("simple", true)]                    // No variables
    [InlineData("http://example.com", true)]        // Simple URI without variables
    [InlineData("path/to/resource", true)]          // Simple path
    [InlineData("https://example.com/path", true)]  // HTTPS URI
    [InlineData("ftp://ftp.example.com", true)]     // FTP URI
    [InlineData("/absolute/path", true)]            // Absolute path
    [InlineData("relative/path", true)]             // Relative path
    [InlineData("file.txt", true)]                  // Simple filename
    public void Validate_BasicTemplateStructure_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("{var}", true)]                     // Simple variable
    [InlineData("{hello}", true)]                   // Simple variable
    [InlineData("prefix{var}suffix", true)]         // Variable with literals
    [InlineData("http://example.com/{var}", true)]  // Variable in URI
    [InlineData("{var}/path", true)]                // Variable at start
    [InlineData("path/{var}", true)]                // Variable at end
    [InlineData("path{var}more", true)]             // Variable in middle
    [InlineData("a{var}b{other}c", true)]           // Multiple variables
    public void Validate_BasicVariableExpressions_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("{", false)]                        // Missing closing brace
    [InlineData("}", false)]                        // Missing opening brace
    [InlineData("{}", false)]                       // Empty expression
    [InlineData("{{var}", false)]                   // Double opening brace
    [InlineData("{var}}", false)]                   // Double closing brace
    [InlineData("{var", false)]                     // Missing closing brace
    [InlineData("var}", false)]                     // Missing opening brace
    [InlineData("prefix{", false)]                  // Incomplete expression at end
    [InlineData("}suffix", false)]                  // Stray closing brace
    [InlineData("pre{fix}post{", false)]           // Mixed valid and invalid
    public void Validate_MalformedBasicStructure_Invalid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.Equal(expected, result);
    }

    #endregion

    #region URI Segment Validation Tests

    [Theory]
    [InlineData("http://example.com/{var}", true)]
    [InlineData("https://sub.domain.com/{var}/path", true)]
    [InlineData("/path/to/{var}/resource", true)]
    [InlineData("/{var}", true)]
    [InlineData("{var}/path", true)]
    [InlineData("path{var}more", true)]
    [InlineData("a{var}b{other}c", true)]
    [InlineData("http://example.com:8080/{var}", true)]
    [InlineData("scheme://host/{var}?query=value", true)]
    [InlineData("mailto:{email}", true)]
    [InlineData("file:///path/{var}", true)]
    public void Validate_ValidUriSegmentsBetweenVariables_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("ht tp://bad{var}", false)]         // Space in URI
    [InlineData("http://bad<{var}", false)]         // Invalid URI character
    [InlineData("http://bad>{var}", false)]         // Invalid URI character
    [InlineData("bad\\{var}", false)]               // Backslash
    [InlineData("bad\t{var}", false)]               // Tab character
    [InlineData("bad\n{var}", false)]               // Newline
    [InlineData("bad\r{var}", false)]               // Carriage return
    [InlineData("bad\u0000{var}", false)]           // Null character
    [InlineData("bad\u001F{var}", false)]           // Control character
    [InlineData("bad^{var}", false)]                // Caret character
    [InlineData("bad`{var}", false)]                // Backtick character
    [InlineData("bad|{var}", false)]                // Pipe character
    public void Validate_InvalidUriSegments_Invalid(string template, bool expected)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(template);
        bool result = Utf8UriTemplate.Validate(bytes);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Operator Tests (RFC 6570 Levels 2-4)

    [Theory]
    [InlineData("{+var}", true)]                    // Reserved expansion
    [InlineData("{#var}", true)]                    // Fragment expansion
    [InlineData("{+hello}", true)]
    [InlineData("{#hello}", true)]
    [InlineData("path{+var}", true)]
    [InlineData("path{#var}", true)]
    [InlineData("{+var}/here", true)]
    [InlineData("here?ref={+path}", true)]
    public void Validate_Level2Operators_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("{.var}", true)]                    // Label expansion
    [InlineData("{/var}", true)]                    // Path expansion
    [InlineData("{;var}", true)]                    // Parameter expansion
    [InlineData("{?var}", true)]                    // Query expansion
    [InlineData("{&var}", true)]                    // Query continuation
    [InlineData("X{.var}", true)]
    [InlineData("{/var,x}/here", true)]
    [InlineData("{;x,y}", true)]
    [InlineData("{?x,y}", true)]
    [InlineData("?fixed=yes{&x}", true)]
    public void Validate_Level3Operators_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("{x,y}", true)]                     // Multiple variables
    [InlineData("{+x,y}", true)]                    // Multiple with operator
    [InlineData("{#x,y,z}", true)]                  // Multiple with fragment
    [InlineData("{.x,y}", true)]                    // Multiple label
    [InlineData("{/x,y}", true)]                    // Multiple path
    [InlineData("{;x,y}", true)]                    // Multiple parameter
    [InlineData("{?x,y}", true)]                    // Multiple query
    [InlineData("{&x,y}", true)]                    // Multiple query continuation
    [InlineData("{var,hello,empty}", true)]         // Three variables
    [InlineData("{+path,x}/here", true)]            // Mixed in path
    public void Validate_Level4MultipleVariables_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("{%var}", false)]                   // Invalid operator
    [InlineData("{$var}", false)]                   // Reserved for extensions
    [InlineData("{(var}", false)]                   // Reserved for extensions
    [InlineData("{)var}", false)]                   // Reserved for extensions
    [InlineData("{[var}", false)]                   // Invalid operator
    [InlineData("{{var}", false)]                   // Double brace
    [InlineData("{^var}", false)]                   // Invalid operator
    [InlineData("{`var}", false)]                   // Invalid operator
    [InlineData("{\var}", false)]                   // Backslash operator
    public void Validate_InvalidOperators_Invalid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("{=var}", false)]                   // Reserved operator
    [InlineData("{,var}", false)]                   // Reserved operator
    [InlineData("{!var}", false)]                   // Reserved operator
    [InlineData("{@var}", false)]                   // Reserved operator
    [InlineData("{|var}", false)]                   // Reserved operator
    public void Validate_ReservedOperators_Invalid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.Equal(expected, result);
    }

    #endregion

    #region Variable Name Tests

    [Theory]
    [InlineData("{a}", true)]                       // Single letter
    [InlineData("{A}", true)]                       // Capital letter
    [InlineData("{var}", true)]                     // Simple name
    [InlineData("{hello_world}", true)]             // Underscore
    [InlineData("{var123}", true)]                  // Numbers
    [InlineData("{_var}", true)]                    // Leading underscore
    [InlineData("{var_}", true)]                    // Trailing underscore
    [InlineData("{x123y}", true)]                   // Mixed
    [InlineData("{VAR}", true)]                     // All caps
    [InlineData("{Var}", true)]                     // Mixed case
    [InlineData("{v}", true)]                       // Single char
    [InlineData("{_}", true)]                       // Just underscore
    [InlineData("{a1b2c3}", true)]                  // Alphanumeric mix
    [InlineData("{123}", true)]                     // Variable names CAN start with digit per RFC 6570
    public void Validate_ValidVariableNames_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("{var.sub}", true)]                 // Dot notation
    [InlineData("{obj.prop}", true)]                // Object property
    [InlineData("{a.b.c}", true)]                   // Multiple dots
    [InlineData("{var.sub.prop}", true)]            // Deep nesting
    [InlineData("{user.name}", true)]               // Common use case
    [InlineData("{config.db.host}", true)]          // Deep nesting
    [InlineData("{a.b}", true)]                     // Simple dot
    public void Validate_DotNotationVariableNames_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("{var%20name}", true)]              // Percent-encoded space
    [InlineData("{%41}", true)]                     // Percent-encoded 'A'
    [InlineData("{var%2Esub}", true)]               // Percent-encoded dot
    [InlineData("{%61%62%63}", true)]               // Multiple percent-encoded
    [InlineData("{var%5Funder}", true)]             // Percent-encoded underscore
    [InlineData("{%2E%2E}", true)]                  // Percent-encoded dots
    public void Validate_PercentEncodedVariableNames_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("{}", false)]                       // Empty name
    [InlineData("{.}", false)]                      // Only dot
    [InlineData("{var.}", false)]                   // Trailing dot
    [InlineData("{var..sub}", false)]               // Double dot
    [InlineData("{var-sub}", false)]                // Dash not allowed
    [InlineData("{var sub}", false)]                // Space not allowed
    [InlineData("{var@sub}", false)]                // @ not allowed
    [InlineData("{%}", false)]                      // Invalid percent encoding
    [InlineData("{%4}", false)]                     // Incomplete percent encoding
    [InlineData("{%GG}", false)]                    // Invalid hex in percent encoding
    [InlineData("{%4G}", false)]                    // Invalid hex digit
    [InlineData("{%G4}", false)]                    // Invalid hex digit
    [InlineData("{var%}", false)]                   // Incomplete percent encoding at end
    [InlineData("{var%4}", false)]                  // Incomplete percent encoding at end
    [InlineData("{var%4G}", false)]                 // Invalid hex at end
    public void Validate_InvalidVariableNames_Invalid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.Equal(expected, result);
    }

    #endregion

    #region Value Modifier Tests (Level 4)

    [Theory]
    [InlineData("{var:1}", true)]                   // Single character prefix
    [InlineData("{var:3}", true)]                   // Multi-character prefix
    [InlineData("{var:10}", true)]                  // Two-digit prefix
    [InlineData("{var:100}", true)]                 // Three-digit prefix
    [InlineData("{var:1000}", true)]                // Four-digit prefix
    [InlineData("{var:9999}", true)]                // Maximum prefix
    [InlineData("{hello:5}", true)]                 // Named variable with prefix
    [InlineData("{x:2}", true)]                     // Single char var with prefix
    public void Validate_PrefixModifiers_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("{var*}", true)]                    // Explode modifier
    [InlineData("{list*}", true)]                   // Explode list
    [InlineData("{keys*}", true)]                   // Explode associative array
    [InlineData("{hello*}", true)]                  // Named variable with explode
    [InlineData("{x*}", true)]                      // Single char var with explode
    public void Validate_ExplodeModifiers_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("{+var:3}", true)]                  // Reserved expansion with prefix
    [InlineData("{#var*}", true)]                   // Fragment expansion with explode
    [InlineData("{.var:5}", true)]                  // Label expansion with prefix
    [InlineData("{/var*}", true)]                   // Path expansion with explode
    [InlineData("{;var:10}", true)]                 // Parameter expansion with prefix
    [InlineData("{?var*}", true)]                   // Query expansion with explode
    [InlineData("{&var:2}", true)]                  // Query continuation with prefix
    [InlineData("{+hello:5}", true)]                // Reserved with prefix
    [InlineData("{#list*}", true)]                  // Fragment with explode
    public void Validate_CombinedModifiersWithOperators_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("{var:0}", false)]                  // Zero prefix not allowed
    [InlineData("{var:10000}", false)]              // Prefix too large
    [InlineData("{var:01}", false)]                 // Leading zero not allowed
    [InlineData("{var:}", false)]                   // Empty prefix
    [InlineData("{var:abc}", false)]                // Non-numeric prefix
    [InlineData("{var**}", false)]                  // Double explode
    [InlineData("{var*:3}", false)]                 // Explode before prefix
    [InlineData("{var:3*}", false)]                 // Cannot combine prefix and explode
    [InlineData("{var:00}", false)]                 // Leading zeros
    [InlineData("{var:0123}", false)]               // Leading zero with valid length
    [InlineData("{var:-1}", false)]                 // Negative prefix
    [InlineData("{var: 3}", false)]                 // Space in prefix
    public void Validate_InvalidModifiers_Invalid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.Equal(expected, result);
    }

    #endregion

    #region Complex Expression Tests

    [Theory]
    [InlineData("{var:3,other:5}", true)]           // Multiple with prefixes
    [InlineData("{list*,keys*}", true)]             // Multiple with explodes
    [InlineData("{var:2,list*}", true)]             // Mixed modifiers
    [InlineData("{+var:3,other*}", true)]           // With operator
    [InlineData("{x,hello,y}", true)]               // Multiple simple variables
    [InlineData("{var,empty,who}", true)]           // Multiple named variables
    [InlineData("{x:2,y:3,z:4}", true)]             // Multiple prefixes
    [InlineData("{a*,b*,c*}", true)]                // Multiple explodes
    public void Validate_MultipleVariablesWithModifiers_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("http://example.com{/paths*}{?query*}", true)]
    [InlineData("/users/{id}/posts{?limit:2,offset:3}", true)]
    [InlineData("{scheme}://{host}{/path*}{?query*}{#fragment}", true)]
    [InlineData("/search{?q,limit:2,fields*}", true)]
    [InlineData("http://{host}:{port}/{path}{?query*}", true)]
    [InlineData("/api/v1/{resource}/{id}{?fields*,include*}", true)]
    [InlineData("/{category}{/subcategory*}{?filter*}", true)]
    public void Validate_ComplexRealWorldExamples_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("{var,}", false)]                   // Trailing comma
    [InlineData("{,var}", false)]                   // Leading comma
    [InlineData("{var,,other}", false)]             // Double comma
    [InlineData("{var other}", false)]              // Space between variables
    [InlineData("{var;other}", false)]              // Wrong separator
    [InlineData("{var|other}", false)]              // Invalid separator
    [InlineData("{var&other}", false)]              // Invalid separator in simple expression
    [InlineData("{var?other}", false)]              // Invalid separator in simple expression
    public void Validate_InvalidComplexExpressions_Invalid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.Equal(expected, result);
    }

    #endregion

    #region Unicode and International Characters

    [Theory]
    [InlineData("http://ƒøø.ßår/{var}", true)]      // IDN in literal
    [InlineData("http://例え.テスト/{var}", true)]     // Japanese IDN
    [InlineData("http://пример.испытание/{var}", true)] // Cyrillic IDN
    [InlineData("http://مثال.آزمایشی/{var}", true)] // Arabic IDN
    [InlineData("http://उदाहरण.परीक्षा/{var}", true)] // Hindi IDN
    public void Validate_InternationalDomainNames_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("path/tëst/{var}", true)]           // Unicode in path
    [InlineData("/ユーザー/{var}", true)]             // Japanese in path
    [InlineData("/файл/{var}", true)]               // Cyrillic in path
    [InlineData("/résumé/{var}", true)]             // French accents
    [InlineData("/naïve/{var}", true)]              // Diaeresis
    [InlineData("/café/{var}", true)]               // Acute accent
    public void Validate_UnicodeInLiterals_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.Equal(expected, result);
    }

    #endregion

    #region Edge Cases and Boundary Conditions

    [Theory]
    [InlineData("", true)]                          // Empty template
    [InlineData(" {var} ", false)]                   // Whitespace around
    [InlineData("\t{var}\n", false)]                 // Tabs and newlines around (valid URI chars)
    public void Validate_EmptyAndWhitespace(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("path%20with%20spaces/{var}", true)] // Percent-encoded spaces
    [InlineData("path%2F{var}", true)]              // Percent-encoded slash
    [InlineData("path%3A{var}", true)]              // Percent-encoded colon
    [InlineData("path%3F{var}", true)]              // Percent-encoded question mark
    [InlineData("path%23{var}", true)]              // Percent-encoded hash
    [InlineData("path%26{var}", true)]              // Percent-encoded ampersand
    public void Validate_PercentEncodingInLiterals_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("path%GG{var}", false)]             // Invalid percent encoding
    [InlineData("path%2{var}", false)]              // Incomplete percent encoding
    [InlineData("path%{var}", false)]               // Invalid percent encoding
    [InlineData("path%%{var}", false)]              // Double percent
    public void Validate_InvalidPercentEncoding_Invalid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.Equal(expected, result);
    }

    #endregion

    #region Performance and Stress Tests

    [Fact]
    public void Validate_LongTemplateWithManyVariables_Valid()
    {
        string template = GenerateTemplateWithManyVariables(50);
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.True(result);
    }

    [Fact]
    public void Validate_VeryLongLiteral_Valid()
    {
        string template = GenerateVeryLongLiteral(1000) + "{var}";
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.True(result);
    }

    [Fact]
    public void Validate_PathologicalCases_Invalid()
    {
        // Many opening braces
        string template1 = new('{', 100);
        bool result1 = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template1));
        Assert.False(result1);

        // Many closing braces
        string template2 = new('}', 100);
        bool result2 = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template2));
        Assert.False(result2);

        // Alternating braces
        string template3 = GenerateAlternatingBraces(100);
        bool result3 = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template3));
        Assert.False(result3);
    }

    #endregion

    #region Helper Methods

    private static string GenerateTemplateWithManyVariables(int count)
    {
        var parts = new List<string>();
        for (int i = 0; i < count; i++)
        {
            parts.Add($"{{var{i}}}");
            if (i < count - 1)
            {
                parts.Add("/");
            }
        }
        return string.Join("", parts);
    }

    private static string GenerateVeryLongLiteral(int length)
    {
        return new string('a', length);
    }

    private static string GenerateAlternatingBraces(int count)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            sb.Append(i % 2 == 0 ? '{' : '}');
        }
        return sb.ToString();
    }

    #endregion
}