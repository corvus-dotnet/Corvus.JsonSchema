//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:5.0.6
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using Corvus.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

#pragma warning disable

public partial class Spec : SpecBase {
    
    public virtual string TransformText() {
        this.GenerationEnvironment = null;
        
        #line 7 "Spec.tt"
        this.Write("\r\nFeature: ");
        
        #line default
        #line hidden
        
        #line 8 "Spec.tt"
        this.Write(this.ToStringHelper.ToStringWithCulture( FeatureName ));
        
        #line default
        #line hidden
        
        #line 8 "Spec.tt"
        this.Write("\r\n\r\n");
        
        #line default
        #line hidden
        
        #line 10 "Spec.tt"
 foreach (var scenario in Scenarios) { 
        
        #line default
        #line hidden
        
        #line 11 "Spec.tt"
        this.Write("Scenario Outline: ");
        
        #line default
        #line hidden
        
        #line 11 "Spec.tt"
        this.Write(this.ToStringHelper.ToStringWithCulture( scenario.Name ));
        
        #line default
        #line hidden
        
        #line 11 "Spec.tt"
        this.Write(" at level ");
        
        #line default
        #line hidden
        
        #line 11 "Spec.tt"
        this.Write(this.ToStringHelper.ToStringWithCulture( scenario.Level ));
        
        #line default
        #line hidden
        
        #line 11 "Spec.tt"
        this.Write("\r\n\tGiven the variables\r\n\t| name  | value          |\r\n");
        
        #line default
        #line hidden
        
        #line 14 "Spec.tt"
 foreach (var variable in scenario.Variables) { 
        
        #line default
        #line hidden
        
        #line 15 "Spec.tt"
        this.Write("\t| ");
        
        #line default
        #line hidden
        
        #line 15 "Spec.tt"
        this.Write(this.ToStringHelper.ToStringWithCulture( variable.Name ));
        
        #line default
        #line hidden
        
        #line 15 "Spec.tt"
        this.Write(" | ");
        
        #line default
        #line hidden
        
        #line 15 "Spec.tt"
        this.Write(this.ToStringHelper.ToStringWithCulture( variable.Value ));
        
        #line default
        #line hidden
        
        #line 15 "Spec.tt"
        this.Write(" |\r\n");
        
        #line default
        #line hidden
        
        #line 16 "Spec.tt"
 } 
        
        #line default
        #line hidden
        
        #line 17 "Spec.tt"
        this.Write("\tWhen I apply the variables to the template <template> \r\n\tThen the result should " +
                "be one of <result>\r\n\r\n\tExamples:\r\n\t| template | result               |\r\n");
        
        #line default
        #line hidden
        
        #line 22 "Spec.tt"
 foreach (var testcase in scenario.TestCases) { 
        
        #line default
        #line hidden
        
        #line 23 "Spec.tt"
        this.Write("\t| ");
        
        #line default
        #line hidden
        
        #line 23 "Spec.tt"
        this.Write(this.ToStringHelper.ToStringWithCulture( testcase.Template ));
        
        #line default
        #line hidden
        
        #line 23 "Spec.tt"
        this.Write(" | ");
        
        #line default
        #line hidden
        
        #line 23 "Spec.tt"
        this.Write(this.ToStringHelper.ToStringWithCulture( testcase.Result ));
        
        #line default
        #line hidden
        
        #line 23 "Spec.tt"
        this.Write(" |\r\n");
        
        #line default
        #line hidden
        
        #line 24 "Spec.tt"
 } 
        
        #line default
        #line hidden
        
        #line 25 "Spec.tt"
        this.Write("\r\n");
        
        #line default
        #line hidden
        
        #line 26 "Spec.tt"
 } 
        
        #line default
        #line hidden
        return this.GenerationEnvironment.ToString();
    }
    
    public virtual void Initialize() {
    }
}

public class SpecBase {
    
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
