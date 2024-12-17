// <copyright file="ITemplateParameterProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.HighPerformance;

namespace Corvus.UriTemplates.TemplateParameterProviders;

/// <summary>
/// Supplies parameters to the <see cref="UriTemplateResolver{TParameterProvider, TParameterPayload}"/>.
/// </summary>
/// <typeparam name="TParameterPayload">The type of the parameter.</typeparam>
/// <remarks>
/// This allows us to abstract our parameter provision mechanism to
/// efficiently format parameters into our output.
/// </remarks>
public interface ITemplateParameterProvider<TParameterPayload>
{
    /// <summary>
    /// Process the given variable.
    /// </summary>
    /// <param name="variableSpecification">The specification for the variable.</param>
    /// <param name="parameters">The parameters.</param>
    /// <param name="output">The output to which to format the parameter.</param>
    /// <returns>
    ///     <see cref="VariableProcessingState.Success"/> if the variable was successfully processed,
    ///     <see cref="VariableProcessingState.NotProcessed"/> if the parameter was not present, or
    ///     <see cref="VariableProcessingState.Failure"/> if the parameter could not be processed because it was incompatible with the variable specification in the template.</returns>
    VariableProcessingState ProcessVariable(ref VariableSpecification variableSpecification, in TParameterPayload parameters, ref ValueStringBuilder output);
}