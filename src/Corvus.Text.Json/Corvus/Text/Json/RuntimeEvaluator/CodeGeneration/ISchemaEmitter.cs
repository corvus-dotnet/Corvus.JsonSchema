// <copyright file="ISchemaEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET && !STJ
using Corvus.Text.Json.RuntimeEvaluator.Compilation;

namespace Corvus.Text.Json.RuntimeEvaluator.CodeGeneration;

/// <summary>
/// What the lowering asks a backend to write. The lowering decides the code's shape (which nodes get a method, what
/// each does inline); a backend only writes it: IL into a collectible assembly now, C# source later. The operations
/// are structured (each <c>Begin</c> has its <c>End</c>), so one backend implements them with labels and another
/// with blocks.
/// </summary>
/// <remarks>
/// Inside an object (<see cref="BeginObject"/> to <see cref="EndObject"/>) the code written runs once per property,
/// and inside an array (<see cref="BeginArray"/> to <see cref="EndArray"/>) once per item; the <c>FailUnless</c>
/// operations test the current property's value or the current item, and a failure returns false from the method.
/// </remarks>
internal interface ISchemaEmitter
{
    /// <summary>Starts the method for a node.</summary>
    /// <param name="nodeId">The node.</param>
    void BeginMethod(int nodeId);

    /// <summary>Returns the interpreter's result for a node at the method's value: for nodes the lowering does not specialise.</summary>
    /// <param name="nodeId">The node to evaluate.</param>
    void ReturnInterpreted(int nodeId);

    /// <summary>Finishes the current method.</summary>
    void EndMethod();

    /// <summary>
    /// Starts an object's pass over its properties. A value that is not an object returns whether its token type is
    /// one of <paramref name="otherTokens"/>; an object returns false unless <paramref name="acceptsObject"/>, or
    /// when its property count is outside the bounds.
    /// </summary>
    /// <param name="otherTokens">The token types accepted for a value that is not an object, a bit per type.</param>
    /// <param name="integerOnly">Whether an accepted number must be an integer.</param>
    /// <param name="lexical">Whether the integer test is draft 4's lexical one.</param>
    /// <param name="acceptsObject">Whether an object is accepted at all.</param>
    /// <param name="minProperties">The least property count, or -1.</param>
    /// <param name="maxProperties">The greatest property count, or -1.</param>
    /// <param name="otherwiseInterpreted">When not negative, a value that is not an object returns the interpreter's general evaluation of this node in place of the token test.</param>
    /// <param name="properties">Whether the object's properties are passed over at all (when not, nothing is written before <see cref="EndProperties"/>).</param>
    void BeginObject(ushort otherTokens, bool integerOnly, bool lexical, bool acceptsObject, int minProperties, int maxProperties, int otherwiseInterpreted = -1, bool properties = true);

    /// <summary>Finishes the pass over the properties; the method then succeeds when every bit of <paramref name="requiredMask"/> was marked, and fails otherwise.</summary>
    /// <param name="requiredMask">The seen bits that must be set.</param>
    void EndObject(ulong requiredMask);

    /// <summary>
    /// Finishes the pass over the properties and goes on: what is written next runs once, for an object, after its
    /// properties (the tests of its seen bits and of its own value), and ends with <see cref="Succeed"/>.
    /// </summary>
    void EndProperties();

    /// <summary>Fails unless every bit of a mask was marked.</summary>
    /// <param name="mask">The seen bits that must be set.</param>
    void FailUnlessSeen(ulong mask);

    /// <summary>Starts code that runs when a seen bit was marked.</summary>
    /// <param name="bit">The bit.</param>
    void BeginIfSeen(int bit);

    /// <summary>
    /// Succeeds: returns true from the method, or, between <see cref="BeginOwnKeywords"/> and
    /// <see cref="EndOwnKeywords"/>, goes on to what follows them.
    /// </summary>
    void Succeed();

    /// <summary>
    /// Starts the node's own keywords (an object, an array or a leaf, written with the operations for a whole method)
    /// when more follows them: where they would return true they go on to what follows
    /// <see cref="EndOwnKeywords"/> instead.
    /// </summary>
    void BeginOwnKeywords();

    /// <summary>Finishes the node's own keywords.</summary>
    void EndOwnKeywords();

    /// <summary>Fails unless the method's value satisfies a node's own local keywords, by the interpreter's leaf evaluation.</summary>
    /// <param name="nodeId">The node.</param>
    void FailUnlessOwnLeaf(int nodeId);

    /// <summary>Fails unless the method's value has one of a set of token types.</summary>
    /// <param name="tokens">The token types accepted, a bit per type.</param>
    /// <param name="integerOnly">Whether an accepted number must be an integer.</param>
    /// <param name="lexical">Whether the integer test is draft 4's lexical one.</param>
    void FailUnlessSelfToken(ushort tokens, bool integerOnly, bool lexical);

    /// <summary>Fails unless the method's value is valid against a child node.</summary>
    /// <param name="child">The child node.</param>
    /// <param name="generated">Whether the child has a generated method (the interpreter evaluates it otherwise).</param>
    void FailUnlessSelf(int child, bool generated);

    /// <summary>Fails if the method's value is valid against a child node.</summary>
    /// <param name="child">The child node.</param>
    /// <param name="generated">Whether the child has a generated method.</param>
    void FailIfSelf(int child, bool generated);

    /// <summary>Starts code that runs when the method's value is valid against a child node; <see cref="Else"/> starts the code for when it is not.</summary>
    /// <param name="child">The child node.</param>
    /// <param name="generated">Whether the child has a generated method.</param>
    void BeginIfSelf(int child, bool generated);

    /// <summary>Starts the other branch of the innermost <c>BeginIf</c>.</summary>
    void Else();

    /// <summary>Finishes the innermost <c>BeginIf</c>.</summary>
    void EndIf();

    /// <summary>Starts alternatives: the code up to <see cref="EndAlternatives"/> is left as soon as one <see cref="OrSelf"/> holds, and fails when none does.</summary>
    void BeginAlternatives();

    /// <summary>An alternative: the method's value valid against a child node.</summary>
    /// <param name="child">The child node.</param>
    /// <param name="generated">Whether the child has a generated method.</param>
    void OrSelf(int child, bool generated);

    /// <summary>Finishes the alternatives.</summary>
    void EndAlternatives();

    /// <summary>Starts a count of the children the method's value is valid against.</summary>
    void BeginCount();

    /// <summary>Counts a child node the method's value is valid against; fails at the second.</summary>
    /// <param name="child">The child node.</param>
    /// <param name="generated">Whether the child has a generated method.</param>
    void CountSelf(int child, bool generated);

    /// <summary>Fails unless exactly one child was counted.</summary>
    void FailUnlessCountedOne();

    /// <summary>Starts a choice by the method's value's token type: each <see cref="BeginTokenCase"/> is the code for some token types, and a token type no case names does nothing.</summary>
    void BeginTokenSwitch();

    /// <summary>Starts the code for a set of token types.</summary>
    /// <param name="tokens">The token types, a bit per type.</param>
    void BeginTokenCase(ushort tokens);

    /// <summary>Finishes the current token case.</summary>
    void EndTokenCase();

    /// <summary>Finishes the choice by token type.</summary>
    void EndTokenSwitch();

    /// <summary>Records whether the current property's name has matched a keyword that applies to it.</summary>
    /// <param name="matched">Whether it has.</param>
    void SetMatched(bool matched);

    /// <summary>Starts code that runs when the current property's name has not matched (see <see cref="SetMatched"/>).</summary>
    void BeginIfNotMatched();

    /// <summary>Starts code that runs when the current property's name matches a pattern; <see cref="Else"/> starts the code for when it does not.</summary>
    /// <param name="matcher">The pattern.</param>
    void BeginIfNameMatches(PatternMatcher matcher);

    /// <summary>
    /// Starts an array's pass over its items. A value that is not an array returns whether its token type is one of
    /// <paramref name="otherTokens"/>; an array returns false unless <paramref name="acceptsArray"/>, or when its
    /// length is outside the bounds.
    /// </summary>
    /// <param name="otherTokens">The token types accepted for a value that is not an array, a bit per type.</param>
    /// <param name="integerOnly">Whether an accepted number must be an integer.</param>
    /// <param name="lexical">Whether the integer test is draft 4's lexical one.</param>
    /// <param name="acceptsArray">Whether an array is accepted at all.</param>
    /// <param name="minItems">The least length, or -1.</param>
    /// <param name="maxItems">The greatest length, or -1.</param>
    void BeginArray(ushort otherTokens, bool integerOnly, bool lexical, bool acceptsArray, int minItems, int maxItems);

    /// <summary>Finishes the pass over the items; the method then returns true.</summary>
    void EndArray();

    /// <summary>
    /// An array whose items need no test: the method returns what <see cref="BeginArray"/> decides before its pass
    /// (the value's type and the array's length), and true for an array within the bounds.
    /// </summary>
    /// <param name="otherTokens">The token types accepted for a value that is not an array, a bit per type.</param>
    /// <param name="integerOnly">Whether an accepted number must be an integer.</param>
    /// <param name="lexical">Whether the integer test is draft 4's lexical one.</param>
    /// <param name="acceptsArray">Whether an array is accepted at all.</param>
    /// <param name="minItems">The least length, or -1.</param>
    /// <param name="maxItems">The greatest length, or -1.</param>
    void ReturnArrayWithoutItems(ushort otherTokens, bool integerOnly, bool lexical, bool acceptsArray, int minItems, int maxItems);

    /// <summary>Starts the dispatch on the current property's name: one case per key (by its index), and the case -1 for every other name.</summary>
    /// <typeparam name="T">The map's value type.</typeparam>
    /// <param name="names">The names, whose indices number the cases.</param>
    /// <param name="thenCommon">Whether each case goes on to the code after <see cref="EndNameDispatch"/> (common to every name) in place of the next property.</param>
    void BeginNameDispatch<T>(Utf8NameMap<T> names, bool thenCommon = false)
        where T : class;

    /// <summary>Returns whether the method's value has one of a set of token types.</summary>
    /// <param name="tokens">The token types accepted, a bit per type.</param>
    /// <param name="integerOnly">Whether an accepted number must be an integer.</param>
    /// <param name="lexical">Whether the integer test is draft 4's lexical one.</param>
    void ReturnTokenTest(ushort tokens, bool integerOnly, bool lexical);

    /// <summary>Returns the result of the child the method's value's token type selects, or false for a token type with none.</summary>
    /// <param name="childByToken">By token type, the child node, or -1.</param>
    /// <param name="generatedByToken">By token type, whether that child has a generated method (the interpreter evaluates it otherwise).</param>
    void ReturnChildByToken(int[] childByToken, bool[] generatedByToken);

    /// <summary>Starts the code for one name (by its index in the dispatch's names), or with -1 for every other name.</summary>
    /// <param name="index">The name's index, or -1.</param>
    void BeginCase(int index);

    /// <summary>Finishes the current case.</summary>
    void EndCase();

    /// <summary>Finishes the dispatch.</summary>
    void EndNameDispatch();

    /// <summary>Marks a seen bit.</summary>
    /// <param name="bit">The bit.</param>
    void MarkSeen(int bit);

    /// <summary>Fails.</summary>
    void Fail();

    /// <summary>Fails unless the value's token type is one of <paramref name="tokens"/>.</summary>
    /// <param name="tokens">The token types accepted, a bit per type.</param>
    /// <param name="integerOnly">Whether an accepted number must be an integer.</param>
    /// <param name="lexical">Whether the integer test is draft 4's lexical one.</param>
    void FailUnlessToken(ushort tokens, bool integerOnly, bool lexical);

    /// <summary>Fails unless the value is a string in a set.</summary>
    /// <param name="allowed">The set.</param>
    void FailUnlessStringSet(Utf8NameMap<object> allowed);

    /// <summary>Fails unless the value is one string.</summary>
    /// <param name="expected">The string, as UTF-8.</param>
    void FailUnlessStringConst(byte[] expected);

    /// <summary>Fails unless the value satisfies a string-length leaf.</summary>
    /// <param name="entry">The leaf's resolution.</param>
    void FailUnlessLength(in StrictEntry entry);

    /// <summary>
    /// Fails unless the value is valid against a child node. A token type in <paramref name="decided"/> is decided in
    /// place by <paramref name="accepts"/>; other values call the child.
    /// </summary>
    /// <param name="decided">The token types the child's type alone decides.</param>
    /// <param name="accepts">Of those, the token types accepted.</param>
    /// <param name="child">The child node.</param>
    /// <param name="generated">Whether the child has a generated method (the interpreter evaluates it otherwise).</param>
    void FailUnlessChild(ushort decided, ushort accepts, int child, bool generated);
}
#endif