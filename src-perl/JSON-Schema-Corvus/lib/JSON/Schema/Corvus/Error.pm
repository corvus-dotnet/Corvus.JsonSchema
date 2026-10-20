package JSON::Schema::Corvus::Error;

# The exceptions of JSON::Schema::Corvus, which stringify to their message. The same classes, under
# JSON::Schema::Corvus::XS::Error, are those of the XS package.

use 5.026;
use strict;
use warnings;

use overload '""' => sub { $_[0]{message} }, 'bool' => sub { 1 }, fallback => 1;

sub _new {
  my ($class, $message, %fields) = @_;
  return bless { message => $message, %fields }, $class;
}

sub throw_usage {
  my ($class, $message) = @_;
  die JSON::Schema::Corvus::Error->_new($message);
}

sub message { $_[0]{message} }

package JSON::Schema::Corvus::Error::InvalidJson;
our @ISA = ('JSON::Schema::Corvus::Error');

sub _new {
  my ($class, $message, $offset) = @_;
  return $class->SUPER::_new($message, offset => $offset);
}

sub offset { $_[0]{offset} }

package JSON::Schema::Corvus::Error::Compilation;
our @ISA = ('JSON::Schema::Corvus::Error');

package JSON::Schema::Corvus::Error::PatternTooLarge;
our @ISA = ('JSON::Schema::Corvus::Error::Compilation');

package JSON::Schema::Corvus::Error::Depth;
our @ISA = ('JSON::Schema::Corvus::Error');

sub _new {
  my ($class) = @_;
  return $class->SUPER::_new('The schema recursed in place beyond the maximum depth.');
}

package JSON::Schema::Corvus::Error::PatternMatchLimit;
our @ISA = ('JSON::Schema::Corvus::Error');

sub pattern { $_[0]{pattern} }
sub limit { $_[0]{limit} }

1;
