using System.Diagnostics.CodeAnalysis;

namespace CorvusLibrary
{
    /// <summary>
    /// This project exists to validate that a pure netstandard2.1 library can be built.
    /// </summary>
    public class Class1
    {
        /// <summary>
        /// Determines whether the specified input is valid.
        /// </summary>
        /// <param name="input">The input string to validate.</param>
        /// <returns><see langword="true"/> if the input is valid; otherwise, <see langword="false"/>.</returns>
        public static bool IsValidInput([NotNullWhen(true)] string? input)
        {
            return !string.IsNullOrWhiteSpace(input);
        }
    }
}