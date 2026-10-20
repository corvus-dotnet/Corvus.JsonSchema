package JSON::Schema::Corvus::Uri;

# URI handling for schema identification and reference resolution (RFC 3986 section 5). A port of the Python
# package's uri.py, which mirrors Corvus.Text.Json.RuntimeEvaluator.Compilation.UriUtilities but resolves references
# directly, so that opaque bases (urn:, tag:) resolve the same way everywhere.

use 5.026;
use strict;
use warnings;
use Encode ();
use JSON::Schema::Corvus::Json ();

my $URI_RE = qr/\A(?:([A-Za-z][A-Za-z0-9+.-]*):)?(?:\/\/([^\/?#]*))?([^?#]*)(?:\?([^#]*))?(?:#(.*))?\z/s;

sub _parse {
  my ($uri) = @_;
  $uri =~ $URI_RE;
  return { scheme => $1, authority => $2, path => $3 // '', query => $4, fragment => $5 };
}

sub _format {
  my ($p) = @_;
  my $s = '';
  $s .= "$p->{scheme}:" if defined $p->{scheme};
  $s .= "//$p->{authority}" if defined $p->{authority};
  $s .= $p->{path};
  $s .= "?$p->{query}" if defined $p->{query};
  $s .= "#$p->{fragment}" if defined $p->{fragment};
  return $s;
}

sub _remove_dot_segments {
  my ($path) = @_;
  return $path if index($path, '.') < 0;
  my @segments = split m{/}, $path, -1;
  my @out;
  my $last = $#segments;
  for my $i (0 .. $last) {
    my $seg = $segments[$i];
    if ($seg eq '.') {
      push @out, '' if $i == $last;
      next;
    }
    if ($seg eq '..') {
      pop @out if @out > 1 || (@out == 1 && $out[0] ne '');
      push @out, '' if $i == $last;
      next;
    }
    push @out, $seg;
  }
  return join '/', @out;
}

sub _merge {
  my ($base, $ref_path) = @_;
  return "/$ref_path" if defined $base->{authority} && $base->{path} eq '';
  my $i = rindex($base->{path}, '/');
  return $i >= 0 ? substr($base->{path}, 0, $i + 1) . $ref_path : $ref_path;
}

# Whether a reference starts with a URI scheme.
sub has_scheme { $_[0] =~ /\A[A-Za-z][A-Za-z0-9+.-]*:/ }

# A reference split at its first '#': (the URI, the fragment).
sub split_fragment {
  my ($reference) = @_;
  my $i = index($reference, '#');
  return $i < 0 ? ($reference, '') : (substr($reference, 0, $i), substr($reference, $i + 1));
}

sub _normalize_parts {
  my ($p) = @_;
  my %n = (%$p, fragment => undef);
  $n{scheme} = lc $n{scheme} if defined $n{scheme};
  if (defined $n{authority}) {
    $n{authority} = lc $n{authority};
    if ((($n{scheme} // '') eq 'http' && $n{authority} =~ /:80\z/)
      || (($n{scheme} // '') eq 'https' && $n{authority} =~ /:443\z/)) {
      $n{authority} = substr($n{authority}, 0, rindex($n{authority}, ':'));
    }
    $n{path} = '/' if $n{path} eq '';
  }
  $n{path} = _remove_dot_segments($n{path});
  return _format(\%n);
}

# An absolute URI (without its fragment), normalised so that equivalent spellings compare equal.
sub normalize {
  my ($uri) = @_;
  my ($part) = split_fragment($uri);
  return $part unless has_scheme($part);
  return _normalize_parts(_parse($part));
}

# A reference (without fragment) resolved against a base URI: the normalised absolute URI.
sub resolve {
  my ($base_uri, $reference) = @_;
  return $base_uri if $reference eq '';
  my $r = _parse($reference);
  return _normalize_parts($r) if defined $r->{scheme};
  return $reference if $base_uri eq '';
  my $b = _parse($base_uri);
  my %t = (path => '');
  if (defined $r->{authority}) {
    $t{authority} = $r->{authority};
    $t{path} = _remove_dot_segments($r->{path});
    $t{query} = $r->{query};
  }
  else {
    if ($r->{path} eq '') {
      $t{path} = $b->{path};
      $t{query} = $r->{query} // $b->{query};
    }
    else {
      $t{path} = _remove_dot_segments(substr($r->{path}, 0, 1) eq '/' ? $r->{path} : _merge($b, $r->{path}));
      $t{query} = $r->{query};
    }
    $t{authority} = $b->{authority};
  }
  $t{scheme} = $b->{scheme};
  return defined $t{scheme} ? _normalize_parts(\%t) : _format(\%t);
}

# A fragment, percent-decoded (as UTF-8); unchanged when that is not valid.
sub decode_fragment {
  my ($fragment) = @_;
  return $fragment if index($fragment, '%') < 0;
  (my $bytes = $fragment) =~ s/%([0-9A-Fa-f]{2})/chr hex $1/ge;
  utf8::encode(my $raw = $fragment);
  # Only the escapes are bytes: the rest of the fragment is characters, encoded as UTF-8 around them.
  my $encoded = join '', map { /\A%([0-9A-Fa-f]{2})\z/ ? chr hex $1 : do { my $c = $_; utf8::encode($c); $c } }
    split /(%[0-9A-Fa-f]{2})/, $fragment;
  my $decoded = eval { Encode::decode('UTF-8', $encoded, Encode::FB_CROAK) };
  return defined $decoded ? $decoded : $fragment;
}

# An RFC 6901 JSON pointer resolved against a JSON value: (found, value, path), the path's steps names and indexes.
sub resolve_pointer {
  my ($root, $pointer) = @_;
  return (1, $root, []) if $pointer eq '';
  return (0, undef, []) if substr($pointer, 0, 1) ne '/';
  my $current = $root;
  my @path;
  for my $raw (split m{/}, substr($pointer, 1), -1) {
    (my $token = $raw) =~ s/~1/\//g;
    $token =~ s/~0/~/g;
    my $r = ref $current;
    if ($r eq 'ARRAY') {
      return (0, undef, \@path) unless $token =~ /\A(?:0|[1-9][0-9]*)\z/ && $token < @$current;
      $current = $current->[$token];
      push @path, 0 + $token;
    }
    elsif ($r eq 'HASH') {
      return (0, undef, \@path) unless exists $current->{$token};
      $current = $current->{$token};
      push @path, $token;
    }
    else {
      return (0, undef, \@path);
    }
  }
  return (1, $current, \@path);
}

sub escape_pointer_token {
  my ($token) = @_;
  return $token if index($token, '~') < 0 && index($token, '/') < 0;
  $token =~ s/~/~0/g;
  $token =~ s/\//~1/g;
  return $token;
}

1;
