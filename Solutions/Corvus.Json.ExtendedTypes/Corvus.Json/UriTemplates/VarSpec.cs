// <copyright file="VarSpec.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// Derived from Tavis.UriTemplate https://github.com/tavis-software/Tavis.UriTemplates/blob/master/License.txt

namespace Corvus.Json.UriTemplates;

/// <summary>
/// A variable specification.
/// </summary>
internal ref struct VarSpec
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VarSpec"/> struct.
    /// </summary>
    /// <param name="operatorInfo">The operator info.</param>
    /// <param name="variableName">The variable name.</param>
    /// <param name="explode">Whether to explode the variable.</param>
    /// <param name="prefixLength">The prefix length.</param>
    /// <param name="first">Wether this is the first variable in the template.</param>
    public VarSpec(OperatorInfo operatorInfo, ReadOnlySpan<char> variableName, bool explode = false, int prefixLength = 0, bool first = true)
    {
        this.OperatorInfo = operatorInfo;
        this.Explode = explode;
        this.PrefixLength = prefixLength;
        this.First = first;
        this.VarName = variableName;
    }

    /// <summary>
    /// Gets the operation info.
    /// </summary>
    public OperatorInfo OperatorInfo { get; }

    /// <summary>
    /// Gets or sets the variable name.
    /// </summary>
    public ReadOnlySpan<char> VarName { get; set;  }

    /// <summary>
    /// Gets or sets a value indicating whether this variable is exploded.
    /// </summary>
    public bool Explode { get; set;  }

    /// <summary>
    /// Gets or sets the prefix length for the variable.
    /// </summary>
    public int PrefixLength { get; set;  }

    /// <summary>
    /// Gets or sets a value indicating whether this is the first variable in the template.
    /// </summary>
    public bool First { get; set; }

    /// <summary>
    /// Gets the variable as a string.
    /// </summary>
    /// <returns>The variable specification as a string.</returns>
    public override string ToString()
    {
        return (this.First ? this.OperatorInfo.First : string.Empty) +
               this.VarName.ToString()
               + (this.Explode ? "*" : string.Empty)
               + (this.PrefixLength > 0 ? ":" + this.PrefixLength : string.Empty);
    }
}