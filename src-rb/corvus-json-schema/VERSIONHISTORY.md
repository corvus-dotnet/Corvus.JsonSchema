# Version History

The version history of the `corvus_json_schema` Ruby gem. It is versioned independently of the Corvus NuGet packages and of the [corvus-json-schema](../../src-rs/corvus-json-schema/VERSIONHISTORY.md) Rust crate it is built on.

## V0.2.0

V0.2.0 takes version 0.2.0 of the `corvus-json-schema` crate, which matches patterns with the [`corvus-regex-ecma262`](https://crates.io/crates/corvus-regex-ecma262) crate and reads them in the grammar of the dialect. It is a minor version because under 2019-09 and 2020-12 a pattern that uses what ECMAScript added after 2020 is no longer valid. Everything else that was there is unchanged, and there are two new options.

- **The grammar of the dialect.** JSON Schema 2019-09 and 2020-12 name the grammar of ECMAScript 2020, and patterns under them are now read with it: a modifier group such as `(?i:...)`, or a group name used by two groups, is not valid, and `CorvusJsonSchema.compile` raises `CompilationError` for a schema with one in a `pattern`. A property escape that the tables of 2020 do not have (`\p{Script=Yezidi}`) is not valid with the `u` flag, so the pattern is read without it, where `\p` is the letter `p`, as a pattern with `\&` is. The `regex` format reads only the grammar of the flag. Drafts 4 to 7 read today's grammar, as before.
- **`regex_edition:`** (`:dialect`, the default, `:current` or `:es2020`) chooses the edition for every dialect, and **`regex_unicode_version:`** (`:default`, the default, `:latest`, `13`, `17` or `18`) the version of Unicode. Another value raises `ArgumentError`.
- **The engine.** Every pattern that no dedicated matcher of the crate takes is matched by `corvus-regex-ecma262`, whose answers are V8's for the whole of the ECMA-262 regular expression test data. The limits are those of 0.1.4. Over the jsonschema-benchmark corpora the crate validates in 0.98 of the time of 0.1.7 once warm, and takes 1.06 of it for the first validation and 1.05 to compile, since the engine builds a pattern's automaton on its first match.
- **Notices.** `THIRD-PARTY-NOTICES.txt` names the tables of `corvus-regex-ecma262`, which the native gems now hold in place of the crate's own. The source gem no longer carries a copy of the crate's notice, which the crate no longer has.

## V0.1.4

V0.1.4 takes version 0.1.7 of the `corvus-json-schema` crate. That fixes wrong results for several forms of pattern and a validation that did not end, replaces the crate's regular expression engine, and gives every match of a pattern a limit of time. The functions and types that were there are unchanged, and there are additions for the limit. Versions 0.1.0 to 0.1.3 are affected.

### The regular expression engine

The crate no longer uses the `regress` crate for the patterns with a lookaround, a backreference or a modifier group. It matches them with an engine of its own, a port of the ECMA-262 engine of the Corvus Go module, and matches every pattern as it is written. The crate's [version history](../../src-rs/corvus-json-schema/VERSIONHISTORY.md) lists the patterns that were answered wrongly before (a repetition inside a repetition that lost its count or did not end, a backreference from inside its group, two groups with one name, case-insensitive matching without the `u` flag, a pattern that is valid only without the `u` flag and a character beyond the Basic Multilingual Plane, `\B` beside a character of several bytes, and others), and what was measured: V8's answer for all 4,268,313 texts of the ECMA-262 regular expression test data that can be asked.

### Limits on regular expressions

A schema and an instance may both come from outside, so a pattern is now bounded in what it costs. Nothing that was there has changed. There is one new option and there are two new errors.

- **A match has a limit of one second.** `pattern_match_limit`, a keyword of `CorvusJsonSchema.compile`, is the seconds one match of a `pattern` or `patternProperties` regular expression against one string may take. The default is `CorvusJsonSchema::DEFAULT_PATTERN_MATCH_LIMIT`, `1.0`. `nil` or `0` turns the limit off for a caller who trusts both the schema and the instances. The limit is on each match, not on the validation.
- **`CorvusJsonSchema::PatternMatchLimitError`** is raised by `valid?`, `valid_json?` and `evaluate` when a match reached the limit, or when its backtracking held 1,048,576 entries. The instance is then neither valid nor not valid. The message names the pattern and the limit. `compile` can raise it too, because a pattern of `patternProperties` is matched against the property names the schema declares.
- **`CorvusJsonSchema::PatternTooLargeError`** is raised by `compile` for a schema with a regular expression over a limit of size: 1,048,576 bytes of pattern, 1,000 levels of groups, or 1,048,576 counted for it as it is read (a pattern of plain characters can be 16,384 characters long). It is a `CompilationError`, so code that rescues those still does. A regular expression that is not valid raises `CompilationError` itself, as before.

### Bug fixes

- **A separated list with a `$` inside it.** The crate matches a pattern of the form `^item(separator item)*$`, such as `^([a-z]+)(\.[a-z]+)*$`, without a regular expression engine. It dropped a `$` at the end of the first item or of the repeated group, so `^x(,a$)*$` was matched as `^x(,a)*$`. A `$` holds only at the end of the string, so the group of `^x(,a$)*$` can match once at most. `CorvusJsonSchema.compile({ "pattern" => '^x(,a$)*$' }).valid?("x,a,a")` returned `true`, where the instance is not valid. `^x$(,a)*$` took `"x,a"`, and `^(a,$)*b$` took `"a,b"`. `valid?`, `valid_json?` and `evaluate` were all affected, and so was a pattern used for `patternProperties` or `propertyNames`. Such a pattern is now matched by a regular expression engine. An escaped `\$` is a literal character and is matched as before. No other form of pattern was affected. The extension's crate requirement names 0.1.7, so the copy of the crate vendored at build time cannot be an older one.

### Notices

- **The notice of the Unicode data is in the gem.** The Unicode tables of the `corvus-json-schema` crate are derived from the Unicode Character Database, which is under the Unicode License V3. That license asks that its notice goes with every copy of the data. `THIRD-PARTY-NOTICES.txt` has it, with the notice of the `regress` crate that the tables were first read from, and is now in the source gem and in every native gem. Versions 0.1.0 to 0.1.3 carried no such notice.

## V0.1.3

V0.1.3 fixes `gem install corvus_json_schema` on Alpine Linux and other musl systems, where RubyGems before 4.0 could install the glibc gem, which does not load there. There are no API changes, and the extension is the one in 0.1.2. Versions 0.1.0 to 0.1.2 are affected.

### Bug fixes

- **`gem install` on a musl system with RubyGems before 4.0 (the RubyGems of Ruby 3.3 and 3.4).** The precompiled glibc gems had the platforms `x86_64-linux` and `aarch64-linux`. On a musl Ruby, those versions of RubyGems rank such a gem equal to the musl gem (`x86_64-linux-musl`) and install whichever comes last in their own ordering. Since 0.1.1 was published that has been the glibc gem, and `require "corvus_json_schema"` then failed with a `LoadError` naming `ld-linux-x86-64.so.2`. The glibc gems now have the platforms `x86_64-linux-gnu` and `aarch64-linux-gnu`, which RubyGems does not offer to a musl Ruby at all, so the musl gem is the one installed. Bundler was not affected, and neither was any glibc system, macOS or Windows.

  The earlier versions stay as they are. On a musl system with RubyGems before 4.0, install 0.1.3 or later, or install an earlier version with Bundler.

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
