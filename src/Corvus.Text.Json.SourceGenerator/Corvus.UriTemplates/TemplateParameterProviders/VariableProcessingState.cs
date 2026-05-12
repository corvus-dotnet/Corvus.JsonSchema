// <copyright file="VariableProcessingState.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.UriTemplates.TemplateParameterProviders;

/// <summary>
/// Used by <see cref="ITemplateParameterProvider{TParameterPayload}"/> to determine
/// the result of processing a variable with a set of parameters.
/// </summary>
public enum VariableProcessingState
{
    /// <summary>
    /// Processing succeeded.
    /// </summary>
    Success,

    /// <summary>
    /// The parameter was not present.
    /// </summary>
    NotProcessed,

    /// <summary>
    /// The parameter was not valid for the given variable.
    /// </summary>
    Failure,
}