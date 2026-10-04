// <copyright file="IlSchemaEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET && !STJ
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.RuntimeEvaluator.Evaluation;

namespace Corvus.Text.Json.RuntimeEvaluator.CodeGeneration;

/// <summary>
/// Writes a schema's generated methods as IL: static methods of one type in a collectible assembly, so the JIT treats
/// them as ordinary methods (it inlines between them, which it does not do between dynamic methods) and the code is
/// unloaded with the schema. The assembly reads the evaluator's internal state through
/// <c>IgnoresAccessChecksToAttribute</c>.
/// </summary>
internal sealed class IlSchemaEmitter : ISchemaEmitter
{
    private static readonly Type[] NodeParameters = [typeof(EvaluationState).MakeByRefType(), typeof(IJsonDocument), typeof(int)];

    private static readonly MethodInfo EvalNodeFast = typeof(Evaluator).GetMethod(nameof(Evaluator.EvalNodeFast), BindingFlags.Static | BindingFlags.NonPublic)!;

    private readonly TypeBuilder type;
    private readonly Dictionary<int, MethodBuilder> methods = [];
    private ILGenerator? il;

    public IlSchemaEmitter()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Corvus.Text.Json.Schema." + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.RunAndCollect);
        ModuleBuilder module = assembly.DefineDynamicModule("Schema");
        AllowAccessTo(assembly, module, typeof(Evaluator).Assembly.GetName().Name!);
        this.type = module.DefineType("Schema", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract);
    }

    /// <inheritdoc/>
    public void BeginMethod(int nodeId)
    {
        this.il = this.Method(nodeId).GetILGenerator();
    }

    /// <inheritdoc/>
    public void ReturnInterpreted(int nodeId)
    {
        ILGenerator il = this.il!;
        il.Emit(OpCodes.Ldc_I4, nodeId);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, EvalNodeFast);
        il.Emit(OpCodes.Ret);
    }

    /// <inheritdoc/>
    public void EndMethod()
    {
        this.il = null;
    }

    /// <summary>Creates the type and returns the method for a node.</summary>
    /// <param name="nodeId">The node.</param>
    /// <returns>Its method.</returns>
    public NodeValidator Build(int nodeId)
    {
        Type created = this.type.CreateType();
        return created.GetMethod(this.methods[nodeId].Name)!.CreateDelegate<NodeValidator>();
    }

    private MethodBuilder Method(int nodeId)
    {
        if (!this.methods.TryGetValue(nodeId, out MethodBuilder? method))
        {
            method = this.type.DefineMethod("N" + nodeId, MethodAttributes.Public | MethodAttributes.Static, typeof(bool), NodeParameters);
            method.DefineParameter(1, ParameterAttributes.None, "state");
            method.DefineParameter(2, ParameterAttributes.None, "doc");
            method.DefineParameter(3, ParameterAttributes.None, "index");
            this.methods[nodeId] = method;
        }

        return method;
    }

    // [assembly: IgnoresAccessChecksTo(name)]: the runtime honours the attribute by name, so the dynamic assembly
    // defines it for itself.
    private static void AllowAccessTo(AssemblyBuilder assembly, ModuleBuilder module, string name)
    {
        TypeBuilder attribute = module.DefineType("System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute", TypeAttributes.Public | TypeAttributes.Class, typeof(Attribute));
        ConstructorBuilder ctor = attribute.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, [typeof(string)]);
        ILGenerator il = ctor.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, typeof(Attribute).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, Type.EmptyTypes)!);
        il.Emit(OpCodes.Ret);
        Type created = attribute.CreateType();
        assembly.SetCustomAttribute(new CustomAttributeBuilder(created.GetConstructor([typeof(string)])!, [name]));
    }
}
#endif