//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
#nullable enable
using Corvus.Json;

namespace Corvus.Json.JsonSchema.OpenApi30;
public readonly partial struct OpenApiDocument
{
    public readonly partial struct Components
    {
        public readonly partial struct ParametersEntity
        {
            /// <summary>
            /// Generated from JSON Schema.
            /// </summary>
            public readonly partial struct AZaZ09Entity
            {
                private ValidationContext ValidateOneOf(in ValidationContext validationContext, ValidationLevel level)
                {
                    ValidationContext result = validationContext;
                    if (level > ValidationLevel.Basic)
                    {
                        result = result.PushValidationLocationProperty("oneOf");
                    }

                    ValidationContext childContextBase = result;
                    int oneOfCount = 0;
                    ValidationContext childContext0 = childContextBase;
                    if (level > ValidationLevel.Basic)
                    {
                        childContext0 = childContext0.PushValidationLocationArrayIndex(0);
                    }

                    ValidationContext oneOfResult0 = this.As<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Reference>().Validate(childContext0.CreateChildContext(), level);
                    if (oneOfResult0.IsValid)
                    {
                        result = result.MergeChildContext(oneOfResult0, level >= ValidationLevel.Verbose);
                        oneOfCount += 1;
                        if (oneOfCount > 1 && level == ValidationLevel.Flag)
                        {
                            result = result.WithResult(isValid: false);
                            return result;
                        }
                    }
                    else
                    {
                        if (level >= ValidationLevel.Verbose)
                        {
                            result = result.MergeResults(result.IsValid, level, oneOfResult0);
                        }
                    }

                    ValidationContext childContext1 = childContextBase;
                    if (level > ValidationLevel.Basic)
                    {
                        childContext1 = childContext1.PushValidationLocationArrayIndex(1);
                    }

                    ValidationContext oneOfResult1 = this.As<Corvus.Json.JsonSchema.OpenApi30.OpenApiDocument.Parameter>().Validate(childContext1.CreateChildContext(), level);
                    if (oneOfResult1.IsValid)
                    {
                        result = result.MergeChildContext(oneOfResult1, level >= ValidationLevel.Verbose);
                        oneOfCount += 1;
                        if (oneOfCount > 1 && level == ValidationLevel.Flag)
                        {
                            result = result.WithResult(isValid: false);
                            return result;
                        }
                    }
                    else
                    {
                        if (level >= ValidationLevel.Verbose)
                        {
                            result = result.MergeResults(result.IsValid, level, oneOfResult1);
                        }
                    }

                    if (oneOfCount == 1)
                    {
                        if (level >= ValidationLevel.Verbose)
                        {
                            result = result.WithResult(isValid: true, "Validation 10.2.1.3. onef - validated against the oneOf schema.");
                        }
                    }
                    else if (oneOfCount == 0)
                    {
                        if (level >= ValidationLevel.Detailed)
                        {
                            result = result.WithResult(isValid: false, "Validation 10.2.1.3. oneOf - failed to validate against any of the oneOf schema.");
                        }
                        else if (level >= ValidationLevel.Basic)
                        {
                            result = result.WithResult(isValid: false, "Validation 10.2.1.3. oneOf - failed to validate against any of the oneOf schema.");
                        }
                        else
                        {
                            result = result.WithResult(isValid: false);
                        }
                    }
                    else
                    {
                        if (level >= ValidationLevel.Detailed)
                        {
                            result = result.WithResult(isValid: false, "Validation 10.2.1.3. oneOf - validated against more than one of the oneOf schema.");
                        }
                        else if (level >= ValidationLevel.Basic)
                        {
                            result = result.WithResult(isValid: false, "Validation 10.2.1.3. oneOf - failed to validate against more than one of the oneOf schema.");
                        }
                        else
                        {
                            result = result.WithResult(isValid: false);
                        }
                    }

                    if (level > ValidationLevel.Basic)
                    {
                        result = result.PopLocation(); // oneOf
                    }

                    return result;
                }
            }
        }
    }
}