# Version History

The version history of the `corvus_json_schema` Ruby gem. It is versioned independently of the Corvus NuGet packages and of the [corvus-json-schema](../../src-rs/corvus-json-schema/VERSIONHISTORY.md) Rust crate it is built on.

## V0.1.3

V0.1.3 fixes a wrong result for one form of pattern, which was taken to match strings it does not match, by taking version 0.1.7 of the corvus-json-schema crate. There are no API changes. Versions 0.1.0 to 0.1.2 are affected.

### Bug fixes

- **A separated list with a `$` inside it.** The crate matches a pattern of the form `^item(separator item)*$`, such as `^([a-z]+)(\.[a-z]+)*$`, without a regular expression engine. It dropped a `$` at the end of the first item or of the repeated group, so `^x(,a$)*$` was matched as `^x(,a)*$`. A `$` holds only at the end of the string, so the group of `^x(,a$)*$` can match once at most. `CorvusJsonSchema.compile({ "pattern" => '^x(,a$)*$' }).valid?("x,a,a")` returned `true`, where the instance is not valid. `^x$(,a)*$` took `"x,a"`, and `^(a,$)*b$` took `"a,b"`. `valid?`, `valid_json?` and `evaluate` were all affected, and so was a pattern used for `patternProperties` or `propertyNames`. Such a pattern is now matched by a regular expression engine. An escaped `\$` is a literal character and is matched as before. No other form of pattern was affected. The extension's crate requirement names 0.1.7, so the copy of the crate vendored at build time cannot be an older one.

## V0.1.2

V0.1.2 fixes a crash of the Ruby process for a schema whose `not` leads back to the schema it is in, by taking version 0.1.6 of the corvus-json-schema crate. There are no API changes. Versions 0.1.0 and 0.1.1 are affected.

### Bug fixes

- **A `not` that leads back to the schema it is in.** A schema can loop without consuming the instance. The gem abandons such an evaluation at `max_depth` (128 by default) and raises `CorvusJsonSchema::DepthError`. Evaluating `not` went around that guard. For a schema such as `{"not": {"$ref": "#"}}`, validating an instance that reached the `not` recursed in the native extension until the stack overflowed, which crashes the Ruby process. `valid?`, `valid_json?` and `evaluate` were all affected. A `not` is now under the guard, so they raise `CorvusJsonSchema::DepthError`. The fault is in the schema. No instance causes it for a schema without such a loop, so a program was exposed only if it compiled schemas it did not write. A schema that loops elsewhere under a `not` was not affected in this gem: it raised `CorvusJsonSchema::DepthError`, and still does.

## V0.1.1

V0.1.1 fixes wrong validation results for one form of pattern, by taking version 0.1.4 of the corvus-json-schema crate. There are no API changes. Version 0.1.0 is affected.

### Bug fixes

- **A pattern of the form `^(?=[^SET]+$)(?=(.*\w)).+$` with a character outside ASCII in its excluded set.** The crate matches this form without a regular expression engine and keeps the excluded set as one bit for each ASCII character. A member outside ASCII, such as `é` in `^(?=[^é]+$)(?=(.*\w)).+$`, was read one UTF-8 byte at a time, and set the bits of unrelated ASCII characters (`C` and `)` for `é`). A validator compiled from such a schema rejected valid strings (`"C1"`) and accepted invalid ones (`"é1"`), with no error. Such a pattern is now matched by the regular expression engine. A pattern of this form whose excluded set is all ASCII was never affected.

## V0.1.0

The first release: a native extension over the corvus-json-schema Rust crate (0.1.3), for drafts 4, 6, 7, 2019-09 and 2020-12. It reads Ruby values in place, validates JSON text without creating Ruby objects for it, and collects results (Basic, Detailed and Verbose) and annotations. It passes the whole JSON-Schema-Test-Suite, read in place, as JSON text and through a collector.
