# The JSON-Schema-Test-Suite (JSON_SCHEMA_TEST_SUITE, or the repository's submodule): every case of draft 4, 6, 7,
# 2019-09 and 2020-12 (one tree, validation/tests, required and optional, each case's compatibility saying which drafts
# it applies to and its tags whether format is asserted), as JSON text, as a parsed document, at the verbose level
# through a collector, and as Perl data (decoded by JSON::PP), which must all give the expected verdict. Each case's
# externalSchemas are served to that case alone. As the other runners, draft4/optional/zero-terminated-floats.json is
# left out (1.0 and 1 cannot be told apart there). The Perl data is
# decoded with allow_bignum, so that big and precise numbers are Math::BigInt and Math::BigFloat, which are encoded
# exactly.
use strict;
use warnings;
use FindBin;
use lib "$FindBin::Bin/lib";
use File::Spec;
use JSON::PP ();
use Test::More;
use SuiteSupport qw(suite_root read_cases compatible case_resolver);
use JSON::Schema::Corvus::XS;

my $root = suite_root() or plan skip_all => 'the JSON-Schema-Test-Suite is not there (set JSON_SCHEMA_TEST_SUITE)';

my %releases = ('draft4' => 4, 'draft6' => 6, 'draft7' => 7, 'draft2019-09' => 2019, 'draft2020-12' => 2020);
my $excluded = 'draft4/optional/zero-terminated-floats.json';
# As the crate's runner, test cases left out by name (file and test description): the high-precision cases of
# optional/bignum.json, whose digits the crate's serde_json values do not hold (nor JSON::PP's doubles, for the Perl
# data, which reads 2^64 as a double even with allow_bignum).
my %excluded_cases = map { ("bignum.json|$_" => 1) } (
  'below the exclusive maximum is valid',
  'above the exclusive minimum is valid',
  'just above the maximum is invalid',
  'just below the minimum is invalid',
  'a slightly smaller decimal is invalid',
  'a slightly larger decimal is invalid',
  'distinct high-precision decimals are unique',
  'arrays containing distinct decimals are unique',
  'objects containing distinct decimals are unique',
);
my $decoder = JSON::PP->new->allow_nonref->allow_bignum->utf8;
my $collector = JSON::Schema::Corvus::XS::ResultsCollector->new('verbose');
my ($total, $failed, $files) = (0, 0, 0);

my $tests = File::Spec->catdir($root, qw(validation tests));
for my $draft (sort keys %releases) {
  for my $sub ('', 'optional') {
    my $directory = File::Spec->catdir($tests, split m{/}, $sub);
    opendir my $dh, $directory or next;
    for my $name (sort grep { /\.json\z/ } readdir $dh) {
      my $label = join '/', grep { length } $draft, $sub, $name;
      next if $label eq $excluded;
      $files++;
      run_file(File::Spec->catfile($directory, $name), $label, $draft, $releases{$draft});
    }
  }
}

sub fail_case {
  my ($what) = @_;
  $failed++;
  diag "FAIL: $what";
}

sub run_file {
  my ($path, $label, $dialect, $release) = @_;
  for my $group (@{ read_cases($path) }) {
    next unless compatible($group->{compatibility}, $release);
    my %tags = map { $_ => 1 } @{ $group->{tags} };
    # The evaluator asserts format by default up to draft7, so a format-annotation case does not apply there.
    next if $tags{'format-annotation'} && $release <= 7;
    my $assert_format = $tags{'format-assertion'};
    my $resolver = case_resolver($group->{external_schemas});
    my $validator = eval {
      JSON::Schema::Corvus::XS->compile($group->{schema}, default_dialect => $dialect,
        ($resolver ? (resolve_document => $resolver) : ()), ($assert_format ? (assert_format => 1) : ()));
    };
    unless ($validator) {
      $total += @{ $group->{tests} };
      fail_case("$label [$group->{description}]: $@") for @{ $group->{tests} };
      next;
    }
    for my $test (@{ $group->{tests} }) {
      next if $excluded_cases{ ($path =~ m{([^/\\]+)\z})[0] . "|$test->{description}" };
      my $what = "$label [$group->{description}] $test->{description}";
      $total++;
      my $expected = $test->{valid} ? 1 : 0;
      my @verdicts = eval {
        my $document = JSON::Schema::Corvus::XS::Document->parse($test->{data});
        (
          $validator->is_valid_json($test->{data}) ? 1 : 0,
          $validator->is_valid_document($document) ? 1 : 0,
          $validator->evaluate_json($test->{data}, $collector) ? 1 : 0,
          $validator->is_valid($decoder->decode($test->{data})) ? 1 : 0,
        );
      };
      if (!@verdicts) {
        fail_case("$what: $@");
      }
      elsif (grep { $_ != $expected } @verdicts) {
        fail_case("$what: expected " . ($expected ? 'valid' : 'invalid') . ", got (text, document, collector, data) "
          . join(',', @verdicts));
      }
    }
  }
}

diag sprintf 'JSON-Schema-Test-Suite: %d/%d passed (%d files), as JSON text, as a document, through a collector and '
  . 'as Perl data', $total - $failed, $total, $files;
ok $total >= 8000, "at least 8,000 cases ran ($total)";
is $failed, 0, 'every case gives the expected verdict';

done_testing;
