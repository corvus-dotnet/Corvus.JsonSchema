// <copyright file="CodeGenerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A <see cref="StringBuilder"/>0like class that supports code generation.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CodeGenerator"/> class.
/// </remarks>
/// <param name="languageProvider">The language provider for the code generator.</param>
/// <param name="cancellationToken">The cancellation token.</param>
/// <param name="instancesPerIndent">The instances of the indent character sequence, per indent (defaults to 4).</param>
/// <param name="indentSequence">The indent character sequence (defaults to ' ' (space).</param>
/// <param name="lineEndSequence">The line end sequence.</param>
public class CodeGenerator(ILanguageProvider languageProvider, CancellationToken cancellationToken, int instancesPerIndent = 4, string indentSequence = " ", string lineEndSequence = "\r\n")
{
    private readonly string indentSequence = string.Concat(Enumerable.Repeat(indentSequence, instancesPerIndent));
    private readonly StringBuilder stringBuilder = new();
    private readonly Dictionary<MemberName, string> memberNames = [];
    private readonly Dictionary<string, HashSet<string>> memberNamesByScope = [];
    private readonly Stack<ScopeValue> scope = [];
    private readonly Dictionary<string, Stack<object?>> metadata = [];
    private readonly Dictionary<TypeDeclaration, Dictionary<string, string>> generatedFiles = [];
    private readonly CancellationToken cancellationToken = cancellationToken;
    private int indentationLevel = 0;
    private string? currentFileBuilderName;
    private TypeDeclaration? currentTypeDeclaration;

    // We pass this dictionary off to the language provider (indirectly)
    // so we create a new one each type we start a TypeDeclaration.
    private Dictionary<string, string>? currentTypeDeclarationFiles;

    /// <summary>
    /// Gets or sets the capacity of the <see cref="CodeGenerator"/>.
    /// </summary>
    public int Capacity
    {
        get => this.stringBuilder.Capacity;
        set => this.stringBuilder.Capacity = value;
    }

    /// <summary>
    /// Gets or sets the line end sequence.
    /// </summary>
    public string LineEndSequence { get; set; } = lineEndSequence;

    /// <summary>
    /// Gets the maximum capacity this builder is allowed to have.
    /// </summary>
    public int MaxCapacity => this.stringBuilder.MaxCapacity;

    /// <summary>
    /// Gets or sets the length of this builder.
    /// </summary>
    public int Length
    {
        get => this.stringBuilder.Length;
        set => this.stringBuilder.Length = value;
    }

    /// <summary>
    /// Gets a value indicating whether cancellation has been requested.
    /// </summary>
    public bool IsCancellationRequested => this.cancellationToken.IsCancellationRequested;

    /// <summary>
    /// Gets the current scope type.
    /// </summary>
    public int ScopeType => this.scope.Count > 0 ? this.scope.Peek().Type : CodeGeneration.ScopeType.Unknown;

    /// <summary>
    /// Gets the fully qualified scope for the builder.
    /// </summary>
    public string FullyQualifiedScope { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the language provider for the builder.
    /// </summary>
    public ILanguageProvider LanguageProvider { get; } = languageProvider;

    /// <summary>
    /// Gets the character at the specified index.
    /// </summary>
    /// <param name="index">The index at which to retrieve the character.</param>
    /// <returns>The character at the index.</returns>
    [IndexerName("Chars")]
    public char this[int index]
    {
        get => this.stringBuilder[index];
        set => this.stringBuilder[index] = value;
    }

    /// <summary>
    /// Pushes a piece of metadata onto the stack.
    /// </summary>
    /// <typeparam name="T">The type of the value to push.</typeparam>
    /// <param name="key">The key to push.</param>
    /// <param name="value">The value to push for the key.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    /// <remarks>
    /// This allows us to build a contextual stack of metadata associated with the current state of the
    /// code generator, temporarily overriding some value and then restoring the state of that value once
    /// some context or scope is left.
    /// </remarks>
    public CodeGenerator PushMetadata<T>(string key, T value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        if (!this.metadata.TryGetValue(key, out Stack<object?>? values))
        {
            values = [];
            this.metadata.Add(key, values);
        }

        values.Push(value);
        return this;
    }

    /// <summary>
    /// Pops a piece of metadata from the stack.
    /// </summary>
    /// <param name="key">The key to pop.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator PopMetadata(string key)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        if (!this.metadata.TryGetValue(key, out Stack<object?>? values))
        {
            throw new InvalidOperationException("The key was not present on the stack.");
        }

        values.Pop();
        return this;
    }

    /// <summary>
    /// Peeks the value of the metadata with the given key.
    /// </summary>
    /// <typeparam name="T">The type of the value to peek.</typeparam>
    /// <param name="key">The key for which to peek the metadata.</param>
    /// <param name="value">The current value of the metadata with the given key, or
    /// <see langword="null"/> if the key is not present.</param>
    /// <returns><see langword="true"/> if the metadata was present with the given key and type.</returns>
    public bool TryPeekMetadata<T>(string key, [MaybeNullWhen(false)] out T? value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            value = default;
            return false;
        }

        if (!this.metadata.TryGetValue(key, out Stack<object?>? values))
        {
            value = default;
            return false;
        }

#if NET8_0_OR_GREATER
        bool hasValue = values.TryPeek(out object? objectValue);
        if (hasValue && objectValue is T v)
        {
            value = v;
            return true;
        }

        value = default;
        return false;

#else
        if (values.Count > 0)
        {
            object? objectValue = values.Peek();
            if (objectValue is T v)
            {
                value = v;
                return true;
            }
        }

        value = default;
        return false;
#endif
    }

    /// <summary>
    /// Begin a new type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to generate code.</param>
    /// <returns><see langword="true"/> if the type declaration has not yet been generated.</returns>
    public bool TryBeginTypeDeclaration(TypeDeclaration typeDeclaration)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        if (this.generatedFiles.ContainsKey(typeDeclaration))
        {
            return false;
        }

        // Set the current type declaration
        this.currentTypeDeclaration = typeDeclaration;

        // Clear the string builder.
        this.stringBuilder.Clear();
        this.currentTypeDeclarationFiles = [];

        // Clear the internal state for the type declaration.
        this.memberNames.Clear();
        this.memberNamesByScope.Clear();
        this.scope.Clear();
        this.metadata.Clear();

        return true;
    }

    /// <summary>
    /// End a type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to generate code.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator EndTypeDeclaration(TypeDeclaration typeDeclaration)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        Debug.Assert(this.currentTypeDeclaration is not null, "The type declaration has been ended out-of-sequence.");
        Debug.Assert(this.currentTypeDeclarationFiles is not null, "The type declaration has been ended out-of-sequence.");
        Debug.Assert(this.currentTypeDeclaration == typeDeclaration, "The type declaration has been ended out-of-sequence.");

        this.generatedFiles.Add(this.currentTypeDeclaration, this.currentTypeDeclarationFiles);
        return this;
    }

    /// <summary>
    /// Begin creating a new file.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to create the new file.</param>
    /// <param name="fileName">The base name for this file, which will be appended to the base file name for the type declaration.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator BeginFile(TypeDeclaration typeDeclaration, string fileName)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        Debug.Assert(this.currentTypeDeclaration is not null, "There is no type declaration for which to create the file.");
        Debug.Assert(this.currentTypeDeclarationFiles is not null, "The type declaration files have not been initialized.");
        Debug.Assert(this.currentTypeDeclaration == typeDeclaration, "A file has been started out-of-sequence for the current type declaration.");
        Debug.Assert(!this.currentTypeDeclarationFiles.ContainsKey(fileName), "A file with this suffix has already been added for the type declaration.");

        this.currentFileBuilderName = fileName;
        this.stringBuilder.Clear();
        return this;
    }

    /// <summary>
    /// End creating a new file.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to create the new file.</param>
    /// <param name="fileSuffix">The suffix for the file, which will be appended to the base file name for the type declaration.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator EndFile(TypeDeclaration typeDeclaration, string fileSuffix)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        Debug.Assert(this.currentTypeDeclaration is not null, "The type declaration has been ended out-of-sequence.");
        Debug.Assert(this.currentTypeDeclarationFiles is not null, "The type declaration has been ended out-of-sequence.");
        Debug.Assert(this.currentTypeDeclaration == typeDeclaration, "A file has been ended out-of-sequence for the current type declaration.");
        Debug.Assert(this.currentFileBuilderName == fileSuffix, "A file has been ended out-of-sequence.");
        ////Debug.Assert(this.indentationLevel == 0, $"Mismatched indents in file: {this.indentationLevel}.");

        this.currentTypeDeclarationFiles.Add(this.currentFileBuilderName, this.ToString());
        return this;
    }

    /// <summary>
    /// Ensures that the capacity of this builder is at least the specified value.
    /// </summary>
    /// <param name="capacity">The new capacity for this builder.</param>
    /// <remarks>
    /// If <paramref name="capacity"/> is less than or equal to the current capacity of
    /// this builder, the capacity remains unchanged.
    /// </remarks>
    /// <returns>The new capacity of the builder.</returns>
    public int EnsureCapacity(int capacity)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return 0;
        }

        return this.stringBuilder.EnsureCapacity(capacity);
    }

    /// <summary>
    /// Gets a value indicating whether the builder ends with the given
    /// character sequence.
    /// </summary>
    /// <param name="sequence">The sequence of characters to match.</param>
    /// <returns><see langword="true"/> if the builder ends with the given sequence.</returns>
    public bool EndsWith(string sequence)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        for (int i = 1; i <= sequence.Length; i++)
        {
            if (this.stringBuilder[^i] != sequence[^i])
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return this.stringBuilder.ToString();
    }

    /// <summary>
    /// Creates a string from a substring of this builder.
    /// </summary>
    /// <param name="startIndex">The index to start in this builder.</param>
    /// <param name="length">The number of characters to read in this builder.</param>
    /// <returns>The resulting substring.</returns>
    public string ToString(int startIndex, int length)
    {
        return this.stringBuilder.ToString(startIndex, length);
    }

    /// <summary>
    /// Resets the builder.
    /// </summary>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Clear()
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Clear();
        this.indentationLevel = 0;
        return this;
    }

    /// <summary>
    /// Increase the indent.
    /// </summary>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator PushIndent()
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.indentationLevel++;
        return this;
    }

    /// <summary>
    /// Decrease the indent.
    /// </summary>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    /// <remarks>This will do nothing if the indentation level is already 0.</remarks>
    public CodeGenerator PopIndent()
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        if (this.indentationLevel > 0)
        {
            this.indentationLevel--;
        }

        return this;
    }

    /// <summary>
    /// Enter a new member name scope.
    /// </summary>
    /// <param name="scopeName">The name of the scope.</param>
    /// <param name="scopeType">The type of the scope.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator PushMemberScope(string scopeName, int scopeType)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.scope.Push(new(scopeName, scopeType));
        this.FullyQualifiedScope = this.BuildFullyQualifiedScope();

        return this;
    }

    /// <summary>
    /// Leave a member name scope.
    /// </summary>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator PopMemberScope()
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.scope.Pop();
        this.FullyQualifiedScope = this.BuildFullyQualifiedScope();
        return this;
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// <para>
    /// GetChunks returns ChunkEnumerator that follows the IEnumerable pattern and
    /// thus can be used in a C# 'foreach' statements to retrieve the data in the StringBuilder
    /// as chunks (ReadOnlyMemory) of characters.  An example use is:
    /// </para>
    /// <para>
    /// <code>
    ///      foreach (ReadOnlyMemory&lt;char&gt; chunk in sb.GetChunks())
    ///         foreach (char c in chunk.Span)
    ///             { /* operation on c }
    /// </code>
    /// </para>
    /// <para>
    /// It is undefined what happens if the StringBuilder is modified while the chunk
    /// enumeration is incomplete.  StringBuilder is also not thread-safe, so operating
    /// on it with concurrent threads is illegal.  Finally the ReadOnlyMemory chunks returned
    /// are NOT guaranteed to remain unchanged if the StringBuilder is modified, so do
    /// not cache them for later use either.  This API's purpose is efficiently extracting
    /// the data of a CONSTANT StringBuilder.
    /// </para>
    /// <para>
    /// Creating a ReadOnlySpan from a ReadOnlyMemory  (the .Span property) is expensive
    /// compared to the fetching of the character, so create a local variable for the SPAN
    /// if you need to use it in a nested for statement.  For example:
    /// </para>
    /// <para>
    /// <code>
    ///    foreach (ReadOnlyMemory&lt;char&gt; chunk in sb.GetChunks())
    ///    {
    ///         var span = chunk.Span;
    ///         for (int i = 0; i &lt; span.Length; i++)
    ///             { /* operation on span[i] */ }
    ///    }
    /// </code>
    /// </para>
    /// </summary>
    /// <returns>The chunk enumerator.</returns>
    public StringBuilder.ChunkEnumerator GetChunks() => this.stringBuilder.GetChunks();
#endif

    /// <summary>
    /// Appends a character 0 or more times to the end of this builder.
    /// </summary>
    /// <param name="value">The character to append.</param>
    /// <param name="repeatCount">The number of times to append <paramref name="value"/>.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Append(char value, int repeatCount)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Append(value, repeatCount);
        return this;
    }

    /// <summary>
    /// Appends a range of characters to the end of this builder.
    /// </summary>
    /// <param name="value">The characters to append.</param>
    /// <param name="startIndex">The index to start in <paramref name="value"/>.</param>
    /// <param name="charCount">The number of characters to read in <paramref name="value"/>.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Append(char[]? value, int startIndex, int charCount)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Append(value, startIndex, charCount);
        return this;
    }

    /// <summary>
    /// Appends a string to the end of this builder.
    /// </summary>
    /// <param name="value">The string to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Append(string? value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Append(value);
        return this;
    }

    /// <summary>
    /// Appends part of a string to the end of this builder.
    /// </summary>
    /// <param name="value">The string to append.</param>
    /// <param name="startIndex">The index to start in <paramref name="value"/>.</param>
    /// <param name="count">The number of characters to read in <paramref name="value"/>.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Append(string? value, int startIndex, int count)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Append(value, startIndex, count);
        return this;
    }

    /// <summary>
    /// Append the contents of the builder.
    /// </summary>
    /// <param name="value">The builder to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Append(StringBuilder? value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Append(value);
        return this;
    }

    /// <summary>
    /// Append the contents of the builder.
    /// </summary>
    /// <param name="value">The builder to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Append(CodeGenerator? value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Append(value?.stringBuilder);
        return this;
    }

    /// <summary>
    /// Appends a character 0 or more times to the end of this builder.
    /// </summary>
    /// <param name="value">The character to append.</param>
    /// <param name="repeatCount">The number of times to append <paramref name="value"/>.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendIndent(char value, int repeatCount)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.WriteIndent();
        this.stringBuilder.Append(value, repeatCount);
        return this;
    }

    /// <summary>
    /// Appends a range of characters to the end of this builder.
    /// </summary>
    /// <param name="value">The characters to append.</param>
    /// <param name="startIndex">The index to start in <paramref name="value"/>.</param>
    /// <param name="charCount">The number of characters to read in <paramref name="value"/>.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendIndent(char[]? value, int startIndex, int charCount)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.WriteIndent();
        this.stringBuilder.Append(value, startIndex, charCount);
        return this;
    }

    /// <summary>
    /// Appends a string to the end of this builder.
    /// </summary>
    /// <param name="value">The string to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendIndent(string? value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.WriteIndent();
        this.stringBuilder.Append(value);
        return this;
    }

    /// <summary>
    /// Appends part of a string to the end of this builder.
    /// </summary>
    /// <param name="value">The string to append.</param>
    /// <param name="startIndex">The index to start in <paramref name="value"/>.</param>
    /// <param name="count">The number of characters to read in <paramref name="value"/>.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendIndent(string? value, int startIndex, int count)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.WriteIndent();
        this.stringBuilder.Append(value, startIndex, count);
        return this;
    }

    /// <summary>
    /// Append the contents of the builder.
    /// </summary>
    /// <param name="value">The builder to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendIndent(StringBuilder? value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.WriteIndent();
        this.stringBuilder.Append(value);
        return this;
    }

    /// <summary>
    /// Append the contents of the builder.
    /// </summary>
    /// <param name="value">The builder to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendIndent(CodeGenerator? value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.WriteIndent();
        this.stringBuilder.Append(value?.stringBuilder);
        return this;
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Append the contents of the builder in a range.
    /// </summary>
    /// <param name="value">The builder to append.</param>
    /// <param name="startIndex">The start index.</param>
    /// <param name="count">The count of characters to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Append(StringBuilder? value, int startIndex, int count)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Append(value, startIndex, count);
        return this;
    }

    /// <summary>
    /// Append the contents of the builder in a range.
    /// </summary>
    /// <param name="value">The builder to append.</param>
    /// <param name="startIndex">The start index.</param>
    /// <param name="count">The count of characters to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Append(CodeGenerator? value, int startIndex, int count)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Append(value?.stringBuilder, startIndex, count);
        return this;
    }

    /// <summary>
    /// Append the contents of the builder in a range.
    /// </summary>
    /// <param name="value">The builder to append.</param>
    /// <param name="startIndex">The start index.</param>
    /// <param name="count">The count of characters to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendIndent(StringBuilder? value, int startIndex, int count)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.WriteIndent();
        this.stringBuilder.Append(value, startIndex, count);
        return this;
    }

    /// <summary>
    /// Append the contents of the builder in a range.
    /// </summary>
    /// <param name="value">The builder to append.</param>
    /// <param name="startIndex">The start index.</param>
    /// <param name="count">The count of characters to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendIndent(CodeGenerator? value, int startIndex, int count)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.WriteIndent();
        this.stringBuilder.Append(value?.stringBuilder, startIndex, count);
        return this;
    }
#endif

    /// <summary>
    /// Append an empty line.
    /// </summary>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendLine()
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Append(this.LineEndSequence);
        return this;
    }

    /// <summary>
    /// Append a line.
    /// </summary>
    /// <param name="value">The text to append as a line.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendLine(string? value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Append(value).Append(this.LineEndSequence);
        return this;
    }

    /// <summary>
    /// Append a line.
    /// </summary>
    /// <param name="value">The text to append as a line.</param>
    /// <param name="trimWhitespaceOnlyLines">Whether to trim the appended line if it is whitespace only.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendLineIndent(string? value, bool trimWhitespaceOnlyLines = true)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        if (trimWhitespaceOnlyLines && string.IsNullOrWhiteSpace(value))
        {
            this.Append(this.LineEndSequence);
            return this;
        }

        this.WriteIndent();
        this.stringBuilder.Append(value).Append(this.LineEndSequence);
        return this;
    }

    /// <summary>
    /// Append multiple segments as an indented line.
    /// </summary>
    /// <param name="segments">The segements to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendLineIndent(params Segment[] segments)
    {
        for (int i = 0; i < segments.Length; ++i)
        {
            if (this.cancellationToken.IsCancellationRequested)
            {
                return this;
            }

            if (i == 0)
            {
                if (i == segments.Length - 1)
                {
                    segments[i].AppendLineIndent(this);
                }
                else
                {
                    segments[i].AppendIndent(this);
                }
            }
            else if (i == segments.Length - 1)
            {
                segments[i].AppendLine(this);
            }
            else
            {
                segments[i].Append(this);
            }
        }

        return this;
    }

    /// <summary>
    /// Append multiple segments with an initial indent.
    /// </summary>
    /// <param name="segments">The segements to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendIndent(params Segment[] segments)
    {
        for (int i = 0; i < segments.Length; ++i)
        {
            if (this.cancellationToken.IsCancellationRequested)
            {
                return this;
            }

            if (i == 0)
            {
                segments[i].AppendIndent(this);
            }
            else
            {
                segments[i].Append(this);
            }
        }

        return this;
    }

    /// <summary>
    /// Append multiple segments with an initial indent.
    /// </summary>
    /// <param name="segments">The segements to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendLine(params Segment[] segments)
    {
        for (int i = 0; i < segments.Length; ++i)
        {
            if (this.cancellationToken.IsCancellationRequested)
            {
                return this;
            }

            if (i == segments.Length - 1)
            {
                segments[i].AppendLine(this);
            }
            else
            {
                segments[i].Append(this);
            }
        }

        return this;
    }

    /// <summary>
    /// Copy the builder to an output array.
    /// </summary>
    /// <param name="sourceIndex">The start index for the copy.</param>
    /// <param name="destination">The destination array.</param>
    /// <param name="destinationIndex">The index in the destination at which to start the copy operation.</param>
    /// <param name="count">The number of characters to copy.</param>
    public void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return;
        }

        this.stringBuilder.CopyTo(sourceIndex, destination, destinationIndex, count);
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Copy the builder to an output span.
    /// </summary>
    /// <param name="sourceIndex">The start index for the copy.</param>
    /// <param name="destination">The destination span.</param>
    /// <param name="count">The number of characters to copy.</param>
    public void CopyTo(int sourceIndex, Span<char> destination, int count)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return;
        }

        this.stringBuilder.CopyTo(sourceIndex, destination, count);
    }
#endif

    /// <summary>
    /// Inserts a string 0 or more times into this builder at the specified position.
    /// </summary>
    /// <param name="index">The index to insert in this builder.</param>
    /// <param name="value">The string to insert.</param>
    /// <param name="count">The number of times to insert the string.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Insert(int index, string? value, int count)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Insert(index, value, count);
        return this;
    }

    /// <summary>
    /// Removes a range of characters from this builder.
    /// </summary>
    /// <param name="startIndex">The start index of the sequence of characters to remove.</param>
    /// <param name="length">The length of the sequence to remove.</param>
    /// <remarks>
    /// This method does not reduce the capacity of this builder.
    /// </remarks>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Remove(int startIndex, int length)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Remove(startIndex, length);
        return this;
    }

    /// <summary>
    /// Appends and formats a value to the builder.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Append(bool value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Append(value);
        return this;
    }

    /// <summary>
    /// Appends and formats a value to the builder.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Append(char value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Append(value);
        return this;
    }

    /// <summary>
    /// Appends and formats a value to the builder.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Append(sbyte value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Append(value);
        return this;
    }

    /// <summary>
    /// Appends and formats a value to the builder.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Append(byte value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Append(value);
        return this;
    }

    /// <summary>
    /// Appends and formats a value to the builder.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Append(short value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Append(value);
        return this;
    }

    /// <summary>
    /// Appends and formats a value to the builder.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Append(int value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Append(value);
        return this;
    }

    /// <summary>
    /// Appends and formats a value to the builder.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Append(long value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Append(value);
        return this;
    }

    /// <summary>
    /// Appends and formats a value to the builder.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Append(float value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Append(value);
        return this;
    }

    /// <summary>
    /// Appends and formats a value to the builder.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Append(double value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Append(value);
        return this;
    }

    /// <summary>
    /// Appends and formats a value to the builder.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Append(decimal value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Append(value);
        return this;
    }

    /// <summary>
    /// Appends and formats a value to the builder.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Append(ushort value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Append(value);
        return this;
    }

    /// <summary>
    /// Appends and formats a value to the builder.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Append(uint value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Append(value);
        return this;
    }

    /// <summary>
    /// Appends and formats a value to the builder.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Append(ulong value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Append(value);
        return this;
    }

    /// <summary>
    /// Appends and formats a value to the builder.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Append(object? value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Append(value);
        return this;
    }

    /// <summary>
    /// Appends and formats a value to the builder.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Append(char[]? value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Append(value);
        return this;
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Appends and formats a value to the builder.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Append(ReadOnlySpan<char> value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Append(value);
        return this;
    }
#endif

    /// <summary>
    /// Appends and formats a value to the builder.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Append(ReadOnlyMemory<char> value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Append(value);
        return this;
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Appends a formatted string to the builder, joining the parameters with a separator string.
    /// </summary>
    /// <param name="separator">The separator.</param>
    /// <param name="values">The values to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendJoin(string? separator, params object?[] values)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.AppendJoin(separator, values);
        return this;
    }

    /// <summary>
    /// Appends a formatted string to the builder, joining the parameters with a separator string.
    /// </summary>
    /// <typeparam name="TValue">The type of the values to append.</typeparam>
    /// <param name="separator">The separator.</param>
    /// <param name="values">The values to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendJoin<TValue>(string? separator, IEnumerable<TValue> values)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.AppendJoin(separator, values);
        return this;
    }

    /// <summary>
    /// Appends a formatted string to the builder, joining the parameters with a separator string.
    /// </summary>
    /// <param name="separator">The separator.</param>
    /// <param name="values">The values to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendJoin(string? separator, params string?[] values)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.AppendJoin(separator, values);
        return this;
    }

    /// <summary>
    /// Appends a formatted string to the builder, joining the parameters with a separator string.
    /// </summary>
    /// <param name="separator">The separator.</param>
    /// <param name="values">The values to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendJoin(char separator, params object?[] values)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.AppendJoin(separator, values);
        return this;
    }

    /// <summary>
    /// Appends a formatted string to the builder, joining the parameters with a separator string.
    /// </summary>
    /// <typeparam name="TValue">The type of the values to append.</typeparam>
    /// <param name="separator">The separator.</param>
    /// <param name="values">The values to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendJoin<TValue>(char separator, IEnumerable<TValue> values)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.AppendJoin(separator, values);
        return this;
    }

    /// <summary>
    /// Appends a formatted string to the builder, joining the parameters with a separator string.
    /// </summary>
    /// <param name="separator">The separator.</param>
    /// <param name="values">The values to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendJoin(char separator, params string?[] values)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.AppendJoin(separator, values);
        return this;
    }
#endif

    /// <summary>
    /// Insert the string at the index.
    /// </summary>
    /// <param name="index">The index at which to insert the string.</param>
    /// <param name="value">The string to insert.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Insert(int index, string? value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Insert(index, value);
        return this;
    }

    /// <summary>
    /// Insert the string at the index.
    /// </summary>
    /// <param name="index">The index at which to insert the string.</param>
    /// <param name="value">The string to insert.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Insert(int index, bool value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Insert(index, value);
        return this;
    }

    /// <summary>
    /// Insert the string at the index.
    /// </summary>
    /// <param name="index">The index at which to insert the string.</param>
    /// <param name="value">The string to insert.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Insert(int index, sbyte value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Insert(index, value);
        return this;
    }

    /// <summary>
    /// Insert the string at the index.
    /// </summary>
    /// <param name="index">The index at which to insert the string.</param>
    /// <param name="value">The string to insert.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Insert(int index, byte value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Insert(index, value);
        return this;
    }

    /// <summary>
    /// Insert the string at the index.
    /// </summary>
    /// <param name="index">The index at which to insert the string.</param>
    /// <param name="value">The string to insert.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Insert(int index, short value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Insert(index, value);
        return this;
    }

    /// <summary>
    /// Insert the string at the index.
    /// </summary>
    /// <param name="index">The index at which to insert the string.</param>
    /// <param name="value">The string to insert.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Insert(int index, char value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Insert(index, value);
        return this;
    }

    /// <summary>
    /// Insert the string at the index.
    /// </summary>
    /// <param name="index">The index at which to insert the string.</param>
    /// <param name="value">The string to insert.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Insert(int index, char[]? value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Insert(index, value);
        return this;
    }

    /// <summary>
    /// Insert the string at the index.
    /// </summary>
    /// <param name="index">The index at which to insert the string.</param>
    /// <param name="value">The string to insert.</param>
    /// <param name="startIndex">The start index in the value to insert.</param>
    /// <param name="charCount">The number of characters to insert.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Insert(int index, char[]? value, int startIndex, int charCount)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Insert(index, value, startIndex, charCount);
        return this;
    }

    /// <summary>
    /// Insert the string at the index.
    /// </summary>
    /// <param name="index">The index at which to insert the string.</param>
    /// <param name="value">The string to insert.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Insert(int index, int value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Insert(index, value);
        return this;
    }

    /// <summary>
    /// Insert the string at the index.
    /// </summary>
    /// <param name="index">The index at which to insert the string.</param>
    /// <param name="value">The string to insert.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Insert(int index, long value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Insert(index, value);
        return this;
    }

    /// <summary>
    /// Insert the string at the index.
    /// </summary>
    /// <param name="index">The index at which to insert the string.</param>
    /// <param name="value">The string to insert.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Insert(int index, float value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Insert(index, value);
        return this;
    }

    /// <summary>
    /// Insert the string at the index.
    /// </summary>
    /// <param name="index">The index at which to insert the string.</param>
    /// <param name="value">The string to insert.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Insert(int index, double value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Insert(index, value);
        return this;
    }

    /// <summary>
    /// Insert the string at the index.
    /// </summary>
    /// <param name="index">The index at which to insert the string.</param>
    /// <param name="value">The string to insert.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Insert(int index, decimal value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Insert(index, value);
        return this;
    }

    /// <summary>
    /// Insert the string at the index.
    /// </summary>
    /// <param name="index">The index at which to insert the string.</param>
    /// <param name="value">The string to insert.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Insert(int index, ushort value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Insert(index, value);
        return this;
    }

    /// <summary>
    /// Insert the string at the index.
    /// </summary>
    /// <param name="index">The index at which to insert the string.</param>
    /// <param name="value">The string to insert.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Insert(int index, uint value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Insert(index, value);
        return this;
    }

    /// <summary>
    /// Insert the string at the index.
    /// </summary>
    /// <param name="index">The index at which to insert the string.</param>
    /// <param name="value">The string to insert.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Insert(int index, ulong value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Insert(index, value);
        return this;
    }

    /// <summary>
    /// Insert the string at the index.
    /// </summary>
    /// <param name="index">The index at which to insert the string.</param>
    /// <param name="value">The string to insert.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Insert(int index, object? value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Insert(index, value);
        return this;
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Insert the string at the index.
    /// </summary>
    /// <param name="index">The index at which to insert the string.</param>
    /// <param name="value">The string to insert.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator Insert(int index, ReadOnlySpan<char> value)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Insert(index, value);
        return this;
    }

    /// <summary>
    /// Formatted append.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <param name="arg0">The argument.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendFormat([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, object? arg0)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.AppendFormat(format, arg0);
        return this;
    }

    /// <summary>
    /// Formatted append.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <param name="arg0">The first argument.</param>
    /// <param name="arg1">The second argument.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendFormat([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, object? arg0, object? arg1)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.AppendFormat(format, arg0, arg1);
        return this;
    }

    /// <summary>
    /// Formatted append.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <param name="arg0">The first argument.</param>
    /// <param name="arg1">The second argument.</param>
    /// <param name="arg2">The third argument.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendFormat([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, object? arg0, object? arg1, object? arg2)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.AppendFormat(format, arg0, arg1, arg2);
        return this;
    }

    /// <summary>
    /// Formatted append.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <param name="args">The arguments to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendFormat([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, params object?[] args)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.AppendFormat(format, args);
        return this;
    }

    /// <summary>
    /// Formatted append.
    /// </summary>
    /// <param name="provider">The format provider.</param>
    /// <param name="format">The composite format string.</param>
    /// <param name="arg0">The argument.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendFormat(IFormatProvider? provider, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, object? arg0)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.AppendFormat(provider, format, arg0);
        return this;
    }

    /// <summary>
    /// Formatted append.
    /// </summary>
    /// <param name="provider">The format provider.</param>
    /// <param name="format">The composite format string.</param>
    /// <param name="arg0">The first argument.</param>
    /// <param name="arg1">The second argument.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendFormat(IFormatProvider? provider, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, object? arg0, object? arg1)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.AppendFormat(provider, format, arg0, arg1);
        return this;
    }

    /// <summary>
    /// Formatted append.
    /// </summary>
    /// <param name="provider">The format provider.</param>
    /// <param name="format">The composite format string.</param>
    /// <param name="arg0">The first argument.</param>
    /// <param name="arg1">The second argument.</param>
    /// <param name="arg2">The third argument.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendFormat(IFormatProvider? provider, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, object? arg0, object? arg1, object? arg2)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.AppendFormat(provider, format, arg0, arg1, arg2);
        return this;
    }

    /// <summary>
    /// Formatted append.
    /// </summary>
    /// <param name="provider">The format provider.</param>
    /// <param name="format">The composite format string.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendFormat(IFormatProvider? provider, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, params object?[] args)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.AppendFormat(provider, format, args);
        return this;
    }

    /// <summary>
    /// Appends the string returned by processing a composite format string, which contains zero or more format items, to this instance.
    /// Each format item is replaced by the string representation of any of the arguments using a specified format provider.
    /// </summary>
    /// <typeparam name="TArg0">The type of the first object to format.</typeparam>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <param name="format">A <see cref="CompositeFormat"/>.</param>
    /// <param name="arg0">The first object to format.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="format"/> is null.</exception>
    /// <exception cref="FormatException">The index of a format item is greater than or equal to the number of supplied arguments.</exception>
    public CodeGenerator AppendFormat<TArg0>(IFormatProvider? provider, CompositeFormat format, TArg0 arg0)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.AppendFormat(provider, format, arg0);
        return this;
    }

    /// <summary>
    /// Appends the string returned by processing a composite format string, which contains zero or more format items, to this instance.
    /// Each format item is replaced by the string representation of any of the arguments using a specified format provider.
    /// </summary>
    /// <typeparam name="TArg0">The type of the first object to format.</typeparam>
    /// <typeparam name="TArg1">The type of the second object to format.</typeparam>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <param name="format">A <see cref="CompositeFormat"/>.</param>
    /// <param name="arg0">The first object to format.</param>
    /// <param name="arg1">The second object to format.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="format"/> is null.</exception>
    /// <exception cref="FormatException">The index of a format item is greater than or equal to the number of supplied arguments.</exception>
    public CodeGenerator AppendFormat<TArg0, TArg1>(IFormatProvider? provider, CompositeFormat format, TArg0 arg0, TArg1 arg1)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.AppendFormat(provider, format, arg0, arg1);
        return this;
    }

    /// <summary>
    /// Appends the string returned by processing a composite format string, which contains zero or more format items, to this instance.
    /// Each format item is replaced by the string representation of any of the arguments using a specified format provider.
    /// </summary>
    /// <typeparam name="TArg0">The type of the first object to format.</typeparam>
    /// <typeparam name="TArg1">The type of the second object to format.</typeparam>
    /// <typeparam name="TArg2">The type of the third object to format.</typeparam>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <param name="format">A <see cref="CompositeFormat"/>.</param>
    /// <param name="arg0">The first object to format.</param>
    /// <param name="arg1">The second object to format.</param>
    /// <param name="arg2">The third object to format.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="format"/> is null.</exception>
    /// <exception cref="FormatException">The index of a format item is greater than or equal to the number of supplied arguments.</exception>
    public CodeGenerator AppendFormat<TArg0, TArg1, TArg2>(IFormatProvider? provider, CompositeFormat format, TArg0 arg0, TArg1 arg1, TArg2 arg2)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.AppendFormat(provider, format, arg0, arg1, arg2);
        return this;
    }

    /// <summary>
    /// Appends the string returned by processing a composite format string, which contains zero or more format items, to this instance.
    /// Each format item is replaced by the string representation of any of the arguments using a specified format provider.
    /// </summary>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <param name="format">A <see cref="CompositeFormat"/>.</param>
    /// <param name="args">An array of objects to format.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="format"/> is null.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="args"/> is null.</exception>
    /// <exception cref="FormatException">The index of a format item is greater than or equal to the number of supplied arguments.</exception>
    public CodeGenerator AppendFormat(IFormatProvider? provider, CompositeFormat format, params object?[] args)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.AppendFormat(provider, format, args);
        return this;
    }

    /// <summary>
    /// Appends the string returned by processing a composite format string, which contains zero or more format items, to this instance.
    /// Each format item is replaced by the string representation of any of the arguments using a specified format provider.
    /// </summary>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <param name="format">A <see cref="CompositeFormat"/>.</param>
    /// <param name="args">A span of objects to format.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="format"/> is null.</exception>
    /// <exception cref="FormatException">The index of a format item is greater than or equal to the number of supplied arguments.</exception>
    public CodeGenerator AppendFormat(IFormatProvider? provider, CompositeFormat format, ReadOnlySpan<object?> args)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.AppendFormat(provider, format, args);
        return this;
    }

    /// <summary>
    /// Formatted append.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <param name="arg0">The argument.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendFormatIndent([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, object? arg0)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.WriteIndent();
        this.stringBuilder.AppendFormat(format, arg0);
        return this;
    }

    /// <summary>
    /// Formatted append.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <param name="arg0">The first argument.</param>
    /// <param name="arg1">The second argument.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendFormatIndent([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, object? arg0, object? arg1)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.WriteIndent();
        this.stringBuilder.AppendFormat(format, arg0, arg1);
        return this;
    }

    /// <summary>
    /// Formatted append.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <param name="arg0">The first argument.</param>
    /// <param name="arg1">The second argument.</param>
    /// <param name="arg2">The third argument.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendFormatIndent([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, object? arg0, object? arg1, object? arg2)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.WriteIndent();
        this.stringBuilder.AppendFormat(format, arg0, arg1, arg2);
        return this;
    }

    /// <summary>
    /// Formatted append.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <param name="args">The arguments to append.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendFormatIndent([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, params object?[] args)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.WriteIndent();
        this.stringBuilder.AppendFormat(format, args);
        return this;
    }

    /// <summary>
    /// Formatted append.
    /// </summary>
    /// <param name="provider">The format provider.</param>
    /// <param name="format">The composite format string.</param>
    /// <param name="arg0">The argument.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendFormatIndent(IFormatProvider? provider, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, object? arg0)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.WriteIndent();
        this.stringBuilder.AppendFormat(provider, format, arg0);
        return this;
    }

    /// <summary>
    /// Formatted append.
    /// </summary>
    /// <param name="provider">The format provider.</param>
    /// <param name="format">The composite format string.</param>
    /// <param name="arg0">The first argument.</param>
    /// <param name="arg1">The second argument.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendFormatIndent(IFormatProvider? provider, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, object? arg0, object? arg1)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.WriteIndent();
        this.stringBuilder.AppendFormat(provider, format, arg0, arg1);
        return this;
    }

    /// <summary>
    /// Formatted append.
    /// </summary>
    /// <param name="provider">The format provider.</param>
    /// <param name="format">The composite format string.</param>
    /// <param name="arg0">The first argument.</param>
    /// <param name="arg1">The second argument.</param>
    /// <param name="arg2">The third argument.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendFormatIndent(IFormatProvider? provider, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, object? arg0, object? arg1, object? arg2)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.WriteIndent();
        this.stringBuilder.AppendFormat(provider, format, arg0, arg1, arg2);
        return this;
    }

    /// <summary>
    /// Formatted append.
    /// </summary>
    /// <param name="provider">The format provider.</param>
    /// <param name="format">The composite format string.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public CodeGenerator AppendFormatIndent(IFormatProvider? provider, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, params object?[] args)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.WriteIndent();
        this.stringBuilder.AppendFormat(provider, format, args);
        return this;
    }

    /// <summary>
    /// Appends the string returned by processing a composite format string, which contains zero or more format items, to this instance.
    /// Each format item is replaced by the string representation of any of the arguments using a specified format provider.
    /// </summary>
    /// <typeparam name="TArg0">The type of the first object to format.</typeparam>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <param name="format">A <see cref="CompositeFormat"/>.</param>
    /// <param name="arg0">The first object to format.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="format"/> is null.</exception>
    /// <exception cref="FormatException">The index of a format item is greater than or equal to the number of supplied arguments.</exception>
    public CodeGenerator AppendFormatIndent<TArg0>(IFormatProvider? provider, CompositeFormat format, TArg0 arg0)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.WriteIndent();
        this.stringBuilder.AppendFormat(provider, format, arg0);
        return this;
    }

    /// <summary>
    /// Appends the string returned by processing a composite format string, which contains zero or more format items, to this instance.
    /// Each format item is replaced by the string representation of any of the arguments using a specified format provider.
    /// </summary>
    /// <typeparam name="TArg0">The type of the first object to format.</typeparam>
    /// <typeparam name="TArg1">The type of the second object to format.</typeparam>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <param name="format">A <see cref="CompositeFormat"/>.</param>
    /// <param name="arg0">The first object to format.</param>
    /// <param name="arg1">The second object to format.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="format"/> is null.</exception>
    /// <exception cref="FormatException">The index of a format item is greater than or equal to the number of supplied arguments.</exception>
    public CodeGenerator AppendFormatIndent<TArg0, TArg1>(IFormatProvider? provider, CompositeFormat format, TArg0 arg0, TArg1 arg1)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.WriteIndent();
        this.stringBuilder.AppendFormat(provider, format, arg0, arg1);
        return this;
    }

    /// <summary>
    /// Appends the string returned by processing a composite format string, which contains zero or more format items, to this instance.
    /// Each format item is replaced by the string representation of any of the arguments using a specified format provider.
    /// </summary>
    /// <typeparam name="TArg0">The type of the first object to format.</typeparam>
    /// <typeparam name="TArg1">The type of the second object to format.</typeparam>
    /// <typeparam name="TArg2">The type of the third object to format.</typeparam>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <param name="format">A <see cref="CompositeFormat"/>.</param>
    /// <param name="arg0">The first object to format.</param>
    /// <param name="arg1">The second object to format.</param>
    /// <param name="arg2">The third object to format.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="format"/> is null.</exception>
    /// <exception cref="FormatException">The index of a format item is greater than or equal to the number of supplied arguments.</exception>
    public CodeGenerator AppendFormatIndent<TArg0, TArg1, TArg2>(IFormatProvider? provider, CompositeFormat format, TArg0 arg0, TArg1 arg1, TArg2 arg2)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.WriteIndent();
        this.stringBuilder.AppendFormat(provider, format, arg0, arg1, arg2);
        return this;
    }

    /// <summary>
    /// Appends the string returned by processing a composite format string, which contains zero or more format items, to this instance.
    /// Each format item is replaced by the string representation of any of the arguments using a specified format provider.
    /// </summary>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <param name="format">A <see cref="CompositeFormat"/>.</param>
    /// <param name="args">An array of objects to format.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="format"/> is null.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="args"/> is null.</exception>
    /// <exception cref="FormatException">The index of a format item is greater than or equal to the number of supplied arguments.</exception>
    public CodeGenerator AppendFormatIndent(IFormatProvider? provider, CompositeFormat format, params object?[] args)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.WriteIndent();
        this.stringBuilder.AppendFormat(provider, format, args);
        return this;
    }

    /// <summary>
    /// Appends the string returned by processing a composite format string, which contains zero or more format items, to this instance.
    /// Each format item is replaced by the string representation of any of the arguments using a specified format provider.
    /// </summary>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <param name="format">A <see cref="CompositeFormat"/>.</param>
    /// <param name="args">A span of objects to format.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="format"/> is null.</exception>
    /// <exception cref="FormatException">The index of a format item is greater than or equal to the number of supplied arguments.</exception>
    public CodeGenerator AppendFormatIndent(IFormatProvider? provider, CompositeFormat format, ReadOnlySpan<object?> args)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.WriteIndent();
        this.stringBuilder.AppendFormat(provider, format, args);
        return this;
    }

#endif

    /// <summary>
    /// Replaces all instances of one string with another in this builder.
    /// </summary>
    /// <param name="oldValue">The string to replace.</param>
    /// <param name="newValue">The string to replace <paramref name="oldValue"/> with.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    /// <remarks>
    /// If <paramref name="newValue"/> is <c>null</c>, instances of <paramref name="oldValue"/>
    /// are removed from this builder.
    /// </remarks>
    public CodeGenerator Replace(string oldValue, string? newValue)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Replace(oldValue, newValue);
        return this;
    }

    /// <summary>
    /// Determines if the contents of this builder are equal to the contents of another builder.
    /// </summary>
    /// <param name="sb">The other builder.</param>
    /// <returns><see langword="true"/> if the values are equal.</returns>
    public bool Equals([NotNullWhen(true)] StringBuilder? sb)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return this.stringBuilder.Equals(sb);
    }

    /// <summary>
    /// Determines if the contents of this builder are equal to the contents of another builder.
    /// </summary>
    /// <param name="sb">The other builder.</param>
    /// <returns><see langword="true"/> if the values are equal.</returns>
    public bool Equals([NotNullWhen(true)] CodeGenerator? sb)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return this.stringBuilder.Equals(sb?.stringBuilder);
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Determines if the contents of this builder are equal to the contents of <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    /// <param name="span">The <see cref="ReadOnlySpan{Char}"/>.</param>
    /// <returns><see langword="true"/> if the values are equal.</returns>
    public bool Equals(ReadOnlySpan<char> span)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return this.stringBuilder.Equals(span);
    }
#endif

    /// <summary>
    /// Replaces all instances of one string with another in part of this builder.
    /// </summary>
    /// <param name="oldValue">The string to replace.</param>
    /// <param name="newValue">The string to replace <paramref name="oldValue"/> with.</param>
    /// <param name="startIndex">The index to start in this builder.</param>
    /// <param name="count">The number of characters to read in this builder.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    /// <remarks>
    /// If <paramref name="newValue"/> is <c>null</c>, instances of <paramref name="oldValue"/>
    /// are removed from this builder.
    /// </remarks>
    public CodeGenerator Replace(string oldValue, string? newValue, int startIndex, int count)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Replace(oldValue, newValue, startIndex, count);
        return this;
    }

    /// <summary>
    /// Replaces all instances of one character with another in this builder.
    /// </summary>
    /// <param name="oldChar">The character to replace.</param>
    /// <param name="newChar">The character to replace <paramref name="oldChar"/> with.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    public CodeGenerator Replace(char oldChar, char newChar)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Replace(oldChar, newChar);
        return this;
    }

    /// <summary>
    /// Replaces all instances of one character with another in this builder.
    /// </summary>
    /// <param name="oldChar">The character to replace.</param>
    /// <param name="newChar">The character to replace <paramref name="oldChar"/> with.</param>
    /// <param name="startIndex">The index to start in this builder.</param>
    /// <param name="count">The number of characters to read in this builder.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    public CodeGenerator Replace(char oldChar, char newChar, int startIndex, int count)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.stringBuilder.Replace(oldChar, newChar, startIndex, count);
        return this;
    }

    /// <summary>
    /// Reserve a specific name in the scope.
    /// </summary>
    /// <param name="memberName">The name to reserve.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    /// <exception cref="ArgumentException">The name is already in use in the scope.</exception>
    public CodeGenerator ReserveName(MemberName memberName)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        if (!this.TryReserveName(memberName))
        {
            throw new ArgumentException("The name is already reserved in this scope.", nameof(memberName));
        }

        return this;
    }

    /// <summary>
    /// Reserve a specific name in the scope if it is not already reserved.
    /// </summary>
    /// <param name="memberName">The name to reserve.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    /// <exception cref="ArgumentException">The name is already in use in the scope.</exception>
    public CodeGenerator ReserveNameIfNotReserved(MemberName memberName)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return this;
        }

        this.TryReserveName(memberName);
        return this;
    }

    /// <summary>
    /// Reserve a specific name in the scope.
    /// </summary>
    /// <param name="memberName">The name to reserve.</param>
    /// <returns><see langword="true"/> if the member name could be reserved in the scope.</returns>
    /// <exception cref="ArgumentException">The name is already in use in the scope.</exception>
    public bool TryReserveName(MemberName memberName)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        if (this.memberNames.TryGetValue(memberName, out _))
        {
            return true;
        }

        string baseName = memberName.BuildName();

        if (this.memberNamesByScope.TryGetValue(memberName.FullyQualifiedScope, out HashSet<string>? memberNamesForScope))
        {
            bool result = memberNamesForScope.Add(baseName);

            if (result)
            {
                this.memberNames.Add(memberName, baseName);
            }

            return result;
        }

        memberNamesForScope = [];
        memberNamesForScope.Add(baseName);
        this.memberNamesByScope.Add(memberName.FullyQualifiedScope, memberNamesForScope);

        return true;
    }

    /// <summary>
    /// Get or add a member name.
    /// </summary>
    /// <param name="memberName">The member name to get or add.</param>
    /// <returns>The formatted member name.</returns>
    /// <remarks>
    /// The <see cref="MemberName"/> overrides <see cref="MemberName.Equals(object?)"/>
    /// and <see cref="MemberName.GetHashCode()"/> to ensure that you always get the same member name
    /// for the same specification.
    /// </remarks>
    public string GetOrAddMemberName(MemberName memberName)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return string.Empty;
        }

        if (this.memberNames.TryGetValue(memberName, out string? name))
        {
            return name;
        }

        return this.AddName(memberName);
    }

    /// <summary>
    /// Get a unique member name.
    /// </summary>
    /// <param name="memberName">The member name to get or add.</param>
    /// <returns>The formatted member name.</returns>
    /// <remarks>
    /// <para>
    /// The <see cref="MemberName"/> overrides <see cref="MemberName.Equals(object?)"/>
    /// and <see cref="MemberName.GetHashCode()"/> to ensure that you always get the same member name
    /// for the same specification.
    /// </para>
    /// </remarks>
    public string GetUniqueMemberName(MemberName memberName)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return string.Empty;
        }

        return this.AddName(memberName);
    }

    /// <summary>
    /// Get the child scope, based on this scope.
    /// </summary>
    /// <param name="childScope">The dotted-path to the child scope.</param>
    /// <param name="rootScope">The optional explicit root scope.</param>
    /// <returns>The child scope.</returns>
    public string GetChildScope(string? childScope, string? rootScope)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return string.Empty;
        }

        return childScope is string c ? $"{rootScope ?? this.FullyQualifiedScope}.{c}" : this.FullyQualifiedScope;
    }

    /// <summary>
    /// Gets the generated code files.
    /// </summary>
    /// <param name="getFileNameDescription">A function which produces a <see cref="FileNameDescription"/> for the given <see cref="TypeDeclaration"/>.</param>
    /// <returns>The collection of generated code files.</returns>
    public IReadOnlyCollection<GeneratedCodeFile> GetGeneratedCodeFiles(Func<TypeDeclaration, FileNameDescription> getFileNameDescription)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return [];
        }

        List<GeneratedCodeFile> generatedCode = [];
        HashSet<string> uniqueFileNames = [];

        foreach (KeyValuePair<TypeDeclaration, Dictionary<string, string>> kvp in this.generatedFiles)
        {
            FileNameDescription fileNameDescription = getFileNameDescription(kvp.Key);
            foreach (KeyValuePair<string, string> fileAndContent in kvp.Value)
            {
                generatedCode.Add(
                    new(
                        kvp.Key,
                        GetFileName(fileNameDescription, fileAndContent.Key, uniqueFileNames),
                        fileAndContent.Value));
            }
        }

        return generatedCode;
    }

    private static string GetFileName(FileNameDescription fileNameDescription, string fileName, HashSet<string> uniqueFileNames)
    {
        string candidateName = GetBaseFileName(fileNameDescription, fileName);
        string baseFileNameWithoutExtension = Path.GetFileNameWithoutExtension(candidateName);
        string extension = Path.GetExtension(candidateName);

        int index = 1;

        while (!uniqueFileNames.Add(candidateName))
        {
            candidateName = $"{baseFileNameWithoutExtension}{index}.{extension}";
            index++;
        }

        return candidateName;

        static string GetBaseFileName(FileNameDescription fileNameDescription, string fileName)
        {
            if (fileNameDescription.Extension is string extension)
            {
                if (fileName.Length == 0)
                {
                    return $"{fileNameDescription.BaseFileName}{extension}";
                }

                return $"{fileNameDescription.BaseFileName}{fileNameDescription.Separator}{fileName}{extension}";
            }

            if (fileName.Length == 0)
            {
                return fileNameDescription.BaseFileName;
            }

            return $"{fileNameDescription.BaseFileName}{fileNameDescription.Separator}{fileName}";
        }
    }

    private string AddName(MemberName memberName)
    {
        if (!this.memberNamesByScope.TryGetValue(memberName.FullyQualifiedScope, out HashSet<string>? memberNamesForScope))
        {
            memberNamesForScope = [];
            this.memberNamesByScope.Add(memberName.FullyQualifiedScope, memberNamesForScope);
        }

        string baseName = memberName.BuildName();

        int index = 0;

        while (true)
        {
            string name = index == 0 ? baseName : $"{baseName}{index}";
            if (memberNamesForScope.Add(name))
            {
                this.memberNames.TryAdd(memberName, name);
                return name;
            }

            ++index;
        }
    }

    private void WriteIndent()
    {
        for (int i = 0; i < this.indentationLevel; ++i)
        {
            this.stringBuilder.Append(this.indentSequence);
        }
    }

    private string BuildFullyQualifiedScope(string? additionalScope = null)
    {
        IEnumerable<ScopeValue> scope = this.scope.Reverse();

        if (additionalScope is string s)
        {
            scope = scope.Append(new(s, 0));
        }

        return string.Join(".", scope.Select(s => s.Name));
    }

    /// <summary>
    /// A segment to append.
    /// </summary>
    public readonly struct Segment
    {
        private readonly string? segment;
        private readonly Action<CodeGenerator>? segmentFunc;

        private Segment(string segment)
        {
            this.segment = segment;
            this.segmentFunc = null;
        }

        private Segment(Action<CodeGenerator> segmentFunc)
        {
            this.segment = null;
            this.segmentFunc = segmentFunc;
        }

        /// <summary>
        /// Conversion from <see langword="string"/>.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator Segment(string value) => new(value);

        /// <summary>
        /// Conversion from <see cref="Action{CodeGenerator}"/>.
        /// </summary>
        /// <param name="value">The value from which to convert.</param>
        public static implicit operator Segment(Action<CodeGenerator> value) => new(value);

        /// <summary>
        /// Append the segment to the generator.
        /// </summary>
        /// <param name="generator">The generator to which to append the segment.</param>
        /// <returns>A reference to the generator after the operation has completed.</returns>
        internal CodeGenerator Append(CodeGenerator generator)
        {
            if (this.segment is string s)
            {
                generator.Append(s);
            }
            else if (this.segmentFunc is Action<CodeGenerator> f)
            {
                f(generator);
            }

            return generator;
        }

        /// <summary>
        /// Append the segment to the generator with line end.
        /// </summary>
        /// <param name="generator">The generator to which to append the segment.</param>
        /// <returns>A reference to the generator after the operation has completed.</returns>
        internal CodeGenerator AppendLine(CodeGenerator generator)
        {
            this.Append(generator);
            return generator.AppendLine();
        }

        /// <summary>
        /// Append the segment to the generator with indent.
        /// </summary>
        /// <param name="generator">The generator to which to append the segment.</param>
        /// <returns>A reference to the generator after the operation has completed.</returns>
        internal CodeGenerator AppendIndent(CodeGenerator generator)
        {
            generator.WriteIndent();
            return this.Append(generator);
        }

        /// <summary>
        /// Append the segment to the generator with indent and line end.
        /// </summary>
        /// <param name="generator">The generator to which to append the segment.</param>
        /// <returns>A reference to the generator after the operation has completed.</returns>
        internal CodeGenerator AppendLineIndent(CodeGenerator generator)
        {
            generator.WriteIndent();
            this.Append(generator);
            return generator.AppendLine();
        }
    }

    private readonly struct ScopeValue(string name, int type)
    {
        public string Name { get; } = name;

        public int Type { get; } = type;
    }
}