package io.github.corvusdotnet.jsonschema;

import java.io.ByteArrayOutputStream;
import java.nio.charset.CharacterCodingException;
import java.nio.charset.CodingErrorAction;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

/**
 * URI handling for schema identification and reference resolution (RFC 3986 section 5), implemented directly so that
 * opaque bases ({@code urn:}, {@code tag:}) resolve the same way in every port.
 */
final class Uris {
    private Uris() {
    }

    private static final class Parts {
        String scheme;
        String authority;
        String path;
        String query;
    }

    /** Splits a URI into scheme, authority, path and query (the fragment must already be removed). */
    private static Parts parse(String uri) {
        Parts p = new Parts();
        String rest = uri;
        int colon = schemeEnd(rest);
        if (colon >= 0) {
            p.scheme = rest.substring(0, colon);
            rest = rest.substring(colon + 1);
        }
        if (rest.startsWith("//")) {
            String after = rest.substring(2);
            int end = after.length();
            for (int i = 0; i < after.length(); i++) {
                char c = after.charAt(i);
                if (c == '/' || c == '?') {
                    end = i;
                    break;
                }
            }
            p.authority = after.substring(0, end);
            rest = after.substring(end);
        }
        int q = rest.indexOf('?');
        if (q >= 0) {
            p.path = rest.substring(0, q);
            p.query = rest.substring(q + 1);
        } else {
            p.path = rest;
        }
        return p;
    }

    /** The index of the ':' ending a URI scheme, if the text starts with one, or -1. */
    private static int schemeEnd(String s) {
        if (s.isEmpty() || !isAsciiAlpha(s.charAt(0))) {
            return -1;
        }
        for (int i = 1; i < s.length(); i++) {
            char c = s.charAt(i);
            if (c == ':') {
                return i;
            }
            if (!(isAsciiAlpha(c) || (c >= '0' && c <= '9') || c == '+' || c == '-' || c == '.')) {
                return -1;
            }
        }
        return -1;
    }

    private static boolean isAsciiAlpha(char c) {
        return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
    }

    /** True when the reference starts with a URI scheme. */
    static boolean hasScheme(String reference) {
        return schemeEnd(reference) >= 0;
    }

    /** The part of a reference before its first '#'. */
    static String withoutFragment(String reference) {
        int i = reference.indexOf('#');
        return i >= 0 ? reference.substring(0, i) : reference;
    }

    /** The part of a reference after its first '#', or "". */
    static String fragment(String reference) {
        int i = reference.indexOf('#');
        return i >= 0 ? reference.substring(i + 1) : "";
    }

    private static String format(String scheme, String authority, String path, String query) {
        StringBuilder s = new StringBuilder(path.length() + 32);
        if (scheme != null) {
            s.append(scheme).append(':');
        }
        if (authority != null) {
            s.append("//").append(authority);
        }
        s.append(path);
        if (query != null) {
            s.append('?').append(query);
        }
        return s.toString();
    }

    private static String removeDotSegments(String path) {
        if (path.indexOf('.') < 0) {
            return path;
        }
        String[] input = path.split("/", -1);
        List<String> out = new ArrayList<>(input.length);
        int last = input.length - 1;
        for (int i = 0; i < input.length; i++) {
            String seg = input[i];
            if (seg.equals(".")) {
                if (i == last) {
                    out.add("");
                }
            } else if (seg.equals("..")) {
                if (out.size() > 1 || (out.size() == 1 && !out.get(0).isEmpty())) {
                    out.remove(out.size() - 1);
                }
                if (i == last) {
                    out.add("");
                }
            } else {
                out.add(seg);
            }
        }
        return String.join("/", out);
    }

    private static String merge(Parts base, String refPath) {
        if (base.authority != null && base.path.isEmpty()) {
            return "/" + refPath;
        }
        int i = base.path.lastIndexOf('/');
        return i >= 0 ? base.path.substring(0, i + 1) + refPath : refPath;
    }

    private static String normalizeParts(String scheme, String authority, String path, String query) {
        if (scheme != null) {
            scheme = scheme.toLowerCase(Locale.ROOT);
        }
        path = removeDotSegments(path);
        if (authority != null) {
            authority = authority.toLowerCase(Locale.ROOT);
            String defaultPort = "http".equals(scheme) ? ":80" : "https".equals(scheme) ? ":443" : null;
            if (defaultPort != null && authority.endsWith(defaultPort)) {
                authority = authority.substring(0, authority.length() - defaultPort.length());
            }
            if (path.isEmpty()) {
                path = "/";
            }
        }
        return format(scheme, authority, path, query);
    }

    /** Normalises an absolute URI (dropping its fragment) so that equivalent spellings compare equal. */
    static String normalize(String uri) {
        String part = withoutFragment(uri);
        if (!hasScheme(part)) {
            return part;
        }
        Parts p = parse(part);
        return normalizeParts(p.scheme, p.authority, p.path, p.query);
    }

    /** Resolves a reference (without fragment) against a base URI, returning the normalised absolute URI. */
    static String resolve(String baseUri, String reference) {
        if (reference.isEmpty()) {
            return baseUri;
        }
        Parts r = parse(reference);
        if (r.scheme != null) {
            return normalizeParts(r.scheme, r.authority, r.path, r.query);
        }
        if (baseUri.isEmpty()) {
            return reference;
        }
        Parts b = parse(baseUri);
        String authority;
        String path;
        String query;
        if (r.authority != null) {
            authority = r.authority;
            path = removeDotSegments(r.path);
            query = r.query;
        } else {
            if (r.path.isEmpty()) {
                path = b.path;
                query = r.query != null ? r.query : b.query;
            } else {
                path = r.path.startsWith("/") ? removeDotSegments(r.path) : removeDotSegments(merge(b, r.path));
                query = r.query;
            }
            authority = b.authority;
        }
        return b.scheme != null
                ? normalizeParts(b.scheme, authority, path, query)
                : format(null, authority, path, query);
    }

    /** Percent-decodes a fragment (invalid escapes, or bytes that are not UTF-8, leave the text unchanged). */
    static String decodeFragment(String fragment) {
        if (fragment.indexOf('%') < 0) {
            return fragment;
        }
        byte[] bytes = fragment.getBytes(StandardCharsets.UTF_8);
        ByteArrayOutputStream out = new ByteArrayOutputStream(bytes.length);
        for (int i = 0; i < bytes.length; i++) {
            if (bytes[i] == '%') {
                int h = i + 1 < bytes.length ? Character.digit(bytes[i + 1], 16) : -1;
                int l = i + 2 < bytes.length ? Character.digit(bytes[i + 2], 16) : -1;
                if (h < 0 || l < 0) {
                    return fragment;
                }
                out.write(h * 16 + l);
                i += 2;
            } else {
                out.write(bytes[i]);
            }
        }
        try {
            return StandardCharsets.UTF_8
                    .newDecoder()
                    .onMalformedInput(CodingErrorAction.REPORT)
                    .onUnmappableCharacter(CodingErrorAction.REPORT)
                    .decode(java.nio.ByteBuffer.wrap(out.toByteArray()))
                    .toString();
        } catch (CharacterCodingException e) {
            return fragment;
        }
    }

    /** Escapes a JSON pointer token ({@code ~} as {@code ~0}, {@code /} as {@code ~1}). */
    static String escapePointerToken(String token) {
        if (token.indexOf('~') < 0 && token.indexOf('/') < 0) {
            return token;
        }
        return token.replace("~", "~0").replace("/", "~1");
    }

    /** The node a JSON pointer (RFC 6901) reaches from {@code root}, and its normalised pointer, or null. */
    static Located resolvePointer(JsonDocument doc, int root, String pointer) {
        if (pointer.isEmpty()) {
            return new Located(root, "");
        }
        if (!pointer.startsWith("/")) {
            return null;
        }
        int current = root;
        StringBuilder path = new StringBuilder(pointer.length());
        for (String raw : pointer.substring(1).split("/", -1)) {
            String token = raw.replace("~1", "/").replace("~0", "~");
            int kind = doc.kind(current);
            if (kind == JsonDocument.ARRAY) {
                boolean valid = token.equals("0")
                        || (!token.isEmpty() && !token.startsWith("0") && token.chars().allMatch(c -> c >= '0' && c <= '9'));
                if (!valid || token.length() > 9) {
                    return null;
                }
                int i = Integer.parseInt(token);
                if (i >= doc.count(current)) {
                    return null;
                }
                current = doc.first(current) + i;
                path.append('/').append(token);
            } else if (kind == JsonDocument.OBJECT) {
                int v = doc.property(current, token);
                if (v < 0) {
                    return null;
                }
                current = v;
                path.append('/').append(escapePointerToken(token));
            } else {
                return null;
            }
        }
        return new Located(current, path.toString());
    }

    /** A node and the normalised pointer that reached it. */
    static final class Located {
        final int node;
        final String pointer;

        Located(int node, String pointer) {
            this.node = node;
            this.pointer = pointer;
        }
    }
}