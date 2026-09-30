//! URI handling for schema identification and reference resolution (RFC 3986 section 5).
//!
//! Mirrors the TypeScript port's `uri.ts`: reference resolution is implemented directly so that opaque bases
//! (`urn:`, `tag:`) resolve the same way everywhere.

use serde_json::Value;

struct UriParts<'a> {
    scheme: Option<&'a str>,
    authority: Option<&'a str>,
    path: &'a str,
    query: Option<&'a str>,
}

/// Splits a URI into scheme, authority, path and query (the fragment must already be removed).
fn parse(uri: &str) -> UriParts<'_> {
    let mut rest = uri;
    let mut scheme = None;
    if let Some(colon) = scheme_end(rest) {
        scheme = Some(&rest[..colon]);
        rest = &rest[colon + 1..];
    }
    let mut authority = None;
    if let Some(after) = rest.strip_prefix("//") {
        let end = after.find(['/', '?']).unwrap_or(after.len());
        authority = Some(&after[..end]);
        rest = &after[end..];
    }
    let (path, query) = match rest.find('?') {
        Some(q) => (&rest[..q], Some(&rest[q + 1..])),
        None => (rest, None),
    };
    UriParts { scheme, authority, path, query }
}

/// The index of the ':' ending a URI scheme, if the text starts with one.
fn scheme_end(s: &str) -> Option<usize> {
    let bytes = s.as_bytes();
    if bytes.is_empty() || !bytes[0].is_ascii_alphabetic() {
        return None;
    }
    for (i, &b) in bytes.iter().enumerate().skip(1) {
        if b == b':' {
            return Some(i);
        }
        if !(b.is_ascii_alphanumeric() || b == b'+' || b == b'-' || b == b'.') {
            return None;
        }
    }
    None
}

/// True when the reference starts with a URI scheme.
pub(crate) fn has_scheme(reference: &str) -> bool {
    scheme_end(reference).is_some()
}

/// Splits a reference at its first '#'.
pub(crate) fn split(reference: &str) -> (&str, &str) {
    match reference.find('#') {
        Some(i) => (&reference[..i], &reference[i + 1..]),
        None => (reference, ""),
    }
}

fn format(scheme: Option<&str>, authority: Option<&str>, path: &str, query: Option<&str>) -> String {
    let mut s = String::with_capacity(path.len() + 32);
    if let Some(sc) = scheme {
        s.push_str(sc);
        s.push(':');
    }
    if let Some(a) = authority {
        s.push_str("//");
        s.push_str(a);
    }
    s.push_str(path);
    if let Some(q) = query {
        s.push('?');
        s.push_str(q);
    }
    s
}

fn remove_dot_segments(path: &str) -> String {
    if !path.contains('.') {
        return path.to_string();
    }
    let input: Vec<&str> = path.split('/').collect();
    let mut out: Vec<&str> = Vec::with_capacity(input.len());
    let last = input.len() - 1;
    for (i, seg) in input.iter().enumerate() {
        match *seg {
            "." => {
                if i == last {
                    out.push("");
                }
            }
            ".." => {
                if out.len() > 1 || (out.len() == 1 && !out[0].is_empty()) {
                    out.pop();
                }
                if i == last {
                    out.push("");
                }
            }
            s => out.push(s),
        }
    }
    out.join("/")
}

fn merge(base: &UriParts<'_>, ref_path: &str) -> String {
    if base.authority.is_some() && base.path.is_empty() {
        return format!("/{ref_path}");
    }
    match base.path.rfind('/') {
        Some(i) => format!("{}{}", &base.path[..=i], ref_path),
        None => ref_path.to_string(),
    }
}

fn normalize_parts(scheme: Option<&str>, authority: Option<&str>, path: &str, query: Option<&str>) -> String {
    let scheme = scheme.map(|s| s.to_ascii_lowercase());
    let mut path = remove_dot_segments(path);
    let authority = authority.map(|a| {
        let mut a = a.to_ascii_lowercase();
        let default_port = match scheme.as_deref() {
            Some("http") => Some(":80"),
            Some("https") => Some(":443"),
            _ => None,
        };
        if let Some(port) = default_port {
            if a.ends_with(port) {
                a.truncate(a.len() - port.len());
            }
        }
        if path.is_empty() {
            path = "/".to_string();
        }
        a
    });
    format(scheme.as_deref(), authority.as_deref(), &path, query)
}

/// Normalises an absolute URI (dropping its fragment) so that equivalent spellings compare equal.
pub(crate) fn normalize(uri: &str) -> String {
    let (uri_part, _) = split(uri);
    if !has_scheme(uri_part) {
        return uri_part.to_string();
    }
    let p = parse(uri_part);
    normalize_parts(p.scheme, p.authority, p.path, p.query)
}

/// Resolves a reference (without fragment) against a base URI, returning the normalised absolute URI.
pub(crate) fn resolve(base_uri: &str, reference: &str) -> String {
    if reference.is_empty() {
        return base_uri.to_string();
    }
    let r = parse(reference);
    if r.scheme.is_some() {
        return normalize_parts(r.scheme, r.authority, r.path, r.query);
    }
    if base_uri.is_empty() {
        return reference.to_string();
    }
    let b = parse(base_uri);
    let (authority, path, query);
    if r.authority.is_some() {
        authority = r.authority;
        path = remove_dot_segments(r.path);
        query = r.query;
    } else {
        if r.path.is_empty() {
            path = b.path.to_string();
            query = if r.query.is_some() { r.query } else { b.query };
        } else {
            path = if r.path.starts_with('/') {
                remove_dot_segments(r.path)
            } else {
                remove_dot_segments(&merge(&b, r.path))
            };
            query = r.query;
        }
        authority = b.authority;
    }
    if b.scheme.is_some() {
        normalize_parts(b.scheme, authority, &path, query)
    } else {
        format(None, authority, &path, query)
    }
}

/// Percent-decodes a fragment (invalid escapes leave the text unchanged).
pub(crate) fn decode_fragment(fragment: &str) -> String {
    if !fragment.contains('%') {
        return fragment.to_string();
    }
    let bytes = fragment.as_bytes();
    let mut out = Vec::with_capacity(bytes.len());
    let mut i = 0;
    while i < bytes.len() {
        if bytes[i] == b'%' {
            let hex = |c: u8| (c as char).to_digit(16);
            match (bytes.get(i + 1).and_then(|&c| hex(c)), bytes.get(i + 2).and_then(|&c| hex(c))) {
                (Some(h), Some(l)) => {
                    out.push((h * 16 + l) as u8);
                    i += 3;
                    continue;
                }
                _ => return fragment.to_string(),
            }
        }
        out.push(bytes[i]);
        i += 1;
    }
    String::from_utf8(out).unwrap_or_else(|_| fragment.to_string())
}

/// Escapes a JSON pointer token (`~` as `~0`, `/` as `~1`).
pub(crate) fn escape_pointer_token(token: &str) -> std::borrow::Cow<'_, str> {
    if !token.contains(['~', '/']) {
        return std::borrow::Cow::Borrowed(token);
    }
    std::borrow::Cow::Owned(token.replace('~', "~0").replace('/', "~1"))
}

/// Resolves an RFC 6901 JSON pointer against a JSON value, returning the value and its normalised pointer.
pub(crate) fn resolve_pointer<'a>(root: &'a Value, pointer: &str) -> Option<(&'a Value, String)> {
    if pointer.is_empty() {
        return Some((root, String::new()));
    }
    let rest = pointer.strip_prefix('/')?;
    let mut current = root;
    let mut path = String::with_capacity(pointer.len());
    for raw in rest.split('/') {
        let token = raw.replace("~1", "/").replace("~0", "~");
        match current {
            Value::Array(items) => {
                let valid = token == "0"
                    || (!token.is_empty() && !token.starts_with('0') && token.bytes().all(|b| b.is_ascii_digit()));
                if !valid {
                    return None;
                }
                let i: usize = token.parse().ok()?;
                current = items.get(i)?;
                path.push('/');
                path.push_str(&token);
            }
            Value::Object(map) => {
                current = map.get(&token)?;
                path.push('/');
                path.push_str(&escape_pointer_token(&token));
            }
            _ => return None,
        }
    }
    Some((current, path))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn resolves_relative_references() {
        assert_eq!(resolve("http://example.com/a/b.json", "c.json"), "http://example.com/a/c.json");
        assert_eq!(resolve("http://example.com/a/b.json", "../c.json"), "http://example.com/c.json");
        assert_eq!(resolve("http://Example.com:80", ""), "http://Example.com:80");
        assert_eq!(normalize("HTTP://Example.COM:80#frag"), "http://example.com/");
        assert_eq!(resolve("urn:uuid:deadbeef-1234", ""), "urn:uuid:deadbeef-1234");
        assert_eq!(resolve("tag:example.com,2021:a", "b"), "tag:b");
    }
}
