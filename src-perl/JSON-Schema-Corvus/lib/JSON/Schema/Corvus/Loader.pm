package JSON::Schema::Corvus::Loader;

# Loads schema documents, identifies resources and anchors, and resolves references. A port of the Python package's
# loader.py (Corvus.Text.Json.RuntimeEvaluator.Compilation.SchemaLoader): elements are identified by (document, JSON
# pointer) rather than by a parsed document's row index.

use 5.026;
use strict;
use warnings;
use Scalar::Util ();
use JSON::Schema::Corvus::Dialect qw(:all);
use JSON::Schema::Corvus::Error;
use JSON::Schema::Corvus::Json;
use JSON::Schema::Corvus::Metaschemas;
use JSON::Schema::Corvus::Uri;

use constant DEFAULT_ROOT_URI => 'https://corvus-oss.org/runtime-evaluator/root.json';

my $Json = 'JSON::Schema::Corvus::Json';

sub _is_container { my $r = ref $_[0]; $r eq 'HASH' || $r eq 'ARRAY' }
sub _is_schema_value { my $r = ref $_[0]; $r eq 'HASH' || $r eq 'JSON::PP::Boolean' }
sub _is_string { JSON::Schema::Corvus::Json::is_string($_[0]) }

# --- Values by schema location ---
#
# Schema objects (hashes and arrays) are keyed by identity, which avoids hashing long pointers; the pointer decides
# for scalars and for objects in the document's shared set, which appear at more than one location (possible in a
# schema built in Perl, never in parsed JSON). The walk records those as it meets them.

sub _map_get {
  my ($doc, $map, $schema, $pointer) = @_;
  if (_is_container($schema)) {
    my $addr = Scalar::Util::refaddr($schema);
    return $doc->{$map}{object}{$addr} unless $doc->{shared}{$addr};
  }
  return $doc->{$map}{pointer}{$pointer};
}

sub _map_set {
  my ($doc, $map, $schema, $pointer, $value) = @_;
  if (_is_container($schema)) {
    my $addr = Scalar::Util::refaddr($schema);
    if (!$doc->{shared}{$addr}) {
      $doc->{$map}{object}{$addr} = $value;
      return;
    }
  }
  $doc->{$map}{pointer}{$pointer} = $value;
}

# The resource that owns a schema location, and the compiled node of one (which the compiler records).
sub resource_of { my ($self, $doc, $value, $pointer) = @_; _map_get($doc, 'resources', $value, $pointer) }
sub node_of { my ($self, $doc, $value, $pointer) = @_; _map_get($doc, 'nodes', $value, $pointer) }
sub set_node_of { my ($self, $doc, $value, $pointer, $id) = @_; _map_set($doc, 'nodes', $value, $pointer, $id) }

sub escape_pointer_token { JSON::Schema::Corvus::Uri::escape_pointer_token($_[0]) }

sub _value_at {
  my ($doc, $pointer) = @_;
  return (JSON::Schema::Corvus::Uri::resolve_pointer($doc->{root}, $pointer))[1];
}

# The root value of a resource.
sub resource_root {
  my ($resource) = @_;
  return _value_at($resource->{document}, $resource->{root_pointer});
}

# A reference's target: { document, pointer, value, resource }.
sub _target {
  my ($document, $pointer, $value, $resource) = @_;
  return { document => $document, pointer => $pointer, value => $value, resource => $resource };
}

sub new {
  my ($class, $options) = @_;
  return bless {
    options => $options,
    documents => [],
    resources => [],
    resources_by_uri => {},
    documents_by_uri => {},
    metaschema_info => {},
    metaschema_loading => {},
  }, $class;
}

sub load_root {
  my ($self, $schema, $base_uri) = @_;
  my $uri = defined $base_uri ? JSON::Schema::Corvus::Uri::normalize($base_uri) : DEFAULT_ROOT_URI;
  my $doc = $self->_add_document($uri, $schema);
  return _map_get($doc, 'resources', $doc->{root}, '');
}

sub load_root_from_uri {
  my ($self, $uri) = @_;
  my $normalized = JSON::Schema::Corvus::Uri::normalize($uri);
  die JSON::Schema::Corvus::Error::Compilation->_new("Unable to resolve the schema document '$uri'.")
    unless $self->_try_load_document($normalized);
  my $doc = $self->{documents_by_uri}{$normalized};
  return _map_get($doc, 'resources', $doc->{root}, '');
}

sub try_resolve_reference {
  my ($self, $source, $reference) = @_;
  my $cache = $source->{document}{reference_cache};
  my $key = "$source->{id} $reference";
  return $cache->{$key} if exists $cache->{$key};
  return $cache->{$key} = $self->_resolve_reference($source, $reference);
}

sub _resolve_reference {
  my ($self, $source, $reference) = @_;
  my ($uri_part, $fragment) = JSON::Schema::Corvus::Uri::split_fragment($reference);
  my $absolute = JSON::Schema::Corvus::Uri::resolve($source->{uri}, $uri_part);
  my $resource = $self->{resources_by_uri}{$absolute};
  if (!$resource) {
    return undef unless $self->_try_load_document($absolute);
    $resource = $self->{resources_by_uri}{$absolute} or return undef;
  }
  return $self->try_resolve_fragment($resource, JSON::Schema::Corvus::Uri::decode_fragment($fragment));
}

sub try_resolve_fragment {
  my ($self, $resource, $fragment) = @_;
  if ($fragment eq '') {
    return _target($resource->{document}, $resource->{root_pointer}, resource_root($resource), $resource);
  }
  if (substr($fragment, 0, 1) eq '/') {
    my ($found, $value, $path) = JSON::Schema::Corvus::Uri::resolve_pointer(resource_root($resource), $fragment);
    return undef unless $found;
    my $pointer = $resource->{root_pointer};
    $pointer .= '/' . escape_pointer_token("$_") for @$path;
    my $owner = _map_get($resource->{document}, 'resources', $value, $pointer) || $resource;
    return _target($resource->{document}, $pointer, $value, $owner);
  }
  my $anchor = $resource->{anchors} ? $resource->{anchors}{$fragment} : undef;
  return _target($resource->{document}, $anchor, _value_at($resource->{document}, $anchor), $resource)
    if defined $anchor;
  return undef;
}

# The dialect and vocabularies of a metaschema URI.
sub get_dialect_info {
  my ($self, $schema_uri) = @_;
  # The standard metaschema URIs, as usually written, need no normalisation.
  (my $plain_uri = $schema_uri) =~ s/#\z//;
  my $plain = known_dialect($plain_uri);
  return ($plain, V_ALL_ANNOTATING_FORMAT) if defined $plain;
  my $normalized = JSON::Schema::Corvus::Uri::normalize($schema_uri);
  my $known = known_dialect($normalized);
  return ($known, V_ALL_ANNOTATING_FORMAT) if defined $known;
  if (my $cached = $self->{metaschema_info}{$normalized}) {
    return @$cached;
  }
  return ($self->{options}{default_dialect}, V_ALL_ANNOTATING_FORMAT) if $self->{metaschema_loading}{$normalized};
  local $self->{metaschema_loading}{$normalized} = 1;
  my $fallback = [ $self->{options}{default_dialect}, V_ALL_ANNOTATING_FORMAT ];
  my $meta_resource = $self->{resources_by_uri}{$normalized};
  if (!$meta_resource) {
    if (!$self->_try_load_document($normalized) || !($meta_resource = $self->{resources_by_uri}{$normalized})) {
      $self->{metaschema_info}{$normalized} = $fallback;
      return @$fallback;
    }
  }
  my $meta = resource_root($meta_resource);
  my $vocabularies = V_ALL_ANNOTATING_FORMAT;
  if (ref $meta eq 'HASH' && ref $meta->{'$vocabulary'} eq 'HASH') {
    $vocabularies = V_NONE;
    $vocabularies |= vocabulary_flag($_) for keys %{ $meta->{'$vocabulary'} };
    $vocabularies |= V_CORE;
  }
  my $info = [ $meta_resource->{dialect}, $vocabularies ];
  $self->{metaschema_info}{$normalized} = $info;
  return @$info;
}

sub _try_load_document {
  my ($self, $absolute_uri) = @_;
  return 1 if $self->{documents_by_uri}{$absolute_uri};
  my $resolver = $self->{options}{resolve_document};
  my $resolved = $resolver ? $resolver->($absolute_uri) : undef;
  if (defined $resolved) {
    $self->_add_document($absolute_uri, ref $resolved || JSON::Schema::Corvus::Json::is_bool($resolved)
      ? JSON::Schema::Corvus::Json::from_data($resolved) : JSON::Schema::Corvus::Json::decode($resolved, 1));
    return 1;
  }
  my $meta = $JSON::Schema::Corvus::Metaschemas::METASCHEMAS{$absolute_uri};
  if (defined $meta) {
    $self->_add_document($absolute_uri, JSON::Schema::Corvus::Json::decode($meta, 1));
    return 1;
  }
  return 0;
}

sub _add_document {
  my ($self, $uri, $root) = @_;
  my $doc = {
    id => scalar @{ $self->{documents} },
    root => $root,
    retrieval_uri => $uri,
    shared => {},
    first_location => {},
    resources => { object => {}, pointer => {} },
    nodes => { object => {}, pointer => {} },
    reference_cache => {},
  };
  push @{ $self->{documents} }, $doc;
  $self->{documents_by_uri}{$uri} = $doc;
  my ($dialect, $vocabularies) = $self->_get_root_dialect($root);
  my $resource = $self->_create_resource($doc, '', $uri, $dialect, $vocabularies);
  $self->_walk($doc, $root, '', $resource, 1);
  return $doc;
}

sub _get_root_dialect {
  my ($self, $root) = @_;
  return $self->get_dialect_info($root->{'$schema'}) if ref $root eq 'HASH' && _is_string($root->{'$schema'});
  return ($self->{options}{default_dialect}, V_ALL_ANNOTATING_FORMAT);
}

sub _create_resource {
  my ($self, $doc, $pointer, $uri, $dialect, $vocabularies) = @_;
  my $resource = {
    id => scalar @{ $self->{resources} },
    document => $doc,
    root_pointer => $pointer,
    uri => $uri,
    dialect => $dialect,
    vocabularies => $vocabularies,
    recursive_anchor => 0,
    anchors => undef,
    dynamic_anchors => undef,
  };
  push @{ $self->{resources} }, $resource;
  $self->{resources_by_uri}{$uri} //= $resource;
  return $resource;
}

sub _add_anchor {
  my ($resource, $name, $pointer) = @_;
  $resource->{anchors}{$name} //= $pointer;
}

sub _walk {
  my ($self, $doc, $element, $pointer, $resource, $is_resource_root) = @_;
  if (_is_container($element)) {
    my $key = Scalar::Util::refaddr($element);
    my $first = $doc->{first_location}{$key};
    if (!defined $first) {
      $doc->{first_location}{$key} = $pointer;
    }
    elsif ($first ne $pointer && !$doc->{shared}{$key}) {
      # Re-key what was recorded for the first location by its pointer.
      my $owner = _map_get($doc, 'resources', $element, $first);
      $doc->{shared}{$key} = 1;
      _map_set($doc, 'resources', $element, $first, $owner) if $owner;
    }
  }
  if (ref $element ne 'HASH') {
    _map_set($doc, 'resources', $element, $pointer, $resource);
    return;
  }

  my $dialect = $resource->{dialect};
  my $vocabularies = $resource->{vocabularies};
  if (!$is_resource_root && _is_string($element->{'$schema'})) {
    ($dialect, $vocabularies) = $self->get_dialect_info($element->{'$schema'});
  }

  my $legacy_ref_overrides_siblings = $dialect <= DRAFT7 && _is_string($element->{'$ref'});
  if (!$legacy_ref_overrides_siblings) {
    my $id_value = $dialect == DRAFT4 ? $element->{id} : $element->{'$id'};
    if (_is_string($id_value)) {
      my ($uri_part, $fragment) = JSON::Schema::Corvus::Uri::split_fragment($id_value);
      if ($uri_part eq '') {
        _add_anchor($resource, $fragment, $pointer) if length $fragment && $dialect <= DRAFT7;
      }
      else {
        my $absolute = JSON::Schema::Corvus::Uri::resolve($resource->{uri}, $uri_part);
        if (!$is_resource_root || $absolute ne $resource->{uri}) {
          if ($is_resource_root) {
            $self->{resources_by_uri}{$absolute} //= $resource;
            $resource->{uri} = $absolute;
          }
          else {
            $resource = $self->_create_resource($doc, $pointer, $absolute, $dialect, $vocabularies);
            $is_resource_root = 1;
          }
        }
        _add_anchor($resource, $fragment, $pointer) if length $fragment && $dialect <= DRAFT7;
      }
    }
    my $anchor = $element->{'$anchor'};
    _add_anchor($resource, $anchor, $pointer) if $dialect >= DRAFT201909 && _is_string($anchor);
    my $dynamic_anchor = $element->{'$dynamicAnchor'};
    if ($dialect >= DRAFT202012 && _is_string($dynamic_anchor)) {
      $resource->{dynamic_anchors}{$dynamic_anchor} //= $pointer;
      _add_anchor($resource, $dynamic_anchor, $pointer);
    }
    if ($dialect == DRAFT201909 && $is_resource_root && JSON::Schema::Corvus::Json::is_bool($element->{'$recursiveAnchor'})
      && $element->{'$recursiveAnchor'}) {
      $resource->{recursive_anchor} = 1;
    }
  }

  _map_set($doc, 'resources', $element, $pointer, $resource);

  for my $name ($Json->can('names')->($element)) {
    my $value = $element->{$name};
    my $base = $pointer . '/' . escape_pointer_token($name);
    my $kind = subschema_kind($name, $dialect, $legacy_ref_overrides_siblings);
    if ($kind == K_SINGLE) {
      $self->_walk($doc, $value, $base, $resource, 0) if _is_schema_value($value);
    }
    elsif ($kind == K_SINGLE_OR_ARRAY) {
      if (ref $value eq 'ARRAY') {
        $self->_walk($doc, $value->[$_], "$base/$_", $resource, 0) for 0 .. $#$value;
      }
      elsif (_is_schema_value($value)) {
        $self->_walk($doc, $value, $base, $resource, 0);
      }
    }
    elsif ($kind == K_ARRAY) {
      if (ref $value eq 'ARRAY') {
        $self->_walk($doc, $value->[$_], "$base/$_", $resource, 0) for 0 .. $#$value;
      }
    }
    elsif ($kind == K_MAP) {
      if (ref $value eq 'HASH') {
        for my $entry (JSON::Schema::Corvus::Json::names($value)) {
          my $v = $value->{$entry};
          $self->_walk($doc, $v, $base . '/' . escape_pointer_token($entry), $resource, 0) if _is_schema_value($v);
        }
      }
    }
  }
}

1;
