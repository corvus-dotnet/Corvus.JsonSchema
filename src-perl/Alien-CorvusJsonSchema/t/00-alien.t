use Test2::V0;
use Test::Alien;
use Alien::CorvusJsonSchema;

alien_ok 'Alien::CorvusJsonSchema';

# The library links and answers, and is the version this distribution installs or found.
my $xs = <<'END';
#include "EXTERN.h"
#include "perl.h"
#include "XSUB.h"
#include "corvus_json_schema.h"

MODULE = main PACKAGE = main

unsigned int
version()
  CODE:
    RETVAL = cjs_version();
  OUTPUT:
    RETVAL

int
validates(const char *schema, const char *json)
  CODE:
    cjs_validator *validator = NULL;
    bool valid = false;
    if (cjs_compile(schema, strlen(schema), NULL, &validator) != CJS_OK) croak("compile failed");
    if (cjs_validator_validate_json(validator, json, strlen(json), &valid) != CJS_OK) croak("validate failed");
    cjs_validator_free(validator);
    RETVAL = valid;
  OUTPUT:
    RETVAL
END

xs_ok { xs => $xs, verbose => 1 }, with_subtest {
  my $version = main::version();
  ok $version >= ((0 << 16) | (1 << 8) | 4), sprintf('the C library is 0.1.4 or later (%d.%d.%d)',
    $version >> 16, ($version >> 8) & 0xff, $version & 0xff);
  is main::validates('{"type": "integer"}', '3'), 1, 'an integer is valid';
  is main::validates('{"type": "integer"}', '"3"'), 0, 'a string is not';
};

done_testing;
