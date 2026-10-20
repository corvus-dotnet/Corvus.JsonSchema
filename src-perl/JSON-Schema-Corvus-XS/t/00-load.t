use strict;
use warnings;
use Test::More;

use_ok 'JSON::Schema::Corvus::XS';
diag 'C library ' . JSON::Schema::Corvus::XS::library_version_string();
ok JSON::Schema::Corvus::XS::library_version() >= JSON::Schema::Corvus::XS::REQUIRED_LIBRARY_VERSION(),
  'the C library is recent enough';

done_testing;
