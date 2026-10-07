# Optimizations: sweep of the Java port's code generator

Every technique of the Java port's bytecode generator (`src-java/corvus-json-schema`, `CodeGen.java` and its
`OPTIMIZATIONS.md`), checked against the runtime code generator here. Status is **done**, **partial** (with what is
missing), **todo** (in the order they will be taken), or **n/a** with the reason.

The lowering in `SchemaLowering.cs` follows the C# interpreter's plans (`NodePlan`), which already carry most of the
analyses the Java generator performs for itself. The interpreter evaluates anything the lowering does not specialise.

## Done

- **One method per node, constants as data of the generated type.** `IlSchemaEmitter`: static methods of one type in
  a collectible assembly, object constants in static fields.
- **Small methods inlined.** The JVM inlines a small method into its callers by itself. The .NET JIT is asked to, for
  a method of up to 300 bytes of IL counting what is inlined into it (a map of strings, an array of strings, a small
  closed object, a choice between a string and an array), where each caller stays under 1,500 bytes with its callees
  inlined. A method that reaches itself is never inlined. A caller already larger than that may still grow by
  1,500 bytes of inlined callees, so a large object's small children are inlined too.
- **An entry method for each schema.** The evaluation's state is a local of it, and the entry node's method is inlined
  into it when nothing else calls it: one frame from the public entry to the validation. On documents of a few
  values that is 8 to 19% (an array of strings, a map of strings, yamllint).
- **One row on after a scalar.** In an array of scalars, a map of scalars and an object with declared properties, the
  loop reads only the value's token, and a value tested to be of a scalar type (or a member of a string enum) is
  followed by the next row: where the next value starts is worked out from the row only after a value that was not.
  In an object, the advance is written out at the end of each property's code, with a jump straight to the loop's
  head. As an exit block shared by the properties it was laid out a jump away from the head whenever the loop had
  another exit too, which an open object's has for a name it does not declare: every property then ended with two
  taken jumps, and an open object's loop measured 1.02 to 1.11 against the code without the short advance. Written
  out, it measures 0.92 (an open object) and 0.93 (required properties), level with a closed object.
- **A direct path from the public entry.** `Evaluate` reads the schema's compiled entry from one field and checks that
  the program still has the node array the code was compiled from, then calls it, ahead of the flag-mode entry data
  and the tiering's checks. 2 to 3% on documents of a few values.
- **Leaves tested where the value is.** A leaf child of an array or an object (bounds, a length, a pattern, a small
  enum) is emitted in the loop, which has the value's token already, not called as a method. An enum of more than
  eight strings stays a method.
- **Integer bounds and consts with no conversion.** An integer literal of up to seven digits is compared by a key
  made from its text: the digit count above the digits' bytes in written order, which orders as the integers do. The
  metadata row says the text is an integer literal, so the digits are not tested. Longer literals are converted.
- **Interpreter and generated code call each other.** Generated code calls the interpreter for a node it does not
  specialise. The interpreter calls generated methods through the `Generated` plan on a copy of the node array.
- **Type tests.** One comparison or one mask test in place. The integer test runs only for integer without number.
  Type-only children are tested at the call site.
- **Objects.** One pass over the properties. `required` as a mask of seen bits. Count bounds from the header, only
  when present. Pattern properties, an additional schema and dependencies (the object plan's three forms).
- **Name dispatch by length, then a trie of words** (`wordTrie`). The length is decided by a tree of comparisons, not
  an IL `switch`: the JIT compiles a switch to a jump table, which is one indirect jump for every property, and the
  tree's conditional branches are predicted far better (generated code 6% faster over the corpora, up to 39%). The
  JVM compiles a small switch to comparisons by itself. Names of one length are told apart word by word,
  so shared prefixes are compared once. Escaped names, names at the very end of the text and names over 128 bytes
  take the interpreter's lookup.
- **String enums and string consts by words.** A set of up to 32 strings is tested with the same trie on the value's
  text, while the method is under 16,000 bytes of IL (beyond that the set is a hashed lookup: clang-format's root
  object reaches 31,000 bytes with every set in place and runs 40% slower). A string const is compared as bytes.
- **Flat composition.** An `allOf`/`$ref` chain of object schemas in one merged pass (the flat fused plan).
- **Fused object plans.** Conditions decided from what was seen, alternative groups, required-only alternatives and
  forbidden sets. The Java port lists this as its own todo.
- **Arrays.** Simple arrays (leaf or type-only items in the array's own loop), arrays of items with the token types
  the items schema's type decides tested in place, prefix items by position, and `uniqueItems` (the interpreter's
  test, pairwise for a short array). The Java port lists simple arrays as its own todo.
- **Composition.** `$ref`, `allOf`, `anyOf` (stops at the first match), `oneOf` (stops at the second), `not` and
  `if`/`then`/`else`. `anyOf`/`oneOf` are one mask test when the branches are types, and otherwise only the branches
  that can accept the value's token type.
- **Type dispatch.** An `anyOf`/`oneOf` whose branches assert disjoint types evaluates only the branch for the value's
  kind. The kind is decided by comparisons, not an IL switch (neutral on the corpora, a third faster where
  the values' types vary from one to the next).
- **Integers read in one pass.** A plain integer literal's value is read by one loop over its digits, which also
  shows it is an integer, in place of a search for a fraction or exponent followed by a parse. An integer type with
  integer bounds makes no separate integer test.
- **`uniqueItems` over strings.** Up to eight unescaped strings are located once and compared by length before bytes
  (in the interpreter too).
- **`uniqueItems` over objects and arrays.** Two objects written with their properties in the same order are compared
  property by property, where a name with different values settles the pair at once; two arrays item by item; and
  values of different row counts are unequal without a comparison. For a longer array, an object's hash takes each
  unescaped name and each string or boolean value where it lies. Hashing every object before comparing pairs, and a
  pairwise limit of 16, both measured slower.
- **Integer const.** A plain integer literal is compared as a long. Any other number takes the general comparison.
- **Number keywords** (`numberSection`). Integer bounds and `multipleOf` compared as longs for a plain integer
  literal. Any other number, a bound that is not an integer and a numeric format take the interpreter's evaluation.
- **Leaves as methods.** A leaf node's method tests only the keywords the node has, by the value's kind.
- **Merged methods.** The C# compiler already canonicalises equivalent subschemas (`CanonicalizeEquivalentNodes`), so
  equal nodes share one node and therefore one method. The Java port merges by structure, which also catches nodes
  written differently. Not ported.

- **Discriminators** (`inlineDiscriminator`). The discriminator's property is found by comparing each name's words,
  and its string value selects the branches by words, as a bit per branch. Other values (numbers, booleans, null,
  escaped strings) take the interpreter's lookup. The generated code then tests each selected branch.
- **String lengths.** Exact from the byte length for text the parser marked all ASCII (the metadata row's ASCII
  bit). Otherwise decided from the byte length where it can be (a rune is one to four bytes), and counted only in the
  band it does not decide, or when escaped.

- **`contains`** (`arraySection`). Counted in the array's pass over its items, and no longer tested once
  `minContains` is met when nothing bounds the count above.
- **`propertyNames`** (`objectSection`). Each name tested in the object's pass. A names schema that only a type and
  string keywords decide (a pattern, a length, a format) tests the name's text where it lies, in the interpreter too.
  Any other names schema is evaluated over a document made of the name, as before.
- **Keywords that cannot apply under the node's type.** Object or array keywords on a type that admits neither leave a
  leaf, and string or number keywords on an array type leave the array of items. The interpreter has no plan for these
  nodes. The lowering takes them by the plan of the keywords that can apply.

After these, a census of the 37 Sourcemeta corpora (`CORVUS_RT_CODEGEN_STATS=census` on the harness's `codegen` mode)
finds no node left to the interpreter.

## Partial

- **`unevaluatedProperties` from static coverage.** Compiled in the fused pass when the plan has no alternative
  groups. With alternative groups, and outside the fused plan, it is the interpreter's.
- **String keywords** (`stringSection`). Lengths are tested in place (see Done), and a pattern is matched by a direct
  call of its matcher. A format or content keyword is one call of the interpreter's string evaluation.
- **Small objects probed by name** (`objectProbe`). Compiled as a dispatch loop over the properties. Java probes each
  declared name, required first.
- **Size guard.** An object of more than 256 names is left to the interpreter, and more than 512 names would take the
  hashed lookup. Java measures each method's size, regenerates oversized ones in compact form, and interprets them
  only if they are still too large.

## Todo

1. **`unevaluatedItems`** (`arraySection`), from static coverage. It is the interpreter's general path today. No
   Sourcemeta corpus reaches it.
2. **Dynamic references and in-place cycles** (`dynamicRef`, guarded calls). A dynamic reference resolved from the
   scope and dispatched to its method. In-place calls into nodes on a cycle run under the depth guard. Today a schema
   with a live dynamic scope is not compiled at all, and a node with an in-place child on a cycle is interpreted.
3. **The remaining `unevaluatedProperties` cases.**
4. **Size guard with compact forms.**
5. **String keywords in place.** Pattern shapes and formats called directly with their constants.

## Not applicable

- **A search on a distinguishing window of bits** (`distinguishingWindow`), for more than four distinct words at one
  position. Ported and measured: generated code was 10% slower on jshintrc and no faster anywhere. The JVM compiles it
  as a lookup switch. In IL it is a computed key, a range check and an indirect jump, which loses to a short chain of
  well-predicted comparisons. The words at a position are compared in turn.

- **The rows' and the text's start kept in locals of each method** (the hand-written validators of the ceiling
  experiment took them as arguments). Measured: generated code 2 to 15% slower on every corpus tried. A managed
  pointer held in a local across calls is one more value the JIT has to keep alive and spill. Reading both from the
  state where they are used is faster.
- **A tree of comparisons for the dispatch by item position.** Measured neutral (0.98 to 1.04), so it remains an IL
  switch.

- **Instance representation.** The Java tape classifies numbers at parse time, so its integer and bound tests are bit
  tests. The C# metadata rows do not, and generated code reads the number's text as the interpreter does. This is a
  parser change, not a code generation technique.
- **An ahead-of-time cache.** The JVM's counterpart to ReadyToRun and native AOT. Generated code needs the JIT.
- **`java.util.regex` translation and reused matchers.** The C# evaluator's `PatternMatcher` and compiled regular
  expressions serve both engines.

## Tried on the interpreter and not kept

- **The child dispatch as a chain of comparisons.** Replacing an IL `switch` on a property name's length with
  comparisons made generated code 6% faster, so the same was tried on the interpreter's own dispatch on a child's plan
  (`Evaluator.EvalChildFast`, a C# `switch` over the plans, which is a jump table): the plans compared in turn, the
  commonest first. By the 15-process measure, run through the interpreter, the chain against the switch read 0.978
  to 1.026 over nine small cases, one of them nested objects and an array of objects, with nothing beyond about one
  standard error. The sequence of plans along a schema is the same for every document, so the jump is predicted
  well, where the lengths of a document's property names were not. The switch stays.

## Open questions

- **The entry.** An empty object costs 3.2 ns through the public entry against the Java port's 2.6. The public path
  was a quarter of its instructions and removing most of that gained 2 to 3%. Keeping the document's rows and text
  together in one block (`JsonDocument._raw`), read with no test of the document's type, gained 13% on an empty
  object and 4 to 5% on other small documents. What is left has no single large part.
- **Integers.** About 2.0 ns for a bounded integer against the Java port's 1.2: the Java tape holds the value from
  the parse, and here the digits are read from the text on every validation.

## Measuring

Processes of one build fall into two modes about 8% apart on small documents, case by case (where the JIT placed each
schema's code), and an occasional process runs at about twice the time. A comparison of two builds on small cases is
the mean over 15 alternating processes of those within 1.3x of the fastest (`floor` mode of the harness, least of 400
batches a process), which reads a build against itself to within 2%. Five alternating rounds of the median, as the
A/B over corpora uses, cannot resolve less than 10% there.
