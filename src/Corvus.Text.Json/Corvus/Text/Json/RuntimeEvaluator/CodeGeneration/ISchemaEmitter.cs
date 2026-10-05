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

    /// <summary>Starts code that runs when a discriminator selects branches for the method's value; <see cref="Else"/> starts the code for when it selects nothing in particular.</summary>
    /// <param name="discriminator">The discriminator.</param>
    void BeginIfDiscriminated(Discriminator discriminator);

    /// <summary>Starts code that runs when the discriminator (see <see cref="BeginIfDiscriminated"/>) selected a branch.</summary>
    /// <param name="branch">The branch.</param>
    void BeginIfBranchSelected(int branch);

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

    /// <summary>Returns the interpreter's general evaluation of a node at the method's value when a seen bit is already marked (a property name repeated in the document).</summary>
    /// <param name="bit">The bit.</param>
    /// <param name="nodeId">The node.</param>
    void ReturnInterpretedIfSeen(int bit, int nodeId);

    /// <summary>Makes a place to keep the row index of a property's value for use after the pass.</summary>
    /// <returns>The place.</returns>
    int DeclareValueSlot();

    /// <summary>Keeps the current property's value in a place.</summary>
    /// <param name="slot">The place.</param>
    void StoreValue(int slot);

    /// <summary>Makes a kept value the current one: the <c>FailUnless</c> operations then test it.</summary>
    /// <param name="slot">The place.</param>
    void UseStoredValue(int slot);

    /// <summary>Runs a fused entry's value tests on the current value: the conditions it fails are marked failed.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="conditions">The number of conditions.</param>
    void FusedValueTests(FusedEntry entry, int conditions);

    /// <summary>Marks a condition failed.</summary>
    /// <param name="condition">The condition.</param>
    void MarkConditionFailed(int condition);

    /// <summary>Decides whether a condition holds: it has not been marked failed, and every bit of a mask was marked seen.</summary>
    /// <param name="condition">The condition.</param>
    /// <param name="requiredMask">The seen bits it needs.</param>
    void DecideCondition(int condition, ulong requiredMask);

    /// <summary>Decides whether a condition is reached: it has no gate, or its gate is reached and holds (or does not, by the polarity). Conditions are decided before gates, and a gate before the conditions under it.</summary>
    /// <param name="condition">The condition.</param>
    /// <param name="gate">The condition above it, or -1.</param>
    /// <param name="gatePolarity">Whether the gate must hold.</param>
    void DecideGate(int condition, int gate, bool gatePolarity);

    /// <summary>Starts code that runs when any of some conditions is reached and holds (or does not hold) as given.</summary>
    /// <param name="any">The conditions, each with whether it must hold.</param>
    void BeginIfActive(ReadOnlySpan<(int Condition, bool Polarity)> any);

    /// <summary>Starts code whose failures run a handler in place of failing the method: <see cref="OnFail"/> starts the handler.</summary>
    void BeginTry();

    /// <summary>Starts the handler of the innermost <see cref="BeginTry"/>.</summary>
    void OnFail();

    /// <summary>Finishes the innermost <see cref="BeginTry"/>.</summary>
    void EndTry();

    /// <summary>Marks a branch of an alternative group failed.</summary>
    /// <param name="group">The group.</param>
    /// <param name="branch">The branch.</param>
    void MarkAlternativeFailed(int group, int branch);

    /// <summary>Fails unless a branch of an alternative group has survived, or exactly one has.</summary>
    /// <param name="group">The group.</param>
    /// <param name="branchCount">The number of its branches.</param>
    /// <param name="exactlyOne">Whether exactly one must survive.</param>
    void FailUnlessAlternativeSurvives(int group, int branchCount, bool exactlyOne);

    /// <summary>Fails unless the method's value (an object) has a property count within bounds.</summary>
    /// <param name="minProperties">The least count, or -1.</param>
    /// <param name="maxProperties">The greatest count, or -1.</param>
    void FailUnlessPropertyCount(int minProperties, int maxProperties);

    /// <summary>Fails if every bit of a mask was marked seen.</summary>
    /// <param name="mask">The seen bits.</param>
    void FailIfSeen(ulong mask);

    /// <summary>An alternative (see <see cref="BeginAlternatives"/>): every bit of a mask marked seen.</summary>
    /// <param name="mask">The seen bits.</param>
    void OrSeen(ulong mask);

    /// <summary>Counts (see <see cref="BeginCount"/>) a mask whose every bit was marked seen; fails at the second.</summary>
    /// <param name="mask">The seen bits.</param>
    void CountSeen(ulong mask);

    /// <summary>Starts another pass over the properties of the object the method has passed over once (after <see cref="EndProperties"/>); <see cref="EndProperties"/> finishes it.</summary>
    void BeginPropertiesAgain();

    /// <summary>Makes the method's own value the current one: the <c>FailUnless</c> operations then test it.</summary>
    void UseSelfAsValue();

    /// <summary>Starts code that runs when the current value has one of a set of token types.</summary>
    /// <param name="tokens">The token types, a bit per type.</param>
    void BeginIfValueToken(ushort tokens);

    /// <summary>Fails unless the current value equals a node's <c>const</c>, by the interpreter's comparison.</summary>
    /// <param name="nodeId">The node.</param>
    void FailUnlessConst(int nodeId);

    /// <summary>Fails unless the current value is in a node's <c>enum</c>, by the interpreter's comparison.</summary>
    /// <param name="nodeId">The node.</param>
    void FailUnlessEnum(int nodeId);

    /// <summary>Fails unless the current value (a number) satisfies a node's number keywords, by the interpreter's evaluation.</summary>
    /// <param name="nodeId">The node.</param>
    void FailUnlessNumberKeywords(int nodeId);

    /// <summary>Fails unless the current value (a string) matches a pattern.</summary>
    /// <param name="matcher">The pattern's matcher.</param>
    void FailUnlessPattern(PatternMatcher matcher);

    /// <summary>Fails unless the current value (a string) satisfies a node's string keywords, by the interpreter's evaluation.</summary>
    /// <param name="nodeId">The node.</param>
    void FailUnlessStringKeywords(int nodeId);

    /// <summary>
    /// Starts code that runs when the current value (a number) is a plain integer literal that fits a long, which the
    /// <c>FailIfLong</c> operations then compare; <see cref="Else"/> starts the code for any other number.
    /// </summary>
    void BeginIfValueLong();

    /// <summary>Fails if the long (see <see cref="BeginIfValueLong"/>) is below a bound, or not above it.</summary>
    /// <param name="bound">The bound.</param>
    /// <param name="exclusive">Whether the bound itself fails.</param>
    void FailIfLongBelow(long bound, bool exclusive);

    /// <summary>Fails if the long is above a bound, or not below it.</summary>
    /// <param name="bound">The bound.</param>
    /// <param name="exclusive">Whether the bound itself fails.</param>
    void FailIfLongAbove(long bound, bool exclusive);

    /// <summary>Fails unless the long is a multiple of a divisor.</summary>
    /// <param name="divisor">The divisor.</param>
    void FailUnlessLongMultipleOf(long divisor);

    /// <summary>Records whether the current property's name has matched a keyword that applies to it.</summary>
    /// <param name="matched">Whether it has.</param>
    void SetMatched(bool matched);

    /// <summary>Starts code that runs when the current property's name has not matched (see <see cref="SetMatched"/>).</summary>
    void BeginIfNotMatched();

    /// <summary>
    /// Starts the test of the current item against a <c>contains</c> schema: the tests up to <see cref="EndContains"/>
    /// decide whether the item is counted, and do not fail the array. With no <paramref name="maxContains"/> the test
    /// is skipped once <paramref name="minContains"/> items have been counted.
    /// </summary>
    /// <param name="minContains">The least number of matching items.</param>
    /// <param name="maxContains">The greatest number of matching items, or -1.</param>
    void BeginContains(int minContains, int maxContains);

    /// <summary>Ends the test started by <see cref="BeginContains"/>, counting the item when the tests passed.</summary>
    void EndContains();

    /// <summary>Ends an array's pass over its items, failing unless the count of items <c>contains</c> matched is within its bounds, and returns true.</summary>
    /// <param name="minContains">The least number of matching items.</param>
    /// <param name="maxContains">The greatest number of matching items, or -1.</param>
    void EndArrayContaining(int minContains, int maxContains);

    /// <summary>Fails unless the current property's name is valid against a <c>propertyNames</c> schema.</summary>
    /// <param name="names">The reference to the schema for the names.</param>
    void FailUnlessName(ChildRef names);

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
    /// <param name="uniqueItems">Whether an array also fails when two of its items are equal.</param>
    void BeginArray(ushort otherTokens, bool integerOnly, bool lexical, bool acceptsArray, int minItems, int maxItems, bool uniqueItems = false);

    /// <summary>
    /// Starts the dispatch on the current item's position in the array: one case per leading position (by its index),
    /// and the case -1 for every item after them. The cases are written as a name dispatch's are.
    /// </summary>
    /// <param name="positions">The number of leading positions.</param>
    void BeginPositionDispatch(int positions);

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
    /// <param name="uniqueItems">Whether an array also fails when two of its items are equal.</param>
    void ReturnArrayWithoutItems(ushort otherTokens, bool integerOnly, bool lexical, bool acceptsArray, int minItems, int maxItems, bool uniqueItems = false);

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

    /// <summary>
    /// Fails unless the current value (a string) has a length in runes within bounds. The byte length decides most
    /// values in place; the rest are counted.
    /// </summary>
    /// <param name="minLength">The least length, or -1.</param>
    /// <param name="maxLength">The greatest length, or -1.</param>
    void FailUnlessStringLength(int minLength, int maxLength);

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