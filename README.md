# Corvus.JsonSchema
Support for Json Schema validation and entity generation

For an introduction to the concepts here, take a look at [this blog post](https://endjin.com/blog/2021/05/csharp-serialization-with-system-text-json-schema).

## Use of JSON-Schema-Test-Suite

This project uses test suites from https://github.com/json-schema-org/JSON-Schema-Test-Suite to
validate operation. The ./JSON-Schema-Test-Suite folder is a submodule pointing to that test suite
repo. When cloning this repository it is important to clone submodules, because test projects in
this repository depend on that submodule being present. If you've already cloned the project, and
haven't yet got the submodules, run this commands:

```
git submodule update --init --recursive
```

Note that `git pull` does nota utomatically update submodules, so if `git pull` reports that any
submodules have changed, you can use the preceding command again, used to update the existing
submodule reference.

When updating to newer versions of the test suite, we can update the submodule reference thus:

```
cd JSON-Schema-Test-Suite
git fetch
git merge origin/master
cd ..
git commit - "Updated to latest JSON Schema Test Suite"
```

(Or you can use `git submodule update --remote` instead of `cd`ing into the submodule folder and
updating from there.)

