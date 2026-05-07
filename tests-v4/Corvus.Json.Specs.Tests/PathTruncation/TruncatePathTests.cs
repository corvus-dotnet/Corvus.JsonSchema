// <copyright file="TruncatePathTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET8_0_OR_GREATER
using Corvus.Json.Internal;
using Xunit;

namespace Corvus.Json.Specs.Tests.PathTruncation;

public class TruncatePathTests
{
    [Theory]
    [InlineData("foo/bar/my.file.cs", 18, "foo/bar/my.file.cs")]
    [InlineData("foo/bar/my.file.cs", 17, "foo/bar/_._ile.cs")]
    [InlineData("foo/bar/my.file.cs", 16, "foo/bar/_._le.cs")]
    [InlineData("foo/bar/my.file.cs", 15, "foo/bar/_._e.cs")]
    [InlineData("foo/bar/my.longer.file.cs", 25, "foo/bar/my.longer.file.cs")]
    [InlineData("foo/bar/my.longer.file.cs", 24, "foo/bar/my_._ger.file.cs")]
    [InlineData("foo/bar/my.longer.file.cs", 23, "foo/bar/my_._er.file.cs")]
    [InlineData("foo/bar/my.longer.file.cs", 22, "foo/bar/my_._r.file.cs")]
    [InlineData("foo/bar/my.longer.file.cs", 21, "foo/bar/my_._file.cs")]
    [InlineData("foo/bar/my.longer.file.cs", 20, "foo/bar/my_._file.cs")]
    [InlineData("foo/bar/my.longer.file.cs", 19, "foo/bar/m_._file.cs")]
    [InlineData("foo/bar/my.longer.file.cs", 18, "foo/bar/_._file.cs")]
    [InlineData("foo/bar/my.longer.file.cs", 17, "foo/bar/_._ile.cs")]
    [InlineData("foo/bar/my.longer.file.cs", 16, "foo/bar/_._le.cs")]
    [InlineData("foo/bar/my.longer.file.cs", 15, "foo/bar/_._e.cs")]
    [InlineData("foo/bar/my.extremely.longer.file.cs", 35, "foo/bar/my.extremely.longer.file.cs")]
    [InlineData("foo/bar/my.extremely.longer.file.cs", 34, "foo/bar/my.extrem_._longer.file.cs")]
    [InlineData("foo/bar/my.extremely.longer.file.cs", 33, "foo/bar/my.extre_._longer.file.cs")]
    [InlineData("foo/bar/my.extremely.longer.file.cs", 32, "foo/bar/my.extr_._longer.file.cs")]
    [InlineData("foo/bar/my.extremely.longer.file.cs", 31, "foo/bar/my.ext_._longer.file.cs")]
    [InlineData("foo/bar/my.extremely.longer.file.cs", 30, "foo/bar/my.ex_._longer.file.cs")]
    [InlineData("foo/bar/my.extremely.longer.file.cs", 29, "foo/bar/my.e_._longer.file.cs")]
    [InlineData("foo/bar/my.extremely.longer.file.cs", 28, "foo/bar/my_._longer.file.cs")]
    [InlineData("foo/bar/my.extremely.longer.file.cs", 27, "foo/bar/my_._longer.file.cs")]
    [InlineData("foo/bar/my.extremely.longer.file.cs", 26, "foo/bar/my_._onger.file.cs")]
    [InlineData("foo/bar/my.extremely.longer.file.cs", 25, "foo/bar/my_._nger.file.cs")]
    [InlineData("foo/bar/my.extremely.longer.file.cs", 24, "foo/bar/my_._ger.file.cs")]
    [InlineData("foo/bar/my.extremely.longer.file.cs", 23, "foo/bar/my_._er.file.cs")]
    [InlineData("foo/bar/my.extremely.longer.file.cs", 22, "foo/bar/my_._r.file.cs")]
    [InlineData("foo/bar/my.extremely.longer.file.cs", 21, "foo/bar/my_._file.cs")]
    [InlineData("foo/bar/my.extremely.longer.file.cs", 20, "foo/bar/my_._file.cs")]
    [InlineData("foo/bar/my.extremely.longer.file.cs", 19, "foo/bar/m_._file.cs")]
    [InlineData("foo/bar/m.file", 14, "foo/bar/m.file")]
    [InlineData("foo/bar/m", 9, "foo/bar/m")]
    [InlineData("foo/bar/fives.file", 18, "foo/bar/fives.file")]
    [InlineData("foo/bar/fives.file", 17, "foo/bar/_._s.file")]
    public void ValidatePathAndFilenameTruncation(string inputPath, int maxLength, string expectedPath)
    {
        string result = PathTruncator.TruncatePath(inputPath, maxLength);

        // Normalize to the local directory separator.
        string expected = expectedPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        Assert.Equal(expected, result);
    }
}
#endif