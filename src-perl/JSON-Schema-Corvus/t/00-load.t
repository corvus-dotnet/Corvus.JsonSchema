use strict;
use warnings;
use Test::More;

use_ok 'JSON::Schema::Corvus';
my $v = JSON::Schema::Corvus->compile('{"type": "integer"}');
ok $v->is_valid_json('3') && !$v->is_valid_json('"3"'), 'a validator answers';

done_testing;
