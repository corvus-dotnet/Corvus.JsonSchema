# The JSON-Schema-Test-Suite annotation tests (annotations/tests), through a verbose collector, as the other ports run
# them: every draft, groups filtered by "compatibility", and each assertion's expected annotations compared with the
# collector's, grouped by instance location, keyword and schema location. The files are decoded by JSON::PP (with
# allow_bignum) and given as Perl data, and two values are compared with the validator itself, by validating one
# against {"const": <the other>}: numbers by value, object members in any order.
use strict;
use warnings;
use FindBin;
use lib "$FindBin::Bin/lib";
use File::Spec;
use JSON::PP ();
use Test::More;
use SuiteSupport qw(suite_root remote_resolver read_text);
use JSON::Schema::Corvus;

my $root = suite_root() or plan skip_all => 'the JSON-Schema-Test-Suite is not there (set JSON_SCHEMA_TEST_SUITE)';

my @drafts = (['draft4', '4'], ['draft6', '6'], ['draft7', '7'], ['draft2019-09', '2019'], ['draft2020-12', '2020']);
my @order = qw(3 4 6 7 2019 2020);
my %position = map { $order[$_] => $_ + 1 } 0 .. $#order;
my $resolver = remote_resolver(File::Spec->catdir($root, 'remotes'));
my $decoder = JSON::PP->new->allow_bignum->utf8;
my $collector = JSON::Schema::Corvus::ResultsCollector->new('verbose');
my $directory = File::Spec->catdir($root, qw(annotations tests));
opendir my $dh, $directory or die "cannot read $directory: $!";
my @names = sort grep { /\.json\z/ } readdir $dh;
my ($total, $failed) = (0, 0);

# Whether a group marked with compatibility applies to the draft at level: "2019" means 2019-09 and later, and "<=7"
# means draft 7 and earlier.
sub compatible {
  my ($level, $compatibility) = @_;
  if ($compatibility =~ /\A<=(.+)\z/) {
    my $limit = $position{$1} or return 0;
    return $position{$level} <= $limit;
  }
  my $limit = $position{$compatibility} or return 0;
  return $position{$level} >= $limit;
}

sub fail_assertion {
  my ($what) = @_;
  $failed++;
  diag "FAIL: $what";
}

for my $draft (@drafts) {
  my ($dialect, $level) = @$draft;
  for my $name (@names) {
    my $label = "$dialect/$name";
    my $file = $decoder->decode(read_text(File::Spec->catfile($directory, $name)));
    for my $group (@{ $file->{suite} }) {
      next if defined $group->{compatibility} && !compatible($level, $group->{compatibility});
      my $validator = eval {
        JSON::Schema::Corvus->compile($group->{schema}, default_dialect => $dialect, resolve_document => $resolver);
      };
      unless ($validator) {
        fail_assertion("$label [$group->{description}]: $@");
        next;
      }
      for my $test (@{ $group->{tests} }) {
        unless (eval { $validator->evaluate($test->{instance}, $collector); 1 }) {
          fail_assertion("$label [$group->{description}]: $@");
          next;
        }
        my $produced = $collector->annotations;
        for my $assertion (@{ $test->{assertions} }) {
          $total++;
          my $actual = $produced->{ $assertion->{location} }{ $assertion->{keyword} } // {};
          my $same = eval {
            JSON::Schema::Corvus->compile({ const => $assertion->{expected} })->is_valid($actual);
          };
          unless ($same) {
            fail_assertion("$label [$group->{description}] '$assertion->{location}' $assertion->{keyword}: expected "
              . JSON::PP->new->canonical->allow_bignum->encode($assertion->{expected}) . ', actual '
              . JSON::PP->new->canonical->allow_bignum->encode($actual) . ($@ ? " ($@)" : ''));
          }
        }
      }
    }
  }
}

diag "$total annotation assertions, $failed failed";
ok $total > 0, 'annotation assertions ran';
is $failed, 0, 'every assertion has the expected annotations';

done_testing;
