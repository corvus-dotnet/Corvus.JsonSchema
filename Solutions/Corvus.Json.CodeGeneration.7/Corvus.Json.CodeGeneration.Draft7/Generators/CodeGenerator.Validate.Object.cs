//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:6.0.8
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Corvus.Json.CodeGeneration.Generators.Draft7 {
    using System.Collections.Generic;
    using Corvus.Json;
    using System;
    
    
    public partial class CodeGeneratorValidateObject : CodeGeneratorValidateObjectBase {
        
        
        #line 300 "./Templates/CodeGenerator.Validate.Object.tt"

    public bool ShouldGenerate
    {
        get
        {
            return HasDependentRequired || HasLocalProperties || HasRequired || HasMaxProperties || HasMinProperties|| HasDependentSchemas || HasPropertyNames || HasPatternProperties || ((AllowsAdditionalProperties && HasAdditionalProperties) || !AllowsAdditionalProperties);
        }
    }

        #line default
        #line hidden
        
        
        public virtual string TransformText() {
            this.GenerationEnvironment = null;
            
            #line 4 "./Templates/CodeGenerator.Validate.Object.tt"

    // This is only emitted if HasDependentRequired || HasLocalProperties || HasRequired || HasMaxProperties || HasMinProperties|| HasDependentSchemas || HasPropertyNames || HasPatternProperties || ((AllowsAdditionalProperties && (HasAdditionalProperties)) || !AllowsAdditionalProperties) is true
    if(!ShouldGenerate)
    {
        throw new InvalidOperationException("CodeGenerator.Validate.Object should not be emitted if HasDependentRequired || HasLocalProperties || HasRequired || HasMaxProperties || HasMinProperties|| HasDependentSchemas || HasPropertyNames || HasPatternProperties || ((AllowsAdditionalProperties && (HasAdditionalProperties)) || !AllowsAdditionalProperties) is false.");
    } 
            
            #line default
            #line hidden
            
            #line 10 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(@"//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

#nullable enable

using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Corvus.Json;
using Corvus.Json.Internal;

namespace ");
            
            #line default
            #line hidden
            
            #line 28 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(this.ToStringHelper.ToStringWithCulture( Namespace ));
            
            #line default
            #line hidden
            
            #line 28 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(";\r\n\r\n");
            
            #line default
            #line hidden
            
            #line 30 "./Templates/CodeGenerator.Validate.Object.tt"
 BeginNesting(); 
            
            #line default
            #line hidden
            
            #line 31 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write("/// <summary>\r\n/// A type generated from a JsonSchema specification.\r\n/// </summa" +
                    "ry>\r\npublic readonly partial struct ");
            
            #line default
            #line hidden
            
            #line 34 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(this.ToStringHelper.ToStringWithCulture( TypeDeclaration.DotnetTypeName ));
            
            #line default
            #line hidden
            
            #line 34 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(@"
{
    private ValidationContext ValidateObject(JsonValueKind valueKind, in ValidationContext validationContext, ValidationLevel level)
    {
        ValidationContext result = validationContext;

        if (valueKind != JsonValueKind.Object)
        {
            return result;
        }
");
            
            #line default
            #line hidden
            
            #line 44 "./Templates/CodeGenerator.Validate.Object.tt"
  if (HasMaxProperties || HasMinProperties || HasLocalProperties || HasRequired || HasDependentSchemas || HasPatternProperties || HasAdditionalProperties)
    { 
            
            #line default
            #line hidden
            
            #line 46 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write("\r\n        int propertyCount = 0;\r\n");
            
            #line default
            #line hidden
            
            #line 48 "./Templates/CodeGenerator.Validate.Object.tt"
  }
    if (HasRequired)
    {
        foreach(var property in RequiredProperties)
        { 
            
            #line default
            #line hidden
            
            #line 53 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write("        bool found");
            
            #line default
            #line hidden
            
            #line 53 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(this.ToStringHelper.ToStringWithCulture( property.DotnetPropertyName ));
            
            #line default
            #line hidden
            
            #line 53 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(" = false;\r\n");
            
            #line default
            #line hidden
            
            #line 54 "./Templates/CodeGenerator.Validate.Object.tt"
      }
    } 
            
            #line default
            #line hidden
            
            #line 56 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write("\r\n");
            
            #line default
            #line hidden
            
            #line 57 "./Templates/CodeGenerator.Validate.Object.tt"
  if (HasDependentRequired)
    { 
            
            #line default
            #line hidden
            
            #line 59 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(@"        foreach(var dependentRequired in __CorvusDependentRequired)
        {
            if (this.HasJsonElementBacking)
            {
                if (this.HasProperty(dependentRequired.Utf8Name.Span))
                {
                    foreach (ReadOnlyMemory<byte> dependency in dependentRequired.Utf8Dependency)
                    {
                        ReadOnlySpan<byte> dSpan = dependency.Span;
                        if (!this.HasProperty(dSpan)");
            
            #line default
            #line hidden
            
            #line 68 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(this.ToStringHelper.ToStringWithCulture( HasDefaults ? " && !this.HasDefault(dSpan))" : ")" ));
            
            #line default
            #line hidden
            
            #line 68 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(@"
                        {
                            if (level >= ValidationLevel.Detailed)
                            {
                                result = result.WithResult(isValid: false, $""6.5.4. dependentRequired - dependent property \""{Encoding.UTF8.GetString(dSpan)}\"" not found."");
                            }
                            else if (level >= ValidationLevel.Basic)
                            {
                                result = result.WithResult(isValid: false, ""6.5.4. dependentRequired - dependent property not found."");
                            }
                            else
                            {
                                return result.WithResult(isValid: false);
                            }
                        }
                    }
                }
            }
            else
            {
                if (this.HasProperty(dependentRequired.Name))
                {
                    foreach (string dependency in dependentRequired.Dependency)
                    {
                        if (!this.HasProperty(dependency)");
            
            #line default
            #line hidden
            
            #line 92 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(this.ToStringHelper.ToStringWithCulture( HasDefaults ? " && !this.HasDefault(dependency))" : ")" ));
            
            #line default
            #line hidden
            
            #line 92 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(@"
                        {
                            if (level >= ValidationLevel.Detailed)
                            {
                                result = result.WithResult(isValid: false, $""6.5.4. dependentRequired - dependent property \""{dependency}\"" not found."");
                            }
                            else if (level >= ValidationLevel.Basic)
                            {
                                result = result.WithResult(isValid: false, ""6.5.4. dependentRequired - dependent property not found."");
                            }
                            else
                            {
                                return result.WithResult(isValid: false);
                            }
                        }
                    }
                }
            }
        }
");
            
            #line default
            #line hidden
            
            #line 111 "./Templates/CodeGenerator.Validate.Object.tt"
  } 
            
            #line default
            #line hidden
            
            #line 112 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write("\r\n        foreach (JsonObjectProperty property in this.EnumerateObject())\r\n      " +
                    "  {\r\n");
            
            #line default
            #line hidden
            
            #line 115 "./Templates/CodeGenerator.Validate.Object.tt"
  if (HasLocalProperties || HasRequired)
    { 
            
            #line default
            #line hidden
            
            #line 117 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(@"
            if (__TryGetCorvusLocalPropertiesValidator(property, this.HasJsonElementBacking, out ObjectPropertyValidator? propertyValidator))
            {
                result = result.WithLocalProperty(propertyCount);
                if (level > ValidationLevel.Basic)
                {
                    result = result.PushDocumentProperty(""properties"", property.Name);
                }

                var propertyResult = propertyValidator(property, result.CreateChildContext(), level);
                result = result.MergeResults(propertyResult.IsValid, level, propertyResult);

                if (level > ValidationLevel.Basic)
                {
                    result = result.PopLocation(); // property name
                }

                if (level == ValidationLevel.Flag && !result.IsValid)
                {
                    return result;
                }
");
            
            #line default
            #line hidden
            
            #line 138 "./Templates/CodeGenerator.Validate.Object.tt"
      if (HasRequired)
        { 
            
            #line default
            #line hidden
            
            #line 140 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write("\r\n");
            
            #line default
            #line hidden
            
            #line 141 "./Templates/CodeGenerator.Validate.Object.tt"
          int requiredIndex = 0 ;
            foreach (var property in RequiredProperties)
            { 
            
            #line default
            #line hidden
            
            #line 144 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write("                ");
            
            #line default
            #line hidden
            
            #line 144 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(this.ToStringHelper.ToStringWithCulture( requiredIndex != 0 ? "else " : "" ));
            
            #line default
            #line hidden
            
            #line 144 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write("if ((this.HasJsonElementBacking && property.NameEquals(");
            
            #line default
            #line hidden
            
            #line 144 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(this.ToStringHelper.ToStringWithCulture( property.DotnetPropertyName ));
            
            #line default
            #line hidden
            
            #line 144 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write("Utf8JsonPropertyName.Span)) || (!this.HasJsonElementBacking && property.NameEqual" +
                    "s(");
            
            #line default
            #line hidden
            
            #line 144 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(this.ToStringHelper.ToStringWithCulture( property.DotnetPropertyName ));
            
            #line default
            #line hidden
            
            #line 144 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write("JsonPropertyName)))\r\n                {\r\n                    found");
            
            #line default
            #line hidden
            
            #line 146 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(this.ToStringHelper.ToStringWithCulture( property.DotnetPropertyName ));
            
            #line default
            #line hidden
            
            #line 146 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(" = true;\r\n                }\r\n");
            
            #line default
            #line hidden
            
            #line 148 "./Templates/CodeGenerator.Validate.Object.tt"
          requiredIndex++;
            }
        } 
            
            #line default
            #line hidden
            
            #line 151 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write("            }\r\n");
            
            #line default
            #line hidden
            
            #line 152 "./Templates/CodeGenerator.Validate.Object.tt"
  }
    if (HasDependentSchemas)
    { 
            
            #line default
            #line hidden
            
            #line 155 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write("            if (__TryGetCorvusDependentSchemaValidator(property, this.HasJsonElem" +
                    "entBacking, out PropertyValidator<");
            
            #line default
            #line hidden
            
            #line 155 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(this.ToStringHelper.ToStringWithCulture( TypeDeclaration.DotnetTypeName ));
            
            #line default
            #line hidden
            
            #line 155 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(@">? dependentSchemaValidator))
            {
                result = result.WithLocalProperty(propertyCount);
                result = dependentSchemaValidator(this, result, level);
                if (level == ValidationLevel.Flag && !result.IsValid)
                {
                    return result;
                }
            }
");
            
            #line default
            #line hidden
            
            #line 164 "./Templates/CodeGenerator.Validate.Object.tt"
  }
    if (HasPropertyNames || HasPatternProperties)
    { 
            
            #line default
            #line hidden
            
            #line 167 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write("            string propertyName = property.Name;\r\n");
            
            #line default
            #line hidden
            
            #line 168 "./Templates/CodeGenerator.Validate.Object.tt"
      if (HasPropertyNames)
        { 
            
            #line default
            #line hidden
            
            #line 170 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write("\r\n            result = new ");
            
            #line default
            #line hidden
            
            #line 171 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(this.ToStringHelper.ToStringWithCulture( PropertyNamesDotnetTypeName ));
            
            #line default
            #line hidden
            
            #line 171 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write("(propertyName).Validate(result, level);\r\n            if (level == ValidationLevel" +
                    ".Flag && !result.IsValid)\r\n            {\r\n                return result;\r\n      " +
                    "      }\r\n");
            
            #line default
            #line hidden
            
            #line 176 "./Templates/CodeGenerator.Validate.Object.tt"
      }
        if (HasPatternProperties)
        { 
            
            #line default
            #line hidden
            
            #line 179 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(@"
            foreach (System.Collections.Generic.KeyValuePair<Regex, PatternPropertyValidator> patternProperty in __CorvusPatternProperties)
            {
                if (patternProperty.Key.IsMatch(propertyName))
                {
                    result = result.WithLocalProperty(propertyCount);
                    result = patternProperty.Value(property, result, level);
                    if (level == ValidationLevel.Flag && !result.IsValid)
                    {
                        return result;
                    }
                }
            }
");
            
            #line default
            #line hidden
            
            #line 192 "./Templates/CodeGenerator.Validate.Object.tt"
      }
    }
    if (AllowsAdditionalProperties && HasAdditionalProperties)
    { 
            
            #line default
            #line hidden
            
            #line 196 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write("\r\n            if (!result.HasEvaluatedLocalProperty(propertyCount))\r\n            " +
                    "{\r\n                result = property.ValueAs<");
            
            #line default
            #line hidden
            
            #line 199 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(this.ToStringHelper.ToStringWithCulture( AdditionalPropertiesDotnetTypeName ));
            
            #line default
            #line hidden
            
            #line 199 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(@">().Validate(result, level);
                if (level == ValidationLevel.Flag && !result.IsValid)
                {
                    return result;
                }
                result = result.WithLocalProperty(propertyCount);
            }
");
            
            #line default
            #line hidden
            
            #line 206 "./Templates/CodeGenerator.Validate.Object.tt"
  }
    if (!AllowsAdditionalProperties)
    { 
            
            #line default
            #line hidden
            
            #line 209 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(@"
            if (!result.HasEvaluatedLocalProperty(propertyCount))
            {
                if (level >= ValidationLevel.Detailed)
                {
                    result = result.WithResult(isValid: false, $""9.3.2.3. additionalProperties - additional property \""{property.Name}\"" is not permitted."");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    result = result.WithResult(isValid: false, ""9.3.2.3. additionalProperties - additional properties are not permitted."");
                }
                else
                {
                    return result.WithResult(isValid: false);
                }
            }
");
            
            #line default
            #line hidden
            
            #line 225 "./Templates/CodeGenerator.Validate.Object.tt"
  }
    if (HasMaxProperties || HasMinProperties || HasLocalProperties || HasRequired || HasDependentSchemas || HasPatternProperties || HasAdditionalProperties)
    { 
            
            #line default
            #line hidden
            
            #line 228 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write("\r\n            propertyCount++;\r\n");
            
            #line default
            #line hidden
            
            #line 230 "./Templates/CodeGenerator.Validate.Object.tt"
  } 
            
            #line default
            #line hidden
            
            #line 231 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write("        }\r\n");
            
            #line default
            #line hidden
            
            #line 232 "./Templates/CodeGenerator.Validate.Object.tt"
  if (HasRequired)
    {
        foreach (var property in RequiredProperties)
        {
            if (!property.HasDefaultValue)
            { 
            
            #line default
            #line hidden
            
            #line 238 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write("\r\n        if (!found");
            
            #line default
            #line hidden
            
            #line 239 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(this.ToStringHelper.ToStringWithCulture( property.DotnetPropertyName ));
            
            #line default
            #line hidden
            
            #line 239 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(")\r\n        {\r\n            if (level >= ValidationLevel.Detailed)\r\n            {\r\n" +
                    "                result = result.WithResult(isValid: false, $\"6.5.3. required - r" +
                    "equired property \\\"");
            
            #line default
            #line hidden
            
            #line 243 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(this.ToStringHelper.ToStringWithCulture( Formatting.FormatLiteralOrNull(property.JsonPropertyName, true).Trim('"') ));
            
            #line default
            #line hidden
            
            #line 243 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(@"\"" not present."");
            }
            else if (level >= ValidationLevel.Basic)
            {
                result = result.WithResult(isValid: false, ""6.5.3. required - required property not present."");
            }
            else
            {
                return result.WithResult(isValid: false);
            }
        }
");
            
            #line default
            #line hidden
            
            #line 254 "./Templates/CodeGenerator.Validate.Object.tt"
          }
        }
    }
    if (HasMaxProperties)
    { 
            
            #line default
            #line hidden
            
            #line 259 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write("\r\n        if (propertyCount > ");
            
            #line default
            #line hidden
            
            #line 260 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(this.ToStringHelper.ToStringWithCulture( MaxProperties ));
            
            #line default
            #line hidden
            
            #line 260 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(")\r\n        {\r\n            if (level >= ValidationLevel.Detailed)\r\n            {\r\n" +
                    "                result = result.WithResult(isValid: false, $\"6.5.1. maxPropertie" +
                    "s - property count of {propertyCount} is greater than ");
            
            #line default
            #line hidden
            
            #line 264 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(this.ToStringHelper.ToStringWithCulture( MaxProperties ));
            
            #line default
            #line hidden
            
            #line 264 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(".\");\r\n            }\r\n            else if (level >= ValidationLevel.Basic)\r\n      " +
                    "      {\r\n                result = result.WithResult(isValid: false, \"6.5.1. maxP" +
                    "roperties - property count greater than ");
            
            #line default
            #line hidden
            
            #line 268 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(this.ToStringHelper.ToStringWithCulture( MaxProperties ));
            
            #line default
            #line hidden
            
            #line 268 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(".\");\r\n            }\r\n            else\r\n            {\r\n                return resu" +
                    "lt.WithResult(isValid: false);\r\n            }\r\n        }\r\n");
            
            #line default
            #line hidden
            
            #line 275 "./Templates/CodeGenerator.Validate.Object.tt"
  }
    if (HasMinProperties)
    { 
            
            #line default
            #line hidden
            
            #line 278 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write("\r\n        if (propertyCount < ");
            
            #line default
            #line hidden
            
            #line 279 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(this.ToStringHelper.ToStringWithCulture( MinProperties ));
            
            #line default
            #line hidden
            
            #line 279 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(")\r\n        {\r\n            if (level >= ValidationLevel.Detailed)\r\n            {\r\n" +
                    "                result = result.WithResult(isValid: false, $\"6.5.2. minPropertie" +
                    "s - property count of {propertyCount} is lezs than ");
            
            #line default
            #line hidden
            
            #line 283 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(this.ToStringHelper.ToStringWithCulture( MinProperties ));
            
            #line default
            #line hidden
            
            #line 283 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(".\");\r\n            }\r\n            else if (level >= ValidationLevel.Basic)\r\n      " +
                    "      {\r\n                result = result.WithResult(isValid: false, \"6.5.2. minP" +
                    "roperties - property count less than ");
            
            #line default
            #line hidden
            
            #line 287 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(this.ToStringHelper.ToStringWithCulture( MinProperties ));
            
            #line default
            #line hidden
            
            #line 287 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write(".\");\r\n            }\r\n            else\r\n            {\r\n                return resu" +
                    "lt.WithResult(isValid: false);\r\n            }\r\n        }\r\n");
            
            #line default
            #line hidden
            
            #line 294 "./Templates/CodeGenerator.Validate.Object.tt"
  } 
            
            #line default
            #line hidden
            
            #line 295 "./Templates/CodeGenerator.Validate.Object.tt"
            this.Write("\r\n        return result;\r\n    }\r\n}\r\n");
            
            #line default
            #line hidden
            
            #line 299 "./Templates/CodeGenerator.Validate.Object.tt"
 EndNesting(); 
            
            #line default
            #line hidden
            return this.GenerationEnvironment.ToString();
        }
        
        public virtual void Initialize() {
        }
    }
    
    public class CodeGeneratorValidateObjectBase {
        
        private global::System.Text.StringBuilder builder;
        
        private global::System.Collections.Generic.IDictionary<string, object> session;
        
        private global::System.CodeDom.Compiler.CompilerErrorCollection errors;
        
        private string currentIndent = string.Empty;
        
        private global::System.Collections.Generic.Stack<int> indents;
        
        private ToStringInstanceHelper _toStringHelper = new ToStringInstanceHelper();
        
        public virtual global::System.Collections.Generic.IDictionary<string, object> Session {
            get {
                return this.session;
            }
            set {
                this.session = value;
            }
        }
        
        public global::System.Text.StringBuilder GenerationEnvironment {
            get {
                if ((this.builder == null)) {
                    this.builder = new global::System.Text.StringBuilder();
                }
                return this.builder;
            }
            set {
                this.builder = value;
            }
        }
        
        protected global::System.CodeDom.Compiler.CompilerErrorCollection Errors {
            get {
                if ((this.errors == null)) {
                    this.errors = new global::System.CodeDom.Compiler.CompilerErrorCollection();
                }
                return this.errors;
            }
        }
        
        public string CurrentIndent {
            get {
                return this.currentIndent;
            }
        }
        
        private global::System.Collections.Generic.Stack<int> Indents {
            get {
                if ((this.indents == null)) {
                    this.indents = new global::System.Collections.Generic.Stack<int>();
                }
                return this.indents;
            }
        }
        
        public ToStringInstanceHelper ToStringHelper {
            get {
                return this._toStringHelper;
            }
        }
        
        public void Error(string message) {
            this.Errors.Add(new global::System.CodeDom.Compiler.CompilerError(null, -1, -1, null, message));
        }
        
        public void Warning(string message) {
            global::System.CodeDom.Compiler.CompilerError val = new global::System.CodeDom.Compiler.CompilerError(null, -1, -1, null, message);
            val.IsWarning = true;
            this.Errors.Add(val);
        }
        
        public string PopIndent() {
            if ((this.Indents.Count == 0)) {
                return string.Empty;
            }
            int lastPos = (this.currentIndent.Length - this.Indents.Pop());
            string last = this.currentIndent.Substring(lastPos);
            this.currentIndent = this.currentIndent.Substring(0, lastPos);
            return last;
        }
        
        public void PushIndent(string indent) {
            this.Indents.Push(indent.Length);
            this.currentIndent = (this.currentIndent + indent);
        }
        
        public void ClearIndent() {
            this.currentIndent = string.Empty;
            this.Indents.Clear();
        }
        
        public void Write(string textToAppend) {
            this.GenerationEnvironment.Append(textToAppend);
        }
        
        public void Write(string format, params object[] args) {
            this.GenerationEnvironment.AppendFormat(format, args);
        }
        
        public void WriteLine(string textToAppend) {
            this.GenerationEnvironment.Append(this.currentIndent);
            this.GenerationEnvironment.AppendLine(textToAppend);
        }
        
        public void WriteLine(string format, params object[] args) {
            this.GenerationEnvironment.Append(this.currentIndent);
            this.GenerationEnvironment.AppendFormat(format, args);
            this.GenerationEnvironment.AppendLine();
        }
        
        public class ToStringInstanceHelper {
            
            private global::System.IFormatProvider formatProvider = global::System.Globalization.CultureInfo.InvariantCulture;
            
            public global::System.IFormatProvider FormatProvider {
                get {
                    return this.formatProvider;
                }
                set {
                    if ((value != null)) {
                        this.formatProvider = value;
                    }
                }
            }
            
            public string ToStringWithCulture(object objectToConvert) {
                if ((objectToConvert == null)) {
                    throw new global::System.ArgumentNullException("objectToConvert");
                }
                global::System.Type type = objectToConvert.GetType();
                global::System.Type iConvertibleType = typeof(global::System.IConvertible);
                if (iConvertibleType.IsAssignableFrom(type)) {
                    return ((global::System.IConvertible)(objectToConvert)).ToString(this.formatProvider);
                }
                global::System.Reflection.MethodInfo methInfo = type.GetMethod("ToString", new global::System.Type[] {
                            iConvertibleType});
                if ((methInfo != null)) {
                    return ((string)(methInfo.Invoke(objectToConvert, new object[] {
                                this.formatProvider})));
                }
                return objectToConvert.ToString();
            }
        }
    }
}