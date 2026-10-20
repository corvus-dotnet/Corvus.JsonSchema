package JSON::Schema::Corvus::Dialect;

# JSON Schema dialects, vocabularies and the keywords that hold subschemas. A port of the Python package's dialect.py.

use 5.026;
use strict;
use warnings;
use Exporter 'import';

# The dialects, in specification order.
use constant { DRAFT4 => 0, DRAFT6 => 1, DRAFT7 => 2, DRAFT201909 => 3, DRAFT202012 => 4 };

# The vocabularies in effect for a schema resource (2019-09 and later), as bits.
use constant {
  V_NONE              => 0,
  V_CORE              => 1 << 0,
  V_APPLICATOR        => 1 << 1,
  V_VALIDATION        => 1 << 2,
  V_META_DATA         => 1 << 3,
  V_FORMAT_ANNOTATION => 1 << 4,
  V_FORMAT_ASSERTION  => 1 << 5,
  V_CONTENT           => 1 << 6,
  V_UNEVALUATED       => 1 << 7,
};
use constant V_ALL_ANNOTATING_FORMAT => V_CORE | V_APPLICATOR | V_VALIDATION | V_META_DATA | V_FORMAT_ANNOTATION
  | V_CONTENT | V_UNEVALUATED;

# Which keywords hold subschemas.
use constant { K_NONE => 0, K_SINGLE => 1, K_SINGLE_OR_ARRAY => 2, K_ARRAY => 3, K_MAP => 4 };

our @EXPORT_OK = qw(
  DRAFT4 DRAFT6 DRAFT7 DRAFT201909 DRAFT202012
  V_NONE V_CORE V_APPLICATOR V_VALIDATION V_META_DATA V_FORMAT_ANNOTATION V_FORMAT_ASSERTION V_CONTENT V_UNEVALUATED
  V_ALL_ANNOTATING_FORMAT
  K_NONE K_SINGLE K_SINGLE_OR_ARRAY K_ARRAY K_MAP
  known_dialect vocabulary_flag subschema_kind dialect_by_name
);
our %EXPORT_TAGS = (all => \@EXPORT_OK);

my %KNOWN_DIALECTS = (
  'http://json-schema.org/draft-04/schema'       => DRAFT4,
  'http://json-schema.org/draft-06/schema'       => DRAFT6,
  'http://json-schema.org/draft-07/schema'       => DRAFT7,
  'https://json-schema.org/draft/2019-09/schema' => DRAFT201909,
  'https://json-schema.org/draft/2020-12/schema' => DRAFT202012,
);

# The dialect of a well-known metaschema URI (normalised, no fragment), or undef.
sub known_dialect { $KNOWN_DIALECTS{ $_[0] } }

my %BY_NAME = ('draft4' => DRAFT4, 'draft6' => DRAFT6, 'draft7' => DRAFT7, 'draft2019-09' => DRAFT201909,
  'draft2020-12' => DRAFT202012);

# The dialect of an option's name (draft4 to draft2020-12), or undef.
sub dialect_by_name { $BY_NAME{ $_[0] } }

my %VOCABULARIES = (
  'https://json-schema.org/draft/2020-12/vocab/core'              => V_CORE,
  'https://json-schema.org/draft/2019-09/vocab/core'              => V_CORE,
  'https://json-schema.org/draft/2020-12/vocab/applicator'        => V_APPLICATOR,
  'https://json-schema.org/draft/2019-09/vocab/applicator'        => V_APPLICATOR,
  'https://json-schema.org/draft/2020-12/vocab/validation'        => V_VALIDATION,
  'https://json-schema.org/draft/2019-09/vocab/validation'        => V_VALIDATION,
  'https://json-schema.org/draft/2020-12/vocab/meta-data'         => V_META_DATA,
  'https://json-schema.org/draft/2019-09/vocab/meta-data'         => V_META_DATA,
  'https://json-schema.org/draft/2020-12/vocab/format-annotation' => V_FORMAT_ANNOTATION,
  'https://json-schema.org/draft/2019-09/vocab/format'            => V_FORMAT_ANNOTATION,
  'https://json-schema.org/draft/2020-12/vocab/format-assertion'  => V_FORMAT_ASSERTION,
  'https://json-schema.org/draft/2020-12/vocab/content'           => V_CONTENT,
  'https://json-schema.org/draft/2019-09/vocab/content'           => V_CONTENT,
  'https://json-schema.org/draft/2020-12/vocab/unevaluated'       => V_UNEVALUATED,
);

sub vocabulary_flag { $VOCABULARIES{ $_[0] } // V_NONE }

# Which keywords hold subschemas, by dialect (SchemaKeywords.GetSubschemaKind).
sub subschema_kind {
  my ($keyword, $dialect, $legacy_ref_overrides_siblings) = @_;
  return K_MAP if $keyword eq 'definitions' || $keyword eq '$defs';
  return K_NONE if $legacy_ref_overrides_siblings;
  return K_MAP if $keyword eq 'properties' || $keyword eq 'patternProperties' || $keyword eq 'dependencies';
  return K_SINGLE if $keyword eq 'additionalProperties' || $keyword eq 'not';
  return K_ARRAY if $keyword eq 'allOf' || $keyword eq 'anyOf' || $keyword eq 'oneOf';
  return $dialect >= DRAFT202012 ? K_SINGLE : K_SINGLE_OR_ARRAY if $keyword eq 'items';
  return $dialect <= DRAFT201909 ? K_SINGLE : K_NONE if $keyword eq 'additionalItems';
  return $dialect >= DRAFT6 ? K_SINGLE : K_NONE if $keyword eq 'contains' || $keyword eq 'propertyNames';
  return $dialect >= DRAFT7 ? K_SINGLE : K_NONE if $keyword eq 'if' || $keyword eq 'then' || $keyword eq 'else';
  return $dialect >= DRAFT201909 ? K_SINGLE : K_NONE
    if $keyword eq 'unevaluatedProperties' || $keyword eq 'unevaluatedItems' || $keyword eq 'contentSchema';
  return $dialect >= DRAFT201909 ? K_MAP : K_NONE if $keyword eq 'dependentSchemas';
  return $dialect >= DRAFT202012 ? K_ARRAY : K_NONE if $keyword eq 'prefixItems';
  return K_NONE;
}

1;
