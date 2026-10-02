# frozen_string_literal: true

require_relative "lib/corvus_json_schema/version"

Gem::Specification.new do |spec|
  spec.name = "corvus_json_schema"
  spec.version = CorvusJsonSchema::VERSION
  spec.authors = ["endjin"]
  spec.summary = "A high-performance JSON Schema evaluator (draft 4 to 2020-12), backed by Rust."
  spec.description = "JSON Schema validation for Ruby with the corvus-json-schema Rust crate: drafts 4, 6, 7, " \
                     "2019-09 and 2020-12, results and annotations, Ruby values read in place."
  spec.homepage = "https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-rb/corvus-json-schema"
  spec.license = "Apache-2.0"
  spec.required_ruby_version = ">= 3.3"
  spec.metadata = {
    "source_code_uri" => "https://github.com/corvus-dotnet/Corvus.JsonSchema",
    "bug_tracker_uri" => "https://github.com/corvus-dotnet/Corvus.JsonSchema/issues",
    "rubygems_mfa_required" => "true"
  }
  spec.files = Dir["lib/**/*.rb", "Cargo.{toml,lock}", "ext/**/*.{rs,toml,rb}", "ext/**/vendor/**/*", "README.md", "LICENSE",
                   "VERSIONHISTORY.md"]
  spec.require_paths = ["lib"]
  spec.extensions = ["ext/corvus_json_schema/extconf.rb"]
  spec.add_dependency "rb_sys", "~> 0.9.130"
end
