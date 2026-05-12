// <copyright file="DefaultNameCollisionResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// The default name collision resolver.
/// </summary>
/// <remarks>
/// <para>
/// This resolver always succeeds, so it is set at the lowest possible
/// priority. It is intended to be used as a fallback when no other
/// resolvers can resolve a name collision.
/// </para>
/// <para>
/// If you have specialized requirements, you can add resolvers at a higher priority.
/// </para>
/// </remarks>
public class DefaultNameCollisionResolver : INameCollisionResolver
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="DefaultNameCollisionResolver"/>.
    /// </summary>
    public static DefaultNameCollisionResolver Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsOptional => false;

    /// <inheritdoc/>
    public uint Priority => uint.MaxValue;

    /// <inheritdoc/>
    public bool TryResolveNameCollision(
        CSharpLanguageProvider languageProvider,
        TypeDeclaration typeDeclaration,
        TypeDeclaration parent,
        ReadOnlySpan<char> parentName,
        Span<char> typeNameBuffer,
        int length,
        out int written)
    {
        string location = typeDeclaration.LocatedSchema.Location;

        // Capture the reference outside the loop so we can work through it.
        JsonReference updatedReference = typeDeclaration.LocatedSchema.Location.MoveToParentFragment();
        Span<char> trimmedStringBuffer = stackalloc char[typeNameBuffer.Length];

        int slashIndex = 0;

        bool first = true;

        for (int index = 1;
            parentName.Equals(typeNameBuffer[..length], StringComparison.Ordinal) ||
            parent.FindChildNameCollision(typeDeclaration, typeNameBuffer[..length]) is not null;
            index++)
        {
            int trimmedStringLength = length;
            while (trimmedStringLength > 1 && typeNameBuffer[trimmedStringLength - 1] >= '0' && typeNameBuffer[trimmedStringLength - 1] <= '9')
            {
                trimmedStringLength--;
            }

            // We always copy, because we are going to manipulate the type name
            // buffer.
            typeNameBuffer[..trimmedStringLength].CopyTo(trimmedStringBuffer);

            ReadOnlySpan<char> trimmedString = trimmedStringBuffer[..trimmedStringLength];

            if (first)
            {
                while (updatedReference.HasFragment && IsPropertySubschemaKeywordFragment(typeDeclaration, updatedReference.Fragment))
                {
                    updatedReference = updatedReference.MoveToParentFragment();
                }

                if (updatedReference.HasFragment && slashIndex > 0 && (slashIndex = updatedReference.Fragment[slashIndex..].LastIndexOf('/')) >= 0 && slashIndex < updatedReference.Fragment.Length - 1)
                {
                    ReadOnlySpan<char> previousNode = updatedReference.Fragment[(slashIndex + 1)..];
                    previousNode.CopyTo(typeNameBuffer);
                    length = Formatting.ToPascalCase(typeNameBuffer[..previousNode.Length]);
                    trimmedString.CopyTo(typeNameBuffer[previousNode.Length..]);
                    length += trimmedString.Length;
                }
                else if (!trimmedString.Equals(parentName, StringComparison.Ordinal))
                {
                    parentName.CopyTo(typeNameBuffer);
                    trimmedString.CopyTo(typeNameBuffer[parentName.Length..]);
                    length = parentName.Length + trimmedString.Length;
                    index--;
                }

                first = false;
            }
            else
            {
                trimmedString.CopyTo(typeNameBuffer);
                length = trimmedStringLength;
                length += Formatting.ApplySuffix(index, typeNameBuffer[trimmedStringLength..]);
            }
        }

        written = length;

        // This always fixes the problem!
        return true;

        static bool IsPropertySubschemaKeywordFragment(TypeDeclaration typeDeclaration, ReadOnlySpan<char> fragment)
        {
            foreach (IPropertySubchemaProviderKeyword keyword in typeDeclaration.LocatedSchema.Vocabulary.Keywords.OfType<IPropertySubchemaProviderKeyword>())
            {
                if (keyword.Keyword.AsSpan().Equals(fragment, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}