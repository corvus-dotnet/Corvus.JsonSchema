# Version History

The version history of the `corvus_json_schema` Ruby gem. It is versioned independently of the Corvus NuGet packages and of the [corvus-json-schema](../../src-rs/corvus-json-schema/VERSIONHISTORY.md) Rust crate it is built on.

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
