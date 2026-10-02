# frozen_string_literal: true

# The JSON-Schema-Test-Suite (the repository's submodule, or JSON_SCHEMA_TEST_SUITE): every case with its data read
# in place from Ruby values (JSON.parse) and as JSON text, and at the verbose level through a collector, which must
# give the same verdict. Remote references are served by a resolver over the suite's remotes.
require "json"
require "minitest/autorun"
require "corvus_json_schema"

class TestSuite < Minitest::Test
  ROOT = ENV.fetch("JSON_SCHEMA_TEST_SUITE") { File.expand_path("../../../JSON-Schema-Test-Suite", __dir__) }
  DRAFTS = {
    "draft4" => :draft4, "draft6" => :draft6, "draft7" => :draft7,
    "draft2019-09" => :draft201909, "draft2020-12" => :draft202012
  }.freeze
  # As the other runners: a JSON parser cannot tell 1.0 from 1 here (draft 4 does not count 1.0 as an integer).
  EXCLUDED = ["draft4/optional/zeroTerminatedFloats.json"].freeze

  def resolver
    remotes = File.join(ROOT, "remotes")
    lambda do |uri|
      next nil unless uri.start_with?("http://localhost:1234/")

      path = File.join(remotes, uri.delete_prefix("http://localhost:1234/"))
      File.exist?(path) ? File.read(path) : nil
    end
  end

  def runs
    DRAFTS.flat_map do |draft, dialect|
      dir = File.join(ROOT, "tests", draft)
      Dir[File.join(dir, "*.json")].sort.map { |f| [f, dialect, false] } +
        Dir[File.join(dir, "optional", "*.json")].sort.map { |f| [f, dialect, false] } +
        Dir[File.join(dir, "optional", "format", "*.json")].sort.map { |f| [f, dialect, true] }
    end
  end

  def test_json_schema_test_suite
    skip "JSON-Schema-Test-Suite not found at #{ROOT}" unless Dir.exist?(File.join(ROOT, "tests"))
    total = 0
    failures = []
    runs.each do |file, dialect, assert_format|
      label = file.delete_prefix(File.join(ROOT, "tests") + "/")
      next if EXCLUDED.include?(label)

      JSON.parse(File.read(file)).each do |group|
        validator = CorvusJsonSchema.compile(group["schema"], default_dialect: dialect,
                                                              assert_format: assert_format ? true : nil,
                                                              resolver: resolver)
        group["tests"].each do |test|
          total += 1
          what = "#{label} [#{group["description"]}] #{test["description"]}"
          next if assert_format && what.downcase.include?("leap second")

          expected = test["valid"]
          actual = validator.valid?(test["data"])
          failures << "#{what}: expected #{expected}, got #{actual}" if actual != expected
          text = validator.valid_json?(JSON.generate(test["data"], quirks_mode: true))
          failures << "#{what}: valid_json? gave #{text}" if text != expected
          collector = CorvusJsonSchema::Collector.new(:verbose)
          collected = validator.evaluate(test["data"], collector)
          failures << "#{what}: evaluate gave #{collected}" if collected != expected
        end
      rescue CorvusJsonSchema::Error => e
        failures << "#{label} [#{group["description"]}]: #{e.class}: #{e.message}"
      end
    end
    assert_operator total, :>, 7000
    assert_empty failures, "#{failures.size} of #{total} failed:\n#{failures.first(40).join("\n")}"
    puts "\n#{total} cases: every one read in place, as JSON text and through a collector"
  end
end
