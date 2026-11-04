# frozen_string_literal: true

# The JSON-Schema-Test-Suite (the repository's submodule, or JSON_SCHEMA_TEST_SUITE): every case with its data read
# in place from Ruby values (JSON.parse) and as JSON text, and at the verbose level through a collector, which must
# give the same verdict. Every dialect's cases are in one tree (validation/tests, required and optional): each case's
# compatibility selects the drafts it applies to, its tags whether format is asserted, and its externalSchemas are
# served to that case alone.
require "json"
require "minitest/autorun"
require "corvus_json_schema"

class TestSuite < Minitest::Test
  # Expanded so that Dir[] can glob it (on Windows a path from the environment has backslashes).
  ROOT = File.expand_path(ENV.fetch("JSON_SCHEMA_TEST_SUITE") { File.join(__dir__, "../../../JSON-Schema-Test-Suite") })
  TESTS = File.join(ROOT, "validation", "tests")
  DRAFTS = {
    "draft4" => [:draft4, 4], "draft6" => [:draft6, 6], "draft7" => [:draft7, 7],
    "draft2019-09" => [:draft201909, 2019], "draft2020-12" => [:draft202012, 2020]
  }.freeze
  # As the other runners: a JSON parser cannot tell 1.0 from 1 here (draft 4 does not count 1.0 as an integer).
  EXCLUDED = ["draft4/optional/zero-terminated-floats.json"].freeze
  # As the crate's runner, test cases left out by name (file and test description): the high-precision cases of
  # optional/bignum.json, whose digits the crate's serde_json values do not hold.
  EXCLUDED_CASES = [
    ["bignum.json", "below the exclusive maximum is valid"],
    ["bignum.json", "above the exclusive minimum is valid"],
    ["bignum.json", "just below the minimum is invalid"],
    ["bignum.json", "a slightly smaller decimal is invalid"],
    ["bignum.json", "a slightly larger decimal is invalid"],
    ["bignum.json", "distinct high-precision decimals are unique"],
    ["bignum.json", "arrays containing distinct decimals are unique"],
    ["bignum.json", "objects containing distinct decimals are unique"]
  ].freeze

  # Whether a test case's compatibility (a comma-separated list of minimums, <= maximums and = releases, from the
  # smallest release to the largest) admits the release, by the algorithm of the suite's validation/README.md.
  def compatible?(compatibility, release)
    return true if compatibility.nil?

    valid = true
    compatibility.split(",").each do |constraint|
      op, version = if constraint.start_with?("<=") then ["<=", constraint[2..]]
                    elsif constraint.start_with?("=") then ["=", constraint[1..]]
                    else ["", constraint]
                    end
      number = Integer(version.strip)
      valid = case op
              when "<=" then release <= number
              when "=" then release == number
              else release >= number
              end
      break if release <= number
    end
    valid
  end

  # A resolver serving a test case's externalSchemas at their retrieval URIs, as JSON text.
  def resolver_for(group)
    schemas = group["externalSchemas"]
    return nil if schemas.nil?

    ->(uri) { schemas.key?(uri) ? JSON.generate(schemas[uri]) : nil }
  end

  def runs
    files = Dir[File.join(TESTS, "*.json")].sort + Dir[File.join(TESTS, "optional", "*.json")].sort
    DRAFTS.flat_map do |draft, (dialect, release)|
      files.map { |f| [f, "#{draft}/#{f.delete_prefix("#{TESTS}/")}", dialect, release] }
    end
  end

  def test_json_schema_test_suite
    skip "JSON-Schema-Test-Suite not found at #{ROOT}" unless Dir.exist?(TESTS)
    total = 0
    failures = []
    runs.each do |file, label, dialect, release|
      next if EXCLUDED.include?(label)

      JSON.parse(File.read(file))["tests"].each do |group|
        next unless compatible?(group["compatibility"], release)

        tags = group.fetch("tags", [])
        # The evaluator asserts format by default up to draft7, so a format-annotation case does not apply there.
        next if tags.include?("format-annotation") && release <= 7

        assert_format = tags.include?("format-assertion")
        validator = CorvusJsonSchema.compile(group["schema"], default_dialect: dialect,
                                                              assert_format: assert_format ? true : nil,
                                                              resolver: resolver_for(group))
        group["tests"].each do |test|
          next if EXCLUDED_CASES.include?([File.basename(file), test["description"]])

          total += 1
          what = "#{label} [#{group["description"]}] #{test["description"]}"
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
    assert_operator total, :>, 8000
    assert_empty failures, "#{failures.size} of #{total} failed:\n#{failures.first(40).join("\n")}"
    puts "\n#{total} cases: every one read in place, as JSON text and through a collector"
  end
end
