package SuiteSupport;

# Support for the tests that run the JSON-Schema-Test-Suite: finding it, reading its files with each value's exact
# JSON text, and serving its remote documents.

use strict;
use warnings;
use Exporter 'import';
use File::Spec;
use JSON::PP ();

our @EXPORT_OK = qw(suite_root read_cases remote_resolver read_text);

# The suite's directory: JSON_SCHEMA_TEST_SUITE, or the repository's submodule. undef when it is not there, and a
# failure when JSON_SCHEMA_TEST_SUITE names a directory that is not the suite (CI sets it, so the suite must run).
sub suite_root {
  my $probe = sub { -f File::Spec->catfile($_[0], qw(tests draft2020-12 type.json)) };
  if (defined $ENV{JSON_SCHEMA_TEST_SUITE}) {
    return $ENV{JSON_SCHEMA_TEST_SUITE} if $probe->($ENV{JSON_SCHEMA_TEST_SUITE});
    die "JSON_SCHEMA_TEST_SUITE is $ENV{JSON_SCHEMA_TEST_SUITE}, which is not the JSON-Schema-Test-Suite\n";
  }
  my $submodule = File::Spec->catdir(qw(.. .. JSON-Schema-Test-Suite));
  return $probe->($submodule) ? $submodule : undef;
}

sub read_text {
  my ($path) = @_;
  open my $file, '<:raw', $path or die "cannot read $path: $!";
  local $/;
  return scalar <$file>;
}

# A test file as a list of groups, each { description, schema => raw JSON text, tests => [ { description, data =>
# raw JSON text, valid } ] }. The raw text is a slice of the file's bytes, so numbers are exactly as written.
sub read_cases {
  my ($path) = @_;
  my $text = read_text($path);
  my $pos = 0;
  my $tree = _value(\$text, \$pos);
  my @groups;
  for my $group (@{ $tree->{items} }) {
    my %g = _members($group);
    my @tests;
    for my $test (@{ $g{tests}{items} }) {
      my %t = _members($test);
      push @tests, {
        description => _decode(\$text, $t{description}),
        data        => _raw(\$text, $t{data}),
        valid       => _raw(\$text, $t{valid}) eq 'true',
      };
    }
    push @groups, { description => _decode(\$text, $g{description}), schema => _raw(\$text, $g{schema}), tests => \@tests };
  }
  return \@groups;
}

sub _members {
  my ($object) = @_;
  return map { $_->[0] => $_->[1] } @{ $object->{members} };
}

sub _raw {
  my ($text, $node) = @_;
  return substr($$text, $node->{start}, $node->{end} - $node->{start});
}

sub _decode {
  my ($text, $node) = @_;
  return JSON::PP->new->allow_nonref->utf8->decode(_raw($text, $node));
}

sub _ws {
  my ($text, $pos) = @_;
  pos($$text) = $$pos;
  $$text =~ /\G[ \t\r\n]*/gc;
  $$pos = pos($$text);
}

# A JSON value at $$pos: { start, end } with members (an object's names and values) or items (an array's values).
sub _value {
  my ($text, $pos) = @_;
  _ws($text, $pos);
  my $start = $$pos;
  my $c = substr($$text, $$pos, 1);
  my $node = { start => $start };
  if ($c eq '{') {
    $$pos++;
    my @members;
    _ws($text, $pos);
    if (substr($$text, $$pos, 1) eq '}') { $$pos++ }
    else {
      while (1) {
        my $name = _value($text, $pos);
        _ws($text, $pos);
        die "expected : at $$pos" unless substr($$text, $$pos++, 1) eq ':';
        my $value = _value($text, $pos);
        push @members, [ _decode($text, $name), $value ];
        _ws($text, $pos);
        my $next = substr($$text, $$pos++, 1);
        last if $next eq '}';
        die "expected , or } at $$pos" unless $next eq ',';
      }
    }
    $node->{members} = \@members;
  }
  elsif ($c eq '[') {
    $$pos++;
    my @items;
    _ws($text, $pos);
    if (substr($$text, $$pos, 1) eq ']') { $$pos++ }
    else {
      while (1) {
        push @items, _value($text, $pos);
        _ws($text, $pos);
        my $next = substr($$text, $$pos++, 1);
        last if $next eq ']';
        die "expected , or ] at $$pos" unless $next eq ',';
      }
    }
    $node->{items} = \@items;
  }
  else {
    pos($$text) = $$pos;
    $$text =~ /\G(?:"(?:[^"\\]|\\.)*"|-?[0-9][0-9.eE+-]*|true|false|null)/gcs or die "unexpected text at $$pos";
    $$pos = pos($$text);
  }
  $node->{end} = $$pos;
  return $node;
}

# A resolver that serves the suite's remotes at http://localhost:1234/.
sub remote_resolver {
  my ($remotes) = @_;
  return sub {
    my ($uri) = @_;
    return undef unless $uri =~ m{\Ahttp://localhost:1234/(.+)\z};
    my $path = File::Spec->catfile($remotes, split m{/}, $1);
    return -f $path ? read_text($path) : undef;
  };
}

1;
