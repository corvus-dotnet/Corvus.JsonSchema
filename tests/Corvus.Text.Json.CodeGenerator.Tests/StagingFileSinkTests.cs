// <copyright file="StagingFileSinkTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// The CLI writes generated files into a staging directory as they arrive; a commit moves them over the output and
/// a discard leaves the output as it was.
/// </summary>
[TestClass]
public class StagingFileSinkTests
{
    private string root;

    [TestInitialize]
    public void Init()
    {
        this.root = Path.Combine(Path.GetTempPath(), "corvusjson-staging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this.root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(this.root))
        {
            Directory.Delete(this.root, recursive: true);
        }
    }

    [TestMethod]
    public void Discard_LeavesTheOutputFolderAsItWas()
    {
        File.WriteAllText(Path.Combine(this.root, "Existing.cs"), "// existing");
        StagingFileSink sink = new(this.root);

        sink.Add(new GeneratedCodeFile("A.cs", "// a"));
        sink.Add(new GeneratedCodeFile("Existing.cs", "// replaced"));
        Assert.AreEqual(2, sink.Count);
        Assert.AreEqual(1, Directory.GetDirectories(this.root).Length, "one staging directory while files are staged");

        sink.Discard();

        CollectionAssert.AreEqual(new[] { "Existing.cs" }, Directory.GetFiles(this.root).Select(Path.GetFileName).ToArray());
        Assert.AreEqual("// existing", File.ReadAllText(Path.Combine(this.root, "Existing.cs")));
        Assert.AreEqual(0, Directory.GetDirectories(this.root).Length, "the staging directory is gone");
    }

    [TestMethod]
    public void Commit_MovesEveryFileOverTheOutputAndReportsEachOne()
    {
        File.WriteAllText(Path.Combine(this.root, "Existing.cs"), "// existing");
        List<(int Index, string Name, string Final)> written = [];
        StagingFileSink sink = new(this.root, (index, file, finalPath) => written.Add((index, file.FileName, finalPath)));

        sink.Add(new GeneratedCodeFile("A.cs", "// a"));
        sink.Add(new GeneratedCodeFile("Existing.cs", "// replaced"));
        sink.Add(new GeneratedCodeFile("A.cs", "// a again"));
        sink.Commit();

        string[] files = Directory.GetFiles(this.root).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        CollectionAssert.AreEqual(new[] { "A.cs", "A1.cs", "Existing.cs" }, files, "a colliding name takes an index, as the drivers always did");
        Assert.AreEqual("// replaced", File.ReadAllText(Path.Combine(this.root, "Existing.cs")));
        Assert.AreEqual("// a again", File.ReadAllText(Path.Combine(this.root, "A1.cs")));
        Assert.AreEqual(0, Directory.GetDirectories(this.root).Length, "the staging directory is gone");
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, written.Select(w => w.Index).ToArray());
        CollectionAssert.AreEqual(new[] { "A.cs", "Existing.cs", "A1.cs" }, written.Select(w => Path.GetFileName(w.Final)).ToArray());
        CollectionAssert.AreEqual(new[] { "A.cs", "Existing.cs", "A1.cs" }, sink.GeneratedFiles.ToArray(), "relative to the output folder, as the lock records them");
    }

    [TestMethod]
    public void Commit_WithNoFiles_CreatesNothing()
    {
        StagingFileSink sink = new(this.root);

        sink.Commit();

        Assert.AreEqual(0, Directory.GetFileSystemEntries(this.root).Length);
    }
}