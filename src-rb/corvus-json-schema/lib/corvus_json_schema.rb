# frozen_string_literal: true

# A high-performance JSON Schema evaluator (draft 4, 6, 7, 2019-09 and 2020-12) for Ruby, backed by the
# corvus-json-schema Rust crate, with results collection and annotations.
#
#   validator = CorvusJsonSchema.compile({ "type" => "object", "required" => ["id"] })
#   validator.valid?({ "id" => 3 })        # => true
#   validator.valid_json?('{"id": 3}')     # => true, parsed in Rust
#
# Values are read in place: Hash (String or Symbol keys), Array, String, Symbol (as its name), Integer, Float, true,
# false and nil. Integers beyond 64 bits become the nearest double. A value the schema examines that is none of these
# raises (TypeError; ArgumentError for NaN and infinities; EncodingError for a String that is not valid UTF-8); values
# are read only as the schema examines them.
require_relative "corvus_json_schema/version"

begin
  RUBY_VERSION =~ /(\d+\.\d+)/
  require_relative "corvus_json_schema/#{Regexp.last_match(1)}/corvus_json_schema"
rescue LoadError
  require_relative "corvus_json_schema/corvus_json_schema"
end

module CorvusJsonSchema
  DIALECTS = {
    draft4: 4, draft6: 6, draft7: 7, draft201909: 2019, draft202012: 2020
  }.freeze

  RESULTS_LEVELS = { basic: 0, detailed: 1, verbose: 2 }.freeze

  # Compiles a schema (a Hash or Array, true or false, or JSON text) into a Validator.
  #
  # Options: default_dialect (:draft4, :draft6, :draft7, :draft201909, :draft202012; the dialect of schemas without
  # $schema), assert_format (nil follows the vocabularies), assert_format_in_legacy_drafts, assert_content, formats (a
  # Hash of format name to a callable returning whether a string is valid), resolver (a callable from an absolute URI
  # to the document, as a Hash or JSON text, or nil when unknown), base_uri, entry_point (a reference such as
  # "#/$defs/item") and max_depth (of in-place recursion, default 128).
  def self.compile(schema, default_dialect: :draft202012, assert_format: nil, assert_format_in_legacy_drafts: false,
                   assert_content: true, formats: nil, resolver: nil, base_uri: nil, entry_point: nil, max_depth: 128)
    dialect = DIALECTS.fetch(default_dialect) { raise ArgumentError, "unknown dialect #{default_dialect.inspect}" }
    Native.compile(schema, dialect, assert_format, assert_format_in_legacy_drafts, assert_content, formats, resolver,
                   base_uri, entry_point, Integer(max_depth))
  end

  # The version of the corvus-json-schema crate the extension was built from.
  def self.crate_version
    Native.crate_version
  end

  # A compiled schema: valid?(value), valid_json?(text) and evaluate(value, collector).
  class Validator
    # Evaluates the value, replacing the collector's results with this evaluation's (every keyword is evaluated and
    # reported at the collector's level). Returns whether the value is valid.
    def evaluate(value, collector)
      native_evaluate(value, collector)
    end
  end

  # The results of the latest evaluation into it: results (rows as Hashes) and annotations (from a :verbose
  # evaluation, grouped by instance location, keyword and schema location).
  class Collector
    def self.new(level = :basic)
      native_new(RESULTS_LEVELS.fetch(level) { raise ArgumentError, "unknown results level #{level.inspect}" })
    end
  end
end
