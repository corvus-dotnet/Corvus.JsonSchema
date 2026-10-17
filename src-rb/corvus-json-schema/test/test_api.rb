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

  # A schema and an instance may both come from outside, so a match of a pattern has a limit of time (one second unless
  # pattern_match_limit says otherwise) and a pattern has limits of size. Reaching either raises an error of its own,
  # and is never an answer.
  NEVER_ANSWERS = '^(?:(a+)\\1*)+$'
  NEVER_ANSWERED = ("a" * 60) + "b"

  def test_a_match_that_reaches_its_limit_raises
    v = CorvusJsonSchema.compile({ "pattern" => NEVER_ANSWERS }, pattern_match_limit: 0.05)
    collector = CorvusJsonSchema::Collector.new(:basic)
    [
      -> { v.valid?(NEVER_ANSWERED) },
      -> { v.valid_json?(JSON.generate(NEVER_ANSWERED)) },
      -> { v.evaluate(NEVER_ANSWERED, collector) }
    ].each do |check|
      start = Process.clock_gettime(Process::CLOCK_MONOTONIC)
      error = assert_raises(CorvusJsonSchema::PatternMatchLimitError) { check.call }
      took = Process.clock_gettime(Process::CLOCK_MONOTONIC) - start
      assert_includes error.message, NEVER_ANSWERS
      assert_includes error.message, "50ms"
      assert_operator took, :>=, 0.05
      assert_operator took, :<, 20
    end
    assert v.valid?("aaaa")
    refute v.valid?("b")
    assert CorvusJsonSchema::PatternMatchLimitError < CorvusJsonSchema::Error
    refute CorvusJsonSchema::PatternMatchLimitError <= CorvusJsonSchema::DepthError
  end

  def test_the_default_match_limit_is_one_second_and_can_be_turned_off
    assert_equal 1.0, CorvusJsonSchema::DEFAULT_PATTERN_MATCH_LIMIT
    v = CorvusJsonSchema.compile({ "patternProperties" => { NEVER_ANSWERS => { "type" => "integer" } } })
    start = Process.clock_gettime(Process::CLOCK_MONOTONIC)
    error = assert_raises(CorvusJsonSchema::PatternMatchLimitError) { v.valid?({ NEVER_ANSWERED => 1 }) }
    took = Process.clock_gettime(Process::CLOCK_MONOTONIC) - start
    assert_includes error.message, "1s"
    assert_operator took, :>=, 1.0
    assert_operator took, :<, 21
    # Off: the match runs to its end. Fourteen characters take a moment, and the answer is no.
    [nil, 0, 0.0, -1].each do |off|
      refute CorvusJsonSchema.compile({ "pattern" => NEVER_ANSWERS }, pattern_match_limit: off).valid?(("a" * 14) + "b")
    end
    assert_raises(ArgumentError) { CorvusJsonSchema.compile({ "pattern" => "a" }, pattern_match_limit: Float::NAN) }
  end

  def test_a_match_that_reaches_its_memory_limit_raises
    v = CorvusJsonSchema.compile({ "pattern" => "(?:a?){2147483647}" })
    error = assert_raises(CorvusJsonSchema::PatternMatchLimitError) { v.valid?("") }
    assert_includes error.message, "1,048,576 backtracking entries"
  end

  def test_a_pattern_over_a_limit_of_size_is_not_an_invalid_pattern
    nest = ->(depth) { ("(?:" * depth) + "a" + (")" * depth) }
    [
      ["a" * 16_384, "a" * 16_385, "the pattern has a size of more than 1048576 bytes when it is read"],
      [nest.call(999), nest.call(1000), "the groups of the pattern nest more than 1000 deep"]
    ].each do |at, over, reason|
      CorvusJsonSchema.compile({ "pattern" => at })
      [{ "pattern" => over }, { "patternProperties" => { over => true } }].each do |schema|
        error = assert_raises(CorvusJsonSchema::PatternTooLargeError) { CorvusJsonSchema.compile(schema) }
        assert error.message.end_with?("is too large to read as an ECMA-262 regular expression: #{reason}.")
        refute_includes error.message.downcase, "invalid"
        assert_operator error.message.length, :<, 400
      end
    end
    # It is a compilation error, so code that rescues those still does. A pattern that is not valid is not it.
    assert CorvusJsonSchema::PatternTooLargeError < CorvusJsonSchema::CompilationError
    error = assert_raises(CorvusJsonSchema::CompilationError) { CorvusJsonSchema.compile({ "pattern" => "a**" }) }
    refute_kind_of CorvusJsonSchema::PatternTooLargeError, error
    assert error.message.start_with?("Invalid regular expression")
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

  # The crate before 0.1.4 kept this pattern's excluded set as ASCII bits and read é as the bits of C and ).
  def test_excluded_class_with_a_member_outside_ascii
    v = CorvusJsonSchema.compile({ "pattern" => '^(?=[^é]+$)(?=(.*\w)).+$' })
    assert v.valid?("C1")
    assert v.valid?(")a")
    refute v.valid?("é1")
    refute v.valid_json?('"é1"')
  end

  # The crate before 0.1.6 evaluated a not without the guard on in-place recursion, so the schemas whose not leads back
  # to the schema it is in overflowed the stack, which ends the process.
  def test_not_on_an_in_place_cycle_stops_at_max_depth
    looping = { "allOf" => [{ "$ref" => "#/$defs/loop" }] }
    schemas = [
      { "not" => { "$ref" => "#" } },
      { "not" => { "not" => { "$ref" => "#" } } },
      { "type" => "integer", "not" => { "$ref" => "#" } },
      { "allOf" => [{ "not" => { "$ref" => "#" } }] },
      { "$defs" => { "a" => { "not" => { "$ref" => "#/$defs/b" } }, "b" => { "not" => { "$ref" => "#/$defs/a" } } },
        "$ref" => "#/$defs/a" },
      { "$defs" => { "loop" => looping }, "not" => { "$ref" => "#/$defs/loop" } },
      { "$defs" => { "loop" => looping }, "not" => { "not" => { "$ref" => "#/$defs/loop" } } },
      { "$defs" => { "loop" => looping }, "properties" => { "a" => { "not" => { "$ref" => "#/$defs/loop" } } } },
      { "unevaluatedProperties" => false, "not" => { "$ref" => "#" } }
    ]
    schemas.each_with_index do |schema, s|
      v = CorvusJsonSchema.compile(schema, max_depth: 16)
      [1, "a", { "a" => 1 }, [1]].each do |instance|
        # Only an object with the property reaches the loop of the eighth schema, and anything but an integer fails
        # the type of the third before its not is reached, when failing fast.
        next if (s == 7 && !instance.is_a?(Hash)) || (s == 2 && instance != 1)

        what = "#{schema} with #{instance.inspect}"
        assert_raises(CorvusJsonSchema::DepthError, what) { v.valid?(instance) }
        assert_raises(CorvusJsonSchema::DepthError, what) { v.valid_json?(JSON.generate(instance)) }
        %i[basic detailed verbose].each do |level|
          assert_raises(CorvusJsonSchema::DepthError, what) do
            v.evaluate(instance, CorvusJsonSchema::Collector.new(level))
          end
        end
      end
    end
  end

  # The crate before 0.1.7 dropped a "$" at the end of the repeated group of a separated list, and after its first item,
  # so the first two patterns were matched as ^x(,a)*$ and the third as ^(a,)*b$. The answers are V8's.
  def test_a_separated_list_keeps_an_anchor_inside_it
    v = CorvusJsonSchema.compile({ "pattern" => '^x(,a$)*$' })
    assert v.valid?("x")
    assert v.valid?("x,a")
    refute v.valid?("x,a,a")
    refute v.valid_json?('"x,a,a"')
    v = CorvusJsonSchema.compile({ "pattern" => '^x$(,a)*$' })
    assert v.valid?("x")
    refute v.valid?("x,a")
    v = CorvusJsonSchema.compile({ "pattern" => '^(a,$)*b$' })
    assert v.valid?("b")
    refute v.valid?("a,b")
    # An escaped "$" is a literal character.
    v = CorvusJsonSchema.compile({ "pattern" => '^x(,a\$)*$' })
    assert v.valid?("x,a$,a$")
    refute v.valid?("x,a,a")
  end

  def test_crate_version
    assert_match(/\A\d+\.\d+\.\d+\z/, CorvusJsonSchema.crate_version)
  end
end
