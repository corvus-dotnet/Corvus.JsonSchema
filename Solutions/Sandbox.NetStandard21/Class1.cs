using System.Diagnostics.CodeAnalysis;

namespace CorvusLibrary
{
    // This project exists to validate that a pure netstandard2.1 library can be built.
    public class Class1
    {
        public static bool IsValidInput([NotNullWhen(true)] string? input)
        {
            return !string.IsNullOrWhiteSpace(input);
        }
    }
}
