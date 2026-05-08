// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Collections.Generic;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for Utf8UriTemplate.Validate method covering RFC 6570 URI Template validation.
/// </summary>
[TestClass]
public class Utf8UriTemplateTests
{
    #region Basic Template Structure Tests

    [TestMethod]
    [DataRow("", true)]                          // Empty template
    [DataRow("simple", true)]                    // No variables
    [DataRow("http://example.com", true)]        // Simple URI without variables
    [DataRow("path/to/resource", true)]          // Simple path
    [DataRow("https://example.com/path", true)]  // HTTPS URI
    [DataRow("ftp://ftp.example.com", true)]     // FTP URI
    [DataRow("/absolute/path", true)]            // Absolute path
    [DataRow("relative/path", true)]             // Relative path
    [DataRow("file.txt", true)]                  // Simple filename
    public void Validate_BasicTemplateStructure_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("{var}", true)]                     // Simple variable
    [DataRow("{hello}", true)]                   // Simple variable
    [DataRow("prefix{var}suffix", true)]         // Variable with literals
    [DataRow("http://example.com/{var}", true)]  // Variable in URI
    [DataRow("{var}/path", true)]                // Variable at start
    [DataRow("path/{var}", true)]                // Variable at end
    [DataRow("path{var}more", true)]             // Variable in middle
    [DataRow("a{var}b{other}c", true)]           // Multiple variables
    public void Validate_BasicVariableExpressions_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("{", false)]                        // Missing closing brace
    [DataRow("}", false)]                        // Missing opening brace
    [DataRow("{}", false)]                       // Empty expression
    [DataRow("{{var}", false)]                   // Double opening brace
    [DataRow("{var}}", false)]                   // Double closing brace
    [DataRow("{var", false)]                     // Missing closing brace
    [DataRow("var}", false)]                     // Missing opening brace
    [DataRow("prefix{", false)]                  // Incomplete expression at end
    [DataRow("}suffix", false)]                  // Stray closing brace
    [DataRow("pre{fix}post{", false)]           // Mixed valid and invalid
    public void Validate_MalformedBasicStructure_Invalid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region URI Segment Validation Tests

    [TestMethod]
    [DataRow("http://example.com/{var}", true)]
    [DataRow("https://sub.domain.com/{var}/path", true)]
    [DataRow("/path/to/{var}/resource", true)]
    [DataRow("/{var}", true)]
    [DataRow("{var}/path", true)]
    [DataRow("path{var}more", true)]
    [DataRow("a{var}b{other}c", true)]
    [DataRow("http://example.com:8080/{var}", true)]
    [DataRow("scheme://host/{var}?query=value", true)]
    [DataRow("mailto:{email}", true)]
    [DataRow("file:///path/{var}", true)]
    public void Validate_ValidUriSegmentsBetweenVariables_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("ht tp://bad{var}", false)]         // Space in URI
    [DataRow("http://bad<{var}", false)]         // Invalid URI character
    [DataRow("http://bad>{var}", false)]         // Invalid URI character
    [DataRow("bad\\{var}", false)]               // Backslash
    [DataRow("bad\t{var}", false)]               // Tab character
    [DataRow("bad\n{var}", false)]               // Newline
    [DataRow("bad\r{var}", false)]               // Carriage return
    [DataRow("bad\u0000{var}", false)]           // Null character
    [DataRow("bad\u001F{var}", false)]           // Control character
    [DataRow("bad^{var}", false)]                // Caret character
    [DataRow("bad`{var}", false)]                // Backtick character
    [DataRow("bad|{var}", false)]                // Pipe character
    public void Validate_InvalidUriSegments_Invalid(string template, bool expected)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(template);
        bool result = Utf8UriTemplate.Validate(bytes);
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region Operator Tests (RFC 6570 Levels 2-4)

    [TestMethod]
    [DataRow("{+var}", true)]                    // Reserved expansion
    [DataRow("{#var}", true)]                    // Fragment expansion
    [DataRow("{+hello}", true)]
    [DataRow("{#hello}", true)]
    [DataRow("path{+var}", true)]
    [DataRow("path{#var}", true)]
    [DataRow("{+var}/here", true)]
    [DataRow("here?ref={+path}", true)]
    public void Validate_Level2Operators_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("{.var}", true)]                    // Label expansion
    [DataRow("{/var}", true)]                    // Path expansion
    [DataRow("{;var}", true)]                    // Parameter expansion
    [DataRow("{?var}", true)]                    // Query expansion
    [DataRow("{&var}", true)]                    // Query continuation
    [DataRow("X{.var}", true)]
    [DataRow("{/var,x}/here", true)]
    [DataRow("{;x,y}", true)]
    [DataRow("{?x,y}", true)]
    [DataRow("?fixed=yes{&x}", true)]
    public void Validate_Level3Operators_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("{x,y}", true)]                     // Multiple variables
    [DataRow("{+x,y}", true)]                    // Multiple with operator
    [DataRow("{#x,y,z}", true)]                  // Multiple with fragment
    [DataRow("{.x,y}", true)]                    // Multiple label
    [DataRow("{/x,y}", true)]                    // Multiple path
    [DataRow("{;x,y}", true)]                    // Multiple parameter
    [DataRow("{?x,y}", true)]                    // Multiple query
    [DataRow("{&x,y}", true)]                    // Multiple query continuation
    [DataRow("{var,hello,empty}", true)]         // Three variables
    [DataRow("{+path,x}/here", true)]            // Mixed in path
    public void Validate_Level4MultipleVariables_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("{%var}", false)]                   // Invalid operator
    [DataRow("{$var}", false)]                   // Reserved for extensions
    [DataRow("{(var}", false)]                   // Reserved for extensions
    [DataRow("{)var}", false)]                   // Reserved for extensions
    [DataRow("{[var}", false)]                   // Invalid operator
    [DataRow("{{var}", false)]                   // Double brace
    [DataRow("{^var}", false)]                   // Invalid operator
    [DataRow("{`var}", false)]                   // Invalid operator
    [DataRow("{\var}", false)]                   // Backslash operator
    public void Validate_InvalidOperators_Invalid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("{=var}", false)]                   // Reserved operator
    [DataRow("{,var}", false)]                   // Reserved operator
    [DataRow("{!var}", false)]                   // Reserved operator
    [DataRow("{@var}", false)]                   // Reserved operator
    [DataRow("{|var}", false)]                   // Reserved operator
    public void Validate_ReservedOperators_Invalid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region Variable Name Tests

    [TestMethod]
    [DataRow("{a}", true)]                       // Single letter
    [DataRow("{A}", true)]                       // Capital letter
    [DataRow("{var}", true)]                     // Simple name
    [DataRow("{hello_world}", true)]             // Underscore
    [DataRow("{var123}", true)]                  // Numbers
    [DataRow("{_var}", true)]                    // Leading underscore
    [DataRow("{var_}", true)]                    // Trailing underscore
    [DataRow("{x123y}", true)]                   // Mixed
    [DataRow("{VAR}", true)]                     // All caps
    [DataRow("{Var}", true)]                     // Mixed case
    [DataRow("{v}", true)]                       // Single char
    [DataRow("{_}", true)]                       // Just underscore
    [DataRow("{a1b2c3}", true)]                  // Alphanumeric mix
    [DataRow("{123}", true)]                     // Variable names CAN start with digit per RFC 6570
    public void Validate_ValidVariableNames_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("{var.sub}", true)]                 // Dot notation
    [DataRow("{obj.prop}", true)]                // Object property
    [DataRow("{a.b.c}", true)]                   // Multiple dots
    [DataRow("{var.sub.prop}", true)]            // Deep nesting
    [DataRow("{user.name}", true)]               // Common use case
    [DataRow("{config.db.host}", true)]          // Deep nesting
    [DataRow("{a.b}", true)]                     // Simple dot
    public void Validate_DotNotationVariableNames_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("{var%20name}", true)]              // Percent-encoded space
    [DataRow("{%41}", true)]                     // Percent-encoded 'A'
    [DataRow("{var%2Esub}", true)]               // Percent-encoded dot
    [DataRow("{%61%62%63}", true)]               // Multiple percent-encoded
    [DataRow("{var%5Funder}", true)]             // Percent-encoded underscore
    [DataRow("{%2E%2E}", true)]                  // Percent-encoded dots
    public void Validate_PercentEncodedVariableNames_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("{}", false)]                       // Empty name
    [DataRow("{.}", false)]                      // Only dot
    [DataRow("{var.}", false)]                   // Trailing dot
    [DataRow("{var..sub}", false)]               // Double dot
    [DataRow("{var-sub}", false)]                // Dash not allowed
    [DataRow("{var sub}", false)]                // Space not allowed
    [DataRow("{var@sub}", false)]                // @ not allowed
    [DataRow("{%}", false)]                      // Invalid percent encoding
    [DataRow("{%4}", false)]                     // Incomplete percent encoding
    [DataRow("{%GG}", false)]                    // Invalid hex in percent encoding
    [DataRow("{%4G}", false)]                    // Invalid hex digit
    [DataRow("{%G4}", false)]                    // Invalid hex digit
    [DataRow("{var%}", false)]                   // Incomplete percent encoding at end
    [DataRow("{var%4}", false)]                  // Incomplete percent encoding at end
    [DataRow("{var%4G}", false)]                 // Invalid hex at end
    public void Validate_InvalidVariableNames_Invalid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region Value Modifier Tests (Level 4)

    [TestMethod]
    [DataRow("{var:1}", true)]                   // Single character prefix
    [DataRow("{var:3}", true)]                   // Multi-character prefix
    [DataRow("{var:10}", true)]                  // Two-digit prefix
    [DataRow("{var:100}", true)]                 // Three-digit prefix
    [DataRow("{var:1000}", true)]                // Four-digit prefix
    [DataRow("{var:9999}", true)]                // Maximum prefix
    [DataRow("{hello:5}", true)]                 // Named variable with prefix
    [DataRow("{x:2}", true)]                     // Single char var with prefix
    public void Validate_PrefixModifiers_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("{var*}", true)]                    // Explode modifier
    [DataRow("{list*}", true)]                   // Explode list
    [DataRow("{keys*}", true)]                   // Explode associative array
    [DataRow("{hello*}", true)]                  // Named variable with explode
    [DataRow("{x*}", true)]                      // Single char var with explode
    public void Validate_ExplodeModifiers_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("{+var:3}", true)]                  // Reserved expansion with prefix
    [DataRow("{#var*}", true)]                   // Fragment expansion with explode
    [DataRow("{.var:5}", true)]                  // Label expansion with prefix
    [DataRow("{/var*}", true)]                   // Path expansion with explode
    [DataRow("{;var:10}", true)]                 // Parameter expansion with prefix
    [DataRow("{?var*}", true)]                   // Query expansion with explode
    [DataRow("{&var:2}", true)]                  // Query continuation with prefix
    [DataRow("{+hello:5}", true)]                // Reserved with prefix
    [DataRow("{#list*}", true)]                  // Fragment with explode
    public void Validate_CombinedModifiersWithOperators_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("{var:0}", false)]                  // Zero prefix not allowed
    [DataRow("{var:10000}", false)]              // Prefix too large
    [DataRow("{var:01}", false)]                 // Leading zero not allowed
    [DataRow("{var:}", false)]                   // Empty prefix
    [DataRow("{var:abc}", false)]                // Non-numeric prefix
    [DataRow("{var**}", false)]                  // Double explode
    [DataRow("{var*:3}", false)]                 // Explode before prefix
    [DataRow("{var:3*}", false)]                 // Cannot combine prefix and explode
    [DataRow("{var:00}", false)]                 // Leading zeros
    [DataRow("{var:0123}", false)]               // Leading zero with valid length
    [DataRow("{var:-1}", false)]                 // Negative prefix
    [DataRow("{var: 3}", false)]                 // Space in prefix
    public void Validate_InvalidModifiers_Invalid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region Complex Expression Tests

    [TestMethod]
    [DataRow("{var:3,other:5}", true)]           // Multiple with prefixes
    [DataRow("{list*,keys*}", true)]             // Multiple with explodes
    [DataRow("{var:2,list*}", true)]             // Mixed modifiers
    [DataRow("{+var:3,other*}", true)]           // With operator
    [DataRow("{x,hello,y}", true)]               // Multiple simple variables
    [DataRow("{var,empty,who}", true)]           // Multiple named variables
    [DataRow("{x:2,y:3,z:4}", true)]             // Multiple prefixes
    [DataRow("{a*,b*,c*}", true)]                // Multiple explodes
    public void Validate_MultipleVariablesWithModifiers_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("http://example.com{/paths*}{?query*}", true)]
    [DataRow("/users/{id}/posts{?limit:2,offset:3}", true)]
    [DataRow("{scheme}://{host}{/path*}{?query*}{#fragment}", true)]
    [DataRow("/search{?q,limit:2,fields*}", true)]
    [DataRow("http://{host}:{port}/{path}{?query*}", true)]
    [DataRow("/api/v1/{resource}/{id}{?fields*,include*}", true)]
    [DataRow("/{category}{/subcategory*}{?filter*}", true)]
    public void Validate_ComplexRealWorldExamples_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("{var,}", false)]                   // Trailing comma
    [DataRow("{,var}", false)]                   // Leading comma
    [DataRow("{var,,other}", false)]             // Double comma
    [DataRow("{var other}", false)]              // Space between variables
    [DataRow("{var;other}", false)]              // Wrong separator
    [DataRow("{var|other}", false)]              // Invalid separator
    [DataRow("{var&other}", false)]              // Invalid separator in simple expression
    [DataRow("{var?other}", false)]              // Invalid separator in simple expression
    public void Validate_InvalidComplexExpressions_Invalid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region Unicode and International Characters

    [TestMethod]
    [DataRow("http://ƒøø.ßår/{var}", true)]      // IDN in literal
    [DataRow("http://例え.テスト/{var}", true)]     // Japanese IDN
    [DataRow("http://пример.испытание/{var}", true)] // Cyrillic IDN
    [DataRow("http://مثال.آزمایشی/{var}", true)] // Arabic IDN
    [DataRow("http://उदाहरण.परीक्षा/{var}", true)] // Hindi IDN
    public void Validate_InternationalDomainNames_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("path/tëst/{var}", true)]           // Unicode in path
    [DataRow("/ユーザー/{var}", true)]             // Japanese in path
    [DataRow("/файл/{var}", true)]               // Cyrillic in path
    [DataRow("/résumé/{var}", true)]             // French accents
    [DataRow("/naïve/{var}", true)]              // Diaeresis
    [DataRow("/café/{var}", true)]               // Acute accent
    public void Validate_UnicodeInLiterals_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region Edge Cases and Boundary Conditions

    [TestMethod]
    [DataRow("", true)]                          // Empty template
    [DataRow(" {var} ", false)]                   // Whitespace around
    [DataRow("\t{var}\n", false)]                 // Tabs and newlines around (valid URI chars)
    public void Validate_EmptyAndWhitespace(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("path%20with%20spaces/{var}", true)] // Percent-encoded spaces
    [DataRow("path%2F{var}", true)]              // Percent-encoded slash
    [DataRow("path%3A{var}", true)]              // Percent-encoded colon
    [DataRow("path%3F{var}", true)]              // Percent-encoded question mark
    [DataRow("path%23{var}", true)]              // Percent-encoded hash
    [DataRow("path%26{var}", true)]              // Percent-encoded ampersand
    public void Validate_PercentEncodingInLiterals_Valid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("path%GG{var}", false)]             // Invalid percent encoding
    [DataRow("path%2{var}", false)]              // Incomplete percent encoding
    [DataRow("path%{var}", false)]               // Invalid percent encoding
    [DataRow("path%%{var}", false)]              // Double percent
    public void Validate_InvalidPercentEncoding_Invalid(string template, bool expected)
    {
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region Performance and Stress Tests

    [TestMethod]
    public void Validate_LongTemplateWithManyVariables_Valid()
    {
        string template = GenerateTemplateWithManyVariables(50);
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Validate_VeryLongLiteral_Valid()
    {
        string template = GenerateVeryLongLiteral(1000) + "{var}";
        bool result = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template));
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Validate_PathologicalCases_Invalid()
    {
        // Many opening braces
        string template1 = new('{', 100);
        bool result1 = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template1));
        Assert.IsFalse(result1);

        // Many closing braces
        string template2 = new('}', 100);
        bool result2 = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template2));
        Assert.IsFalse(result2);

        // Alternating braces
        string template3 = GenerateAlternatingBraces(100);
        bool result3 = Utf8UriTemplate.Validate(Encoding.UTF8.GetBytes(template3));
        Assert.IsFalse(result3);
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
