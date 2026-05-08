// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public partial class JsonWriterOptionsTests
{
    [TestMethod]
    [DataRow(true, '\t', 1, true, 0, "\n")]
    [DataRow(true, ' ', 127, false, 1, "\r\n")]
    [DataRow(false, ' ', 0, true, 1024, "\n")]
    [DataRow(false, ' ', 4, false, 1024 * 1024, "\r\n")]
    public void JsonWriterOptions(bool indented, char indentCharacter, int indentSize, bool skipValidation, int maxDepth, string newLine)
    {
        var options = new JsonWriterOptions
        {
            Indented = indented,
            IndentCharacter = indentCharacter,
            IndentSize = indentSize,
            SkipValidation = skipValidation,
            MaxDepth = maxDepth,
            NewLine = newLine
        };

        var expectedOption = new JsonWriterOptions
        {
            Indented = indented,
            IndentCharacter = indentCharacter,
            IndentSize = indentSize,
            SkipValidation = skipValidation,
            MaxDepth = maxDepth,
            NewLine = newLine,
        };
        Assert.AreEqual(expectedOption, options);
    }

    [TestMethod]
    public void JsonWriterOptions_DefaultValues()
    {
        JsonWriterOptions options = default;

        Assert.IsFalse(options.Indented);
        Assert.AreEqual(' ', options.IndentCharacter);
        Assert.AreEqual(2, options.IndentSize);
        Assert.IsFalse(options.SkipValidation);
        Assert.AreEqual(0, options.MaxDepth);
        Assert.AreEqual(Environment.NewLine, options.NewLine);
    }

    [TestMethod]
    [DataRow('\f')]
    [DataRow('\n')]
    [DataRow('\r')]
    [DataRow('\0')]
    [DataRow('a')]
    public void JsonWriterOptions_IndentCharacter_InvalidCharacter(char character)
    {
        var options = new JsonWriterOptions();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => options.IndentCharacter = character);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(128)]
    [DataRow(int.MinValue)]
    [DataRow(int.MaxValue)]
    public void JsonWriterOptions_IndentSize_OutOfRange(int size)
    {
        var options = new JsonWriterOptions();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => options.IndentSize = size);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(-100)]
    public void JsonWriterOptions_MaxDepth_InvalidParameters(int maxDepth)
    {
        var options = new JsonWriterOptions();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => options.MaxDepth = maxDepth);
    }

    [TestMethod]
    public void JsonWriterOptions_MultipleValues()
    {
        JsonWriterOptions defaultOptions = default;
        var options = new JsonWriterOptions
        {
            Indented = true
        };
        options.Indented = defaultOptions.Indented;
        Assert.AreEqual(defaultOptions.Indented, options.Indented);

        options.IndentCharacter = '\t';
        options.IndentCharacter = defaultOptions.IndentCharacter;
        Assert.AreEqual(defaultOptions.IndentCharacter, options.IndentCharacter);

        options.IndentSize = 127;
        options.IndentSize = defaultOptions.IndentSize;
        Assert.AreEqual(defaultOptions.IndentSize, options.IndentSize);

        options.SkipValidation = true;
        options.SkipValidation = defaultOptions.SkipValidation;
        Assert.AreEqual(defaultOptions.SkipValidation, options.SkipValidation);

        options.MaxDepth = 1024 * 1024;
        options.MaxDepth = defaultOptions.MaxDepth;
        Assert.AreEqual(defaultOptions.MaxDepth, options.MaxDepth);

        options.NewLine = Environment.NewLine.Length == 1 ? "\r\n" : "\n";
        options.NewLine = defaultOptions.NewLine;
        Assert.AreEqual(defaultOptions.NewLine, options.NewLine);

        Assert.AreEqual(defaultOptions, options);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("\r")]
    [DataRow("\n\n")]
    [DataRow("\r\n\r\n")]
    [DataRow("0")]
    [DataRow("a")]
    [DataRow("foo")]
    [DataRow("$")]
    [DataRow(".")]
    [DataRow("\u03b1")]
    public void JsonWriterOptions_NewLine_InvalidNewLine(string value)
    {
        var options = new JsonWriterOptions();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => options.NewLine = value);
    }

    [TestMethod]
    public void JsonWriterOptions_NewLine_Null_Throws()
    {
        var options = new JsonWriterOptions();
        Assert.ThrowsExactly<ArgumentNullException>(() => options.NewLine = null);
    }

    [TestMethod]
    [DataRow(true, '\t', 1, true, 0, "\n")]
    [DataRow(true, ' ', 127, false, 1, "\r\n")]
    [DataRow(false, ' ', 0, true, 1024, "\n")]
    [DataRow(false, ' ', 4, false, 1024 * 1024, "\r\n")]
    public void JsonWriterOptions_Properties(bool indented, char indentCharacter, int indentSize, bool skipValidation, int maxDepth, string newLine)
    {
        var options = new JsonWriterOptions
        {
            Indented = indented,
            IndentCharacter = indentCharacter,
            IndentSize = indentSize,
            SkipValidation = skipValidation,
            MaxDepth = maxDepth,
            NewLine = newLine
        };

        Assert.AreEqual(indented, options.Indented);
        Assert.AreEqual(indentCharacter, options.IndentCharacter);
        Assert.AreEqual(indentSize, options.IndentSize);
        Assert.AreEqual(skipValidation, options.SkipValidation);
        Assert.AreEqual(maxDepth, options.MaxDepth);
        Assert.AreEqual(newLine, options.NewLine);
    }

    [TestMethod]
    public void JsonWriterOptionsCtor()
    {
        var options = new JsonWriterOptions();

        var expectedOption = new JsonWriterOptions
        {
            Indented = false,
            IndentCharacter = ' ',
            IndentSize = 2,
            SkipValidation = false,
            MaxDepth = 0,
            NewLine = Environment.NewLine,
        };
        Assert.AreEqual(expectedOption, options);
    }

    [TestMethod]
    public void JsonWriterOptionsDefaultCtor()
    {
        JsonWriterOptions options = default;

        var expectedOption = new JsonWriterOptions
        {
            Indented = false,
            IndentCharacter = ' ',
            IndentSize = 2,
            SkipValidation = false,
            MaxDepth = 0,
            NewLine = Environment.NewLine,
        };
        Assert.AreEqual(expectedOption, options);
    }
}
