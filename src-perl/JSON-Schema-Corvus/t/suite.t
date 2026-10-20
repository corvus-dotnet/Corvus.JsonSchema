# The JSON-Schema-Test-Suite (JSON_SCHEMA_TEST_SUITE, or the repository's submodule): every case of draft 4, 6, 7,
# 2019-09 and 2020-12, required, optional and optional/format, as JSON text, as a parsed document, at the verbose
# level through a collector, and as Perl data (decoded by JSON::PP), which must all give the expected verdict. Remote
# references are served from the suite's remotes. As the other runners, draft4/optional/zeroTerminatedFloats.json is
# left out (1.0 and 1 cannot be told apart there), and leap-second format cases are skipped. The Perl data is
# decoded with allow_bignum, so that big and precise numbers are Math::BigInt and Math::BigFloat, which are encoded
# exactly.
use strict;
use warnings;
use FindBin;
use lib "$FindBin::Bin/lib";
use File::Spec;
use JSON::PP ();
use Test::More;
use SuiteSupport qw(suite_root read_cases remote_resolver);
use JSON::Schema::Corvus;

my $root = suite_root() or plan skip_all => 'the JSON-Schema-Test-Suite is not there (set JSON_SCHEMA_TEST_SUITE)';

my %drafts = ('draft4' => 'draft4', 'draft6' => 'draft6', 'draft7' => 'draft7', 'draft2019-09' => 'draft2019-09',
  'draft2020-12' => 'draft2020-12');
my $excluded = 'draft4/optional/zeroTerminatedFloats.json';
my $resolver = remote_resolver(File::Spec->catdir($root, 'remotes'));
my $decoder = JSON::PP->new->allow_nonref->allow_bignum->utf8;
my $collector = JSON::Schema::Corvus::ResultsCollector->new('verbose');
my ($total, $failed, $skipped, $files) = (0, 0, 0, 0);

for my $draft (sort keys %drafts) {
  for my $sub ('', 'optional', 'optional/format') {
    my $directory = File::Spec->catdir($root, 'tests', $draft, split m{/}, $sub);
    opendir my $dh, $directory or next;
    for my $name (sort grep { /\.json\z/ } readdir $dh) {
      my $label = join '/', grep { length } $draft, $sub, $name;
      next if $label eq $excluded;
      $files++;
      run_file(File::Spec->catfile($directory, $name), $label, $drafts{$draft}, $sub eq 'optional/format');
    }
  }
}

sub fail_case {
  my ($what) = @_;
  $failed++;
  diag "FAIL: $what";
}

sub run_file {
  my ($path, $label, $dialect, $assert_format) = @_;
  for my $group (@{ read_cases($path) }) {
    my $validator = eval {
      JSON::Schema::Corvus->compile($group->{schema}, default_dialect => $dialect,
        resolve_document => $resolver, ($assert_format ? (assert_format => 1) : ()));
    };
    unless ($validator) {
      $total += @{ $group->{tests} };
      fail_case("$label [$group->{description}]: $@") for @{ $group->{tests} };
      next;
    }
    for my $test (@{ $group->{tests} }) {
      my $what = "$label [$group->{description}] $test->{description}";
      if ($assert_format && $what =~ /leap second/i) {
        $skipped++;
        next;
      }
      $total++;
      my $expected = $test->{valid} ? 1 : 0;
      my @verdicts = eval {
        my $document = JSON::Schema::Corvus::Document->parse($test->{data});
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

diag sprintf 'JSON-Schema-Test-Suite: %d/%d passed (%d files, %d leap-second cases skipped), as JSON text, as a '
  . 'document, through a collector and as Perl data', $total - $failed, $total, $files, $skipped;
ok $total >= 7000, "at least 7,000 cases ran ($total)";
is $failed, 0, 'every case gives the expected verdict';

done_testing;
