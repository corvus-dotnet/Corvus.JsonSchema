using System.Diagnostics.CodeAnalysis;

#if STJ && TOON
using Corvus.Toon;

namespace Corvus.Toon.Internal;
#else
namespace Corvus.Text.Json.Toon.Internal;
#endif

internal static class ThrowHelper
{
    [DoesNotReturn]
    public static void ThrowArgumentException_StreamNotWritable(string paramName)
    {
        throw new ArgumentException(SR.StreamNotWritable, paramName);
    }

    [DoesNotReturn]
    public static void ThrowToonException(string message)
    {
        throw new ToonException(message);
    }

    [DoesNotReturn]
    public static void ThrowToonException(string message, int line, int column)
    {
        throw new ToonException(message, line, column);
    }
}