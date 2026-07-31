// <copyright file="InitrdCpio.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability.MicroGuest.Deploy;

/// <summary>
/// Builds the Unikraft initrd for a micro-guest deploy (ADR 0063): a <c>newc</c>-format CPIO archive placing the
/// verified guest binary at the kernel's baked exec path. The layout mirrors the proven
/// <c>cd rootfs &amp;&amp; find . | cpio -o -H newc</c> shape the guest kernel's ELF loader consumes: a <c>.</c> root
/// entry, each ancestor directory, the executable file, and the trailer, all 4-byte aligned. The archive is
/// deterministic (fixed inodes, zero mtime, root ownership) so the same binary always bakes the same initrd.
/// </summary>
internal static class InitrdCpio
{
    private const int HeaderLength = 110;

    // Unix file modes for the entries: directories are drwxr-xr-x, the guest binary is -rwxr-xr-x so the ELF loader
    // can execute it (the same mode the Lambda deployer marks its bootstrap zip entry with).
    private const uint DirectoryMode = 0x41ED;   // 040755
    private const uint ExecutableMode = 0x81ED;  // 0100755

    /// <summary>
    /// Builds the initrd CPIO placing <paramref name="guestBinary"/> at <paramref name="guestBinaryPath"/> (an
    /// absolute path such as <c>/bin/guest</c>, which must match the kernel's baked exec path).
    /// </summary>
    /// <param name="guestBinary">The verified native guest binary bytes.</param>
    /// <param name="guestBinaryPath">The absolute path the kernel executes, e.g. <c>/bin/guest</c>.</param>
    /// <returns>The CPIO archive bytes.</returns>
    /// <exception cref="ArgumentException">The path is not absolute, or names no file.</exception>
    public static byte[] Build(ReadOnlyMemory<byte> guestBinary, string guestBinaryPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(guestBinaryPath);
        if (guestBinaryPath[0] != '/' || guestBinaryPath[^1] == '/')
        {
            throw new ArgumentException($"The guest binary path must be an absolute file path (got '{guestBinaryPath}').", nameof(guestBinaryPath));
        }

        string[] segments = guestBinaryPath[1..].Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            throw new ArgumentException($"The guest binary path must name a file (got '{guestBinaryPath}').", nameof(guestBinaryPath));
        }

        var archive = new ArrayBufferWriter<byte>();
        uint inode = 1;

        // The root and each ancestor directory, in descent order, exactly as `find .` emits them.
        WriteEntry(archive, inode++, ".", DirectoryMode, ReadOnlyMemory<byte>.Empty);
        for (int i = 0; i < segments.Length - 1; i++)
        {
            WriteEntry(archive, inode++, "./" + string.Join('/', segments[..(i + 1)]), DirectoryMode, ReadOnlyMemory<byte>.Empty);
        }

        WriteEntry(archive, inode++, "./" + string.Join('/', segments), ExecutableMode, guestBinary);
        WriteEntry(archive, 0, "TRAILER!!!", 0, ReadOnlyMemory<byte>.Empty);
        return archive.WrittenSpan.ToArray();
    }

    // One newc entry: the 110-byte ASCII header, the NUL-terminated name padded to a 4-byte boundary (relative to the
    // archive start, which every entry keeps aligned), then the data padded likewise.
    private static void WriteEntry(ArrayBufferWriter<byte> archive, uint inode, string name, uint mode, ReadOnlyMemory<byte> data)
    {
        int nameSize = Encoding.ASCII.GetByteCount(name) + 1;

        var header = new StringBuilder(HeaderLength);
        header.Append("070701");
        AppendHex(header, inode);                                  // c_ino
        AppendHex(header, mode);                                   // c_mode
        AppendHex(header, 0);                                      // c_uid (root)
        AppendHex(header, 0);                                      // c_gid (root)
        AppendHex(header, mode == DirectoryMode ? 2u : 1u);        // c_nlink
        AppendHex(header, 0);                                      // c_mtime (deterministic archive)
        AppendHex(header, (uint)data.Length);                      // c_filesize
        AppendHex(header, 0);                                      // c_devmajor
        AppendHex(header, 0);                                      // c_devminor
        AppendHex(header, 0);                                      // c_rdevmajor
        AppendHex(header, 0);                                      // c_rdevminor
        AppendHex(header, (uint)nameSize);                         // c_namesize (including the NUL)
        AppendHex(header, 0);                                      // c_check (always 0 for newc)

        Span<byte> headerBytes = archive.GetSpan(HeaderLength + nameSize + 3);
        int written = Encoding.ASCII.GetBytes(header.ToString(), headerBytes);
        written += Encoding.ASCII.GetBytes(name, headerBytes[written..]);
        headerBytes[written++] = 0;
        while (written % 4 != 0)
        {
            headerBytes[written++] = 0;
        }

        archive.Advance(written);

        if (!data.IsEmpty)
        {
            archive.Write(data.Span);
            int padding = (4 - (data.Length % 4)) % 4;
            if (padding != 0)
            {
                archive.GetSpan(padding)[..padding].Clear();
                archive.Advance(padding);
            }
        }
    }

    private static void AppendHex(StringBuilder header, uint value) => header.Append(value.ToString("x8", System.Globalization.CultureInfo.InvariantCulture));
}