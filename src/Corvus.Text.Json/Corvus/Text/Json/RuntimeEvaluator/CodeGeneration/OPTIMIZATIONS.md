# Optimizations: sweep of the Java port's code generator

Every technique of the Java port's bytecode generator (`src-java/corvus-json-schema`, `CodeGen.java` and its
`OPTIMIZATIONS.md`), checked against the runtime code generator here. Status is **done**, **partial** (with what is
missing), **todo** (in the order they will be taken), or **n/a** with the reason.

The lowering in `SchemaLowering.cs` follows the C# interpreter's plans (`NodePlan`), which already carry most of the
analyses the Java generator performs for itself. The interpreter evaluates anything the lowering does not specialise.

## Done

- **One method per node, constants as data of the generated type.** `IlSchemaEmitter`: static methods of one type in
  a collectible assembly, object constants in static fields.
- **Interpreter and generated code call each other.** Generated code calls the interpreter for a node it does not
  specialise. The interpreter calls generated methods through the `Generated` plan on a copy of the node array.
- **Type tests.** One comparison or one mask test in place. The integer test runs only for integer without number.
  Type-only children are tested at the call site.
- **Objects.** One pass over the properties. `required` as a mask of seen bits. Count bounds from the header, only
  when present. Pattern properties, an additional schema and dependencies (the object plan's three forms).
- **Name dispatch by length, then a trie of words** (`wordTrie`). Names of one length are told apart word by word,
  so shared prefixes are compared once. Escaped names, names at the very end of the text and names over 128 bytes
  take the interpreter's lookup.
- **String enums and string consts by words.** A set of up to 32 strings is tested with the same trie on the value's
  text. A string const is compared as bytes.
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
  kind.
- **Number keywords** (`numberSection`). Integer bounds and `multipleOf` compared as longs for a plain integer
  literal. Any other number, a bound that is not an integer and a numeric format take the interpreter's evaluation.
- **Leaves as methods.** A leaf node's method tests only the keywords the node has, by the value's kind.
- **Merged methods.** The C# compiler already canonicalises equivalent subschemas (`CanonicalizeEquivalentNodes`), so
  equal nodes share one node and therefore one method. The Java port merges by structure, which also catches nodes
  written differently. Not ported.

- **Discriminators** (`inlineDiscriminator`). The discriminator's property is found by comparing each name's words,
  and its string value selects the branches by words, as a bit per branch. Other values (numbers, booleans, null,
  escaped strings) take the interpreter's lookup. The generated code then tests each selected branch.
- **String lengths.** Decided from the byte length in place (a rune is one to four bytes). Only a value in the band
  the byte length does not decide, or an escaped one, is counted.

## Partial

- **`unevaluatedProperties` from static coverage.** Compiled in the fused pass when the plan has no alternative
  groups. With alternative groups, and outside the fused plan, it is the interpreter's.
- **String keywords** (`stringSection`). Lengths are tested in place (see Done). A node's other string keywords
  (pattern, format, content) are one call of the interpreter's string evaluation.
- **Small objects probed by name** (`objectProbe`). Compiled as a dispatch loop over the properties. Java probes each
  declared name, required first.
- **Size guard.** An object of more than 256 names is left to the interpreter, and more than 512 names would take the
  hashed lookup. Java measures each method's size, regenerates oversized ones in compact form, and interprets them
  only if they are still too large.

## Todo

1. **`contains` and `unevaluatedItems`** (`arraySection`). `contains` stopped at `minContains`, and
   `unevaluatedItems` from static coverage. Both are the interpreter's general path today.
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

- **Instance representation.** The Java tape classifies numbers at parse time, so its integer and bound tests are bit
  tests. The C# metadata rows do not, and generated code reads the number's text as the interpreter does. This is a
  parser change, not a code generation technique.
- **An ahead-of-time cache.** The JVM's counterpart to ReadyToRun and native AOT. Generated code needs the JIT.
- **`java.util.regex` translation and reused matchers.** The C# evaluator's `PatternMatcher` and compiled regular
  expressions serve both engines.
