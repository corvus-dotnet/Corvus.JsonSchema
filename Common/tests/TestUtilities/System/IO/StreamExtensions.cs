// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
using System.Threading;
using System.Threading.Tasks;

namespace System.IO;

public static class StreamExtensions
{
    public static async Task<int> ReadByteAsync(this Stream stream, CancellationToken cancellationToken = default)
    {
        byte[] buffer = new byte[1];

        int numBytesRead = await stream.ReadAsync(buffer, 0, 1, cancellationToken);
        if (numBytesRead == 0)
        {
            return -1; // EOF
        }

        return buffer[0];
    }
}