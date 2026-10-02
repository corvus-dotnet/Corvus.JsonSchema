# frozen_string_literal: true

require "json"
require "minitest/autorun"
require "corvus_json_schema"

class TestApi < Minitest::Test
  PERSON = { "type" => "object", "required" => ["name"],
             "properties" => { "name" => { "type" => "string", "minLength" => 1, "title" => "Name" } } }.freeze

  def test_validates_ruby_values_in_place
    v = CorvusJsonSchema.compile(PERSON)
    assert v.valid?({ "name" => "a" })
    assert v.valid?({ name: "a" }), "Symbol keys"
    refute v.valid?({ "name" => "" })
    refute v.valid?({})
    refute v.valid?([1])
    assert CorvusJsonSchema.compile({ "type" => "string" }).valid?(:symbol), "a Symbol is a string"
    numbers = CorvusJsonSchema.compile({ "type" => "integer", "maximum" => 2**70 })
    assert numbers.valid?(2**64 + 1), "beyond 64 bits, the nearest double"
    refute numbers.valid?(2**71)
    refute numbers.valid?(1.5)
  end

  def test_compiles_json_text_and_validates_json_text
    v = CorvusJsonSchema.compile(JSON.generate(PERSON))
    assert v.valid_json?('{"name": "x"}')
    refute v.valid_json?('{"name": 1}')
    error = assert_raises(CorvusJsonSchema::InvalidJsonError) { v.valid_json?("{") }
    assert_match(/byte 1/, error.message)
    assert_raises(CorvusJsonSchema::InvalidJsonError) { CorvusJsonSchema.compile("{nope") }
  end

  # A value the schema examines that is not JSON is converted, and the conversion raises; values are read lazily, so
  # one the schema never examines (an item, with no items keyword) is not checked.
  def test_values_that_are_not_json_fall_back_to_conversion
    v = CorvusJsonSchema.compile({ "type" => "array", "items" => { "type" => "string" } })
    assert_raises(TypeError) { v.valid?([Time.now]) }
    assert_raises(TypeError) { CorvusJsonSchema.compile({ "type" => "object" }).valid?({ 1 => 2 }) }
    assert_raises(ArgumentError) { CorvusJsonSchema.compile({ "type" => "number" }).valid?(Float::NAN) }
    # A string that is not valid UTF-8 is not a JSON string, even where only its type is examined.
    assert_raises(EncodingError) { CorvusJsonSchema.compile({ "type" => "string" }).valid?("\xff".dup.force_encoding("UTF-8")) }
    assert CorvusJsonSchema.compile({ "type" => "array" }).valid?([Time.now]), "an item no keyword examines"
  end

  def test_errors
    assert_raises(CorvusJsonSchema::CompilationError) { CorvusJsonSchema.compile({ "$ref" => "http://example.com/x.json" }) }
    looping = CorvusJsonSchema.compile({ "$defs" => { "a" => { "$ref" => "#/$defs/a" } }, "$ref" => "#/$defs/a" },
                                       max_depth: 16)
    assert_raises(CorvusJsonSchema::DepthError) { looping.valid?(1) }
    assert_raises(ArgumentError) { CorvusJsonSchema.compile({}, default_dialect: :draft5) }
    assert_raises(ArgumentError) { CorvusJsonSchema::Collector.new(:everything) }
    assert CorvusJsonSchema::CompilationError < CorvusJsonSchema::Error
  end

  def test_options
    v = CorvusJsonSchema.compile({ "$defs" => { "item" => { "type" => "string" } }, "type" => "object" },
                                 entry_point: "#/$defs/item", default_dialect: :draft7)
    assert v.valid?("x")
    refute v.valid?({})
    date = { "format" => "date" }
    assert CorvusJsonSchema.compile(date).valid?("nope"), "format annotates by default"
    refute CorvusJsonSchema.compile(date, assert_format: true).valid?("nope")
  end

  def test_format_validators_and_their_exceptions
    calls = 0
    v = CorvusJsonSchema.compile({ "items" => { "format" => "even" } }, assert_format: true,
                                                                        formats: { even: lambda { |s|
                                                                          calls += 1
                                                                          s.length.even?
                                                                        } })
    assert v.valid?(%w[ab cdef])
    refute v.valid?(%w[ab c])
    assert v.valid_json?('["ab"]')
    assert_equal 5, calls
    raising = CorvusJsonSchema.compile({ "format" => "boom" }, assert_format: true,
                                                               formats: { "boom" => ->(_s) { raise "no" } })
    error = assert_raises(RuntimeError) { raising.valid?("x") }
    assert_equal "no", error.message
  end

  def test_resolver
    positive = { "type" => "integer", "minimum" => 1 }
    v = CorvusJsonSchema.compile({ "$ref" => "http://example.com/positive.json" },
                                 resolver: ->(uri) { uri == "http://example.com/positive.json" ? positive : nil })
    assert v.valid?(3)
    refute v.valid?(0)
    error = assert_raises(CorvusJsonSchema::CompilationError) do
      CorvusJsonSchema.compile({ "$ref" => "http://example.com/y.json" }, resolver: ->(_uri) { raise "offline" })
    end
    assert_match(/offline/, error.message)
  end

  def test_collectors_and_annotations
    v = CorvusJsonSchema.compile(PERSON)
    collector = CorvusJsonSchema::Collector.new(:detailed)
    refute v.evaluate({ "name" => "" }, collector)
    row = collector.results.find { |r| !r[:is_match] && r[:instance_location] == "/name" }
    refute_nil row
    refute_empty row[:message]
    verbose = CorvusJsonSchema::Collector.new(:verbose)
    assert v.evaluate({ "name" => "a" }, verbose)
    assert_equal({ "#/properties/name" => "Name" }, verbose.annotations["/name"]["title"])
    verbose.clear
    assert_empty verbose.results
  end

  def test_crate_version
    assert_match(/\A\d+\.\d+\.\d+\z/, CorvusJsonSchema.crate_version)
  end
end
