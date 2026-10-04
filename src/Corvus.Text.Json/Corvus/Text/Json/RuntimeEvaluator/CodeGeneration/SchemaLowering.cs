// <copyright file="SchemaLowering.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET && !STJ
using System.Runtime.CompilerServices;
using System.Threading;
using Corvus.Text.Json.RuntimeEvaluator.Compilation;

namespace Corvus.Text.Json.RuntimeEvaluator.CodeGeneration;

/// <summary>
/// Lowers a compiled schema to generated code through an <see cref="ISchemaEmitter"/>: decides which nodes get a
/// method and what each does in place, leaving the rest to the interpreter.
/// </summary>
internal static class SchemaLowering
{
    private static int compiledCount;

    /// <summary>Gets the number of schemas compiled in this process (for tests).</summary>
    public static int CompiledCount => Volatile.Read(ref compiledCount);

    /// <summary>Whether generated code can run here (it needs Reflection.Emit and the JIT).</summary>
    public static bool IsSupported => RuntimeFeature.IsDynamicCodeSupported;

    /// <summary>Compiles the flag-mode code for an entry node.</summary>
    /// <param name="nodes">The program's nodes.</param>
    /// <param name="entry">The entry node (after reference elision).</param>
    /// <returns>The entry's generated method.</returns>
    public static NodeValidator Compile(SchemaNode[] nodes, SchemaNode entry)
    {
        Interlocked.Increment(ref compiledCount);
        var emitter = new IlSchemaEmitter();
        Lower(emitter, nodes, entry);
        return emitter.Build(entry.Id);
    }

    private static void Lower(ISchemaEmitter emitter, SchemaNode[] nodes, SchemaNode entry)
    {
        // Increment 1: the plumbing. The entry's method hands the whole evaluation to the interpreter; later
        // increments specialise node kinds and leave only the rest to it.
        emitter.BeginMethod(entry.Id);
        emitter.ReturnInterpreted(entry.Id);
        emitter.EndMethod();
    }
}
#endif