---
name: corvus-perl-packages
description: >
  Work on the Perl distributions (src-perl): JSON::Schema::Corvus, the pure-Perl port of the evaluator (its JSON
  parser and value model, the loader, compiler, code generator and collecting evaluator ported from the pure Python
  package, and its ECMA-262 regular expression engine); JSON::Schema::Corvus::XS, the XS binding to the
  corvus-json-schema C library; and Alien::CorvusJsonSchema, which finds or downloads the C library. USE FOR: changing
  or debugging either validator, keeping them building on Perl 5.26 to the latest, threaded or not, on Linux, Alpine,
  macOS and Windows (Strawberry), the regex dataset gate, benchmarks against JSON::Schema::Modern, and releasing to
  CPAN.
---

# The Perl distributions

`src-perl` holds three CPAN distributions with one API between the two validators: the same methods, options, result
rows and error classes (`JSON::Schema::Corvus::Error::*` and `JSON::Schema::Corvus::XS::Error::*`), so either can be
used as the other. `docs/JsonSchemaForPerl.md` is the user's page.

## JSON::Schema::Corvus (pure Perl)

A port of the pure Python package (`src-py/corvus-json-schema`), module for module. Keep them in step: a fix to the
Python compiler, code generator or collecting evaluator usually belongs here too.

- `Json.pm`: the value model and the parser. A JSON value is undef, a JSON::PP::Boolean, an unblessed hash or array,
  a scalar created as a number (an integer literal as an IV, any other as an NV created with
  `unpack('d', pack('d', ...))`, or `POSIX::strtod` before 5.30, whose own reading is not correctly rounded), a
  Math::BigInt beyond 64 bits, or a scalar created as a string. Numbers are told from strings by how they were created
  (`builtin::created_as_number`, or B's flags before 5.36), never by their text. The parser records the order of an
  object's names (a fieldhash) in ordered mode: schemas and instances evaluated with a collector, so result rows come
  in document order. `from_data` copies Perl data into this form by JSON::PP's rules (B's flags: a number used as a
  string is a string).
- `Loader.pm`, `Compiler.pm`, `Node.pm`, `Uri.pm`, `Dialect.pm`, `Formats.pm`, `Runtime.pm`: ports of the Python
  modules of the same names.
- `CodeGen.pm`: the code generator. Each node is a named sub `_v<id>` (the underscore because `v1` alone is a version
  string) in a package of the validator's own (`JSON::Schema::Corvus::Generated::G<n>`), which the validator's
  DESTROY deletes. Constants are package variables `$C<n>`. A floating point constant is written with its exact bits
  (`unpack('d', pack('H*', ...))`). Objects are walked with `keys`, never `each`, whose iterator an early return
  would leave half way. `$validator->source` shows the code.
- `Collecting.pm` and `ResultsCollector.pm`: evaluation with a results collector, interpreting the node graph.
- `Pattern.pm` and `Regex/`: ECMA-262 patterns. `Regex/Parse.pm` (the Go module's parser: u flag, then Annex B),
  `Regex/Charset.pm` (sets, properties, case folding), `Regex/Unicode.pm` (Unicode 17 tables written by
  `tools/gen_unicode.py` from the Go module's `ucd/tables_gen.go`; never Perl's own tables), `Regex/Vm.pm` (the Go
  module's backtracking matcher, over an array of code points or UTF-16 code units, with its memo, quick checks,
  needs and a clock-checked limit), and `Regex/Linear.pm` (the Python package's proof of linear time). A pattern the
  proof proves runs on Perl's own engine, written out with every literal and class as code points; any other on the
  VM, under `pattern_match_limit`. Perl's engine cannot be interrupted, so it must never get an unproved pattern
  while a limit is in force.
- **Perl 5.40 bug.** A class whose members are one case folding set (`[\x{1f80}\x{1f88}]`, `[\x{fb05}\x{fb06}]`)
  compiles to an EXACTFU node with full case folding, which matches neither member. Classes of at most four code
  points are written as alternations for Perl's engine, and `Charset::contains` tests them by hash.

## JSON::Schema::Corvus::XS and Alien::CorvusJsonSchema

- `XS.xs`: blessed scalar references hold the C library's handles, freed in DESTROY, never cloned into ithreads
  (CLONE_SKIP: a reference to undef there). No Perl exception may unwind through the C library: custom formats and
  the resolver are called with G_EVAL, and an exception is kept (MY_CXT pending) and thrown once the library call
  returns. Perl data is encoded to JSON text in C by JSON::PP's rules before any library call. Before 5.32 `SvTRUE`
  evaluates its argument more than once: never `SvTRUE(POPs)`.
- `alienfile`: the pkg-config probe, and the release packages of the C library by target with their SHA-256 digests.
  The share install keeps the static library on Unix (linked into XS.so) and the DLL on Windows, which `XS.pm`
  loads before the XS module. The PkgConfig plugin's own share gather hook is replaced (`around_hook`).

## Testing

- `perl src-perl/ci.pl test|dist-install <dir>` builds and tests a distribution as CI does (`perl.yml`); `smoke`
  uses the installed modules. The suite tests need `JSON_SCHEMA_TEST_SUITE` or the submodule two levels up.
- Locally, `~/src/corvus-session-tools/perl-dev.sh [-v 5.26|5.40t] -- '<command>'` runs a Perl container (official
  `perl:<version>` images, `image=5.40-threaded` for threads) with the worktree at `/w` and a persistent cpanm
  library per version.
- **The regex dataset gate.** `~/src/corvus-session-tools/perl-regexdata/run-all.sh` (LIMIT=, SUFFIX= in the
  environment) runs the pure package's pattern engine over the gating files of
  `~/src/ecma262-regex-test-data`. It must give no wrong answer; a text that reaches the limit at 1 second must be
  right at 30.
- **Benchmarks.** `~/src/corvus-session-tools/perl-bench/corpora.pl` (pure against XS, every corpus of
  jsonschema-benchmark, with verdicts compared) and `vs-modern.pl` (against JSON::Schema::Modern).

## Releasing

`perl-publish.yml` (see `docs/ReleaseProcess.md`, "The Perl distributions"): bump a distribution's `$VERSION`, add the
version to its `Changes`, and merge. PAUSE credentials are the `perl` environment's secrets.
