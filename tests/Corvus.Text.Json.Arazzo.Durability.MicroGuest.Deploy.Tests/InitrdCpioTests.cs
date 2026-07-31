// <copyright file="InitrdCpioTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.MicroGuest.Deploy.Tests;

/// <summary>
/// Proves the initrd builder emits a well-formed <c>newc</c> CPIO in the exact shape the guest kernel's ELF loader
/// consumes (ADR 0063): the <c>.</c> root, each ancestor directory, the executable at the kernel's exec path with the
/// verbatim binary bytes, and the trailer, all 4-byte aligned and deterministic.
/// </summary>
[TestClass]
public sealed class InitrdCpioTests
{
    [TestMethod]
    public void Builds_the_rootfs_shape_the_kernel_consumes()
    {
        byte[] binary = [0x7F, (byte)'E', (byte)'L', (byte)'F', 1, 2, 3];

        List<CpioEntry> entries = Parse(InitrdCpio.Build(binary, "/bin/guest"));

        entries.Select(e => e.Name).ShouldBe([".", "./bin", "./bin/guest", "TRAILER!!!"]);
        entries[0].Mode.ShouldBe(0x41EDu, customMessage: "the root must be a 0755 directory");
        entries[1].Mode.ShouldBe(0x41EDu, customMessage: "the ancestor must be a 0755 directory");
        entries[2].Mode.ShouldBe(0x81EDu, customMessage: "the guest must be a 0755 regular file so the loader can execute it");
        entries[2].Data.ShouldBe(binary);
    }

    [TestMethod]
    public void A_deeper_exec_path_emits_each_ancestor_directory_in_descent_order()
    {
        List<CpioEntry> entries = Parse(InitrdCpio.Build(new byte[] { 1 }, "/usr/local/bin/guest"));

        entries.Select(e => e.Name).ShouldBe([".", "./usr", "./usr/local", "./usr/local/bin", "./usr/local/bin/guest", "TRAILER!!!"]);
    }

    [TestMethod]
    public void The_archive_is_deterministic_for_the_same_binary()
    {
        byte[] binary = Enumerable.Range(0, 1000).Select(i => (byte)i).ToArray();

        InitrdCpio.Build(binary, "/bin/guest").ShouldBe(InitrdCpio.Build(binary, "/bin/guest"));
    }

    [TestMethod]
    public void Every_entry_starts_on_a_four_byte_boundary()
    {
        // A 1-byte payload forces data padding; the parser below throws on a misaligned or malformed header, so a
        // clean parse of oddly-sized content is the alignment proof.
        List<CpioEntry> entries = Parse(InitrdCpio.Build(new byte[] { 0xAB }, "/bin/guest"));

        entries[2].Data.ShouldBe([0xAB]);
        entries[^1].Name.ShouldBe("TRAILER!!!");
    }

    [TestMethod]
    public void Rejects_a_relative_or_directory_path()
    {
        Should.Throw<ArgumentException>(() => InitrdCpio.Build(new byte[] { 1 }, "bin/guest"));
        Should.Throw<ArgumentException>(() => InitrdCpio.Build(new byte[] { 1 }, "/bin/"));
    }

    // A minimal newc reader: enough to prove the writer's structure. Each header must start where the previous
    // entry's padding ends, carry the newc magic, and describe the name and data lengths exactly.
    private static List<CpioEntry> Parse(ReadOnlySpan<byte> archive)
    {
        var entries = new List<CpioEntry>();
        int offset = 0;
        while (true)
        {
            (offset % 4).ShouldBe(0, customMessage: $"entry at {offset} is not 4-byte aligned");
            Encoding.ASCII.GetString(archive.Slice(offset, 6)).ShouldBe("070701", customMessage: $"bad magic at {offset}");

            uint mode = Field(archive, offset, 1);
            uint fileSize = Field(archive, offset, 6);
            uint nameSize = Field(archive, offset, 11);

            string name = Encoding.ASCII.GetString(archive.Slice(offset + 110, (int)nameSize - 1));
            int dataStart = Align4(offset + 110 + (int)nameSize);
            byte[] data = archive.Slice(dataStart, (int)fileSize).ToArray();

            entries.Add(new CpioEntry(name, mode, data));
            if (name == "TRAILER!!!")
            {
                return entries;
            }

            offset = Align4(dataStart + (int)fileSize);
        }
    }

    // The i-th 8-hex-character field after the magic (0 = c_ino, 1 = c_mode, 6 = c_filesize, 11 = c_namesize).
    private static uint Field(ReadOnlySpan<byte> archive, int headerOffset, int index)
        => uint.Parse(Encoding.ASCII.GetString(archive.Slice(headerOffset + 6 + (index * 8), 8)), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static int Align4(int value) => (value + 3) & ~3;

    private sealed record CpioEntry(string Name, uint Mode, byte[] Data);
}