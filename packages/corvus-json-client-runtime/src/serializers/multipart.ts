/**
 * Multipart request-body serializers — the TypeScript analogue of the C# `MultipartFormDataSerializer`
 * and `MultipartMixedSerializer`. Both produce a {@link RequestBody} of kind `"writer"` (the zero-copy
 * streaming shape): the body is framed once into a {@link ByteWriter} and handed to the transport's
 * {@link ByteSink}, with the MIME boundary carried in BOTH the `contentType` and the framing.
 *
 * - {@link multipartFormData} (`multipart/form-data`, RFC 7578): one part per field, each with a
 *   `Content-Disposition: form-data; name="..."` header. Primitive fields are written as plain text,
 *   complex (array/object) fields are JSON-encoded with `Content-Type: application/json`, and binary
 *   fields carry raw bytes (+ an optional `filename`). Binary parts follow the named text/JSON fields.
 * - {@link multipartMixed} (`multipart/mixed`, OpenAPI 3.2): ordered, unnamed parts (no
 *   `Content-Disposition` for text/JSON parts) — used for the 3.2 sequential `prefixEncoding` /
 *   `itemEncoding` bodies.
 *
 * The boundary is generated once per body. Production code lets the serializer mint a random boundary;
 * tests pass a fixed `boundary` so the framed bytes are deterministic. (Either way the boundary is also
 * recoverable from the returned `contentType`, so a transport-level test can reconstruct the framing.)
 *
 * Divergences from the C# serializers, for the C#↔TS byte-parity reconciliation (workstream V):
 *  - booleans render lowercase `true`/`false` (the JS idiom) where C# `JsonElement.ToString()` renders
 *    `True`/`False`;
 *  - complex fields are encoded with `JSON.stringify` (insertion-order keys, compact) where C# uses the
 *    model's canonical `Utf8JsonWriter` output (schema-order keys).
 */

import type { RequestBody } from "../contracts/api-transport.js";
import type { ByteSink } from "./buffer-writer.js";
import { ByteWriter } from "./buffer-writer.js";

/** A binary multipart part: raw bytes plus an optional content type and filename. */
export interface MultipartBinaryPart {
  /** The raw part content. */
  readonly content: Uint8Array;

  /** The part's MIME type. Defaults to `"application/octet-stream"`. */
  readonly contentType?: string;

  /** An optional filename for the `Content-Disposition` header. */
  readonly filename?: string;
}

/** A primitive `multipart/form-data` field value. */
export type MultipartScalar = string | number | boolean | null;

/** A `multipart/form-data` field value: a primitive, or a complex value encoded as JSON. */
export type MultipartFieldValue =
  | MultipartScalar
  | readonly unknown[]
  | { readonly [key: string]: unknown };

/** The non-binary fields of a `multipart/form-data` body (an `undefined` field is omitted). */
export type MultipartFormFields = { readonly [name: string]: MultipartFieldValue | undefined };

/** Options for {@link multipartFormData}. */
export interface MultipartFormOptions {
  /** A fixed boundary (for deterministic output); a random one is minted when omitted. */
  readonly boundary?: string;

  /** Per-field `Content-Type` overrides (the OpenAPI Encoding Object `contentType`), keyed by field name. */
  readonly fieldContentTypes?: { readonly [name: string]: string };
}

/** A single `multipart/mixed` part: a JSON value or raw binary content. */
export type MultipartMixedPart =
  | { readonly kind: "json"; readonly value: unknown; readonly contentType?: string }
  | {
      readonly kind: "binary";
      readonly content: Uint8Array;
      readonly contentType?: string;
      readonly filename?: string;
    };

/** Options for {@link multipartMixed}. */
export interface MultipartMixedOptions {
  /** A fixed boundary token (for deterministic output); a random one is minted when omitted. */
  readonly boundary?: string;
}

const CRLF = "\r\n";

/** Mints a boundary token: a fixed prefix (when given) plus 32 random hex characters (a UUID, dash-free). */
function randomBoundaryToken(prefix: string): string {
  return prefix + crypto.randomUUID().replace(/-/g, "");
}

/** Writes a `Content-Type: <value>` header line (no leading CRLF). */
function writeContentTypeLine(w: ByteWriter, contentType: string): void {
  w.writeAscii("Content-Type: ");
  w.writeUtf8(contentType);
  w.writeAscii(CRLF);
}

/**
 * Frames a single `multipart/form-data` field (its boundary line, `Content-Disposition`, optional
 * `Content-Type`, blank line, and body) into the writer.
 */
function writeFormField(
  w: ByteWriter,
  boundary: string,
  name: string,
  value: MultipartFieldValue,
  contentTypeOverride: string | undefined,
): void {
  w.writeAscii("--");
  w.writeUtf8(boundary);
  w.writeAscii(CRLF);

  w.writeAscii('Content-Disposition: form-data; name="');
  w.writeUtf8(name);
  w.writeAscii('"');
  w.writeAscii(CRLF);

  if (value === null) {
    if (contentTypeOverride !== undefined) {
      writeContentTypeLine(w, contentTypeOverride);
    }

    w.writeAscii(CRLF);
  } else if (typeof value === "object") {
    // Arrays and objects are JSON-encoded (Content-Type: application/json unless overridden).
    writeContentTypeLine(w, contentTypeOverride ?? "application/json");
    w.writeAscii(CRLF);
    w.writeUtf8(JSON.stringify(value));
  } else {
    // Primitive: raw text. Strings verbatim; numbers/booleans via their JS string form.
    if (contentTypeOverride !== undefined) {
      writeContentTypeLine(w, contentTypeOverride);
    }

    w.writeAscii(CRLF);
    w.writeUtf8(typeof value === "string" ? value : String(value));
  }

  w.writeAscii(CRLF);
}

/** Frames a single binary `multipart/form-data` part into the writer. */
function writeFormBinaryPart(w: ByteWriter, boundary: string, name: string, part: MultipartBinaryPart): void {
  w.writeAscii("--");
  w.writeUtf8(boundary);
  w.writeAscii(CRLF);

  w.writeAscii('Content-Disposition: form-data; name="');
  w.writeUtf8(name);
  w.writeAscii('"');
  if (part.filename !== undefined) {
    w.writeAscii('; filename="');
    w.writeUtf8(part.filename);
    w.writeAscii('"');
  }

  w.writeAscii(CRLF);
  writeContentTypeLine(w, part.contentType ?? "application/octet-stream");
  w.writeAscii(CRLF);
  w.writeBytes(part.content);
  w.writeAscii(CRLF);
}

/**
 * Serializes a `multipart/form-data` request body — the analogue of the C#
 * `MultipartFormDataSerializer`. Named text/JSON fields (in `fields` insertion order) are framed first,
 * then the binary parts (in `binaryParts` insertion order); the message is closed with the final
 * `--boundary--` marker.
 *
 * @param fields The non-binary form fields (an `undefined` field is omitted).
 * @param binaryParts The binary file parts, keyed by field name.
 * @param options A fixed boundary and/or per-field `Content-Type` overrides.
 * @returns A `"writer"` request body whose `contentType` carries the boundary.
 */
export function multipartFormData(
  fields: MultipartFormFields,
  binaryParts?: { readonly [name: string]: MultipartBinaryPart },
  options?: MultipartFormOptions,
): RequestBody {
  const boundary = options?.boundary ?? randomBoundaryToken("");
  const contentTypes = options?.fieldContentTypes;

  return {
    kind: "writer",
    contentType: `multipart/form-data; boundary=${boundary}`,
    async write(sink: ByteSink, signal: AbortSignal): Promise<void> {
      const w = new ByteWriter();

      for (const name of Object.keys(fields)) {
        const value = fields[name];
        if (value === undefined) {
          continue;
        }

        writeFormField(w, boundary, name, value, contentTypes?.[name]);
      }

      if (binaryParts !== undefined) {
        for (const name of Object.keys(binaryParts)) {
          writeFormBinaryPart(w, boundary, name, binaryParts[name]);
        }
      }

      w.writeAscii("--");
      w.writeUtf8(boundary);
      w.writeAscii("--");
      w.writeAscii(CRLF);

      sink.write(w.written);
      await sink.flush(signal);
    },
  };
}

/**
 * Serializes a `multipart/mixed` request body (OpenAPI 3.2 sequential parts) — the analogue of the C#
 * `MultipartMixedSerializer`. Parts are framed in order as unnamed entries (text/JSON parts carry no
 * `Content-Disposition`); the message is closed with the final `--boundary--` marker.
 *
 * @param parts The ordered parts (JSON values or raw binary content).
 * @param options A fixed boundary token (one is minted when omitted).
 * @returns A `"writer"` request body whose `contentType` carries the boundary.
 */
export function multipartMixed(parts: readonly MultipartMixedPart[], options?: MultipartMixedOptions): RequestBody {
  const boundary = options?.boundary ?? randomBoundaryToken("----CorvusBoundary");

  return {
    kind: "writer",
    contentType: `multipart/mixed; boundary=${boundary}`,
    async write(sink: ByteSink, signal: AbortSignal): Promise<void> {
      const w = new ByteWriter();

      for (const part of parts) {
        w.writeAscii("--");
        w.writeUtf8(boundary);
        w.writeAscii(CRLF);

        if (part.kind === "binary") {
          writeContentTypeLine(w, part.contentType ?? "application/octet-stream");
          if (part.filename !== undefined) {
            w.writeAscii('Content-Disposition: attachment; filename="');
            w.writeUtf8(part.filename);
            w.writeAscii('"');
            w.writeAscii(CRLF);
          }

          w.writeAscii(CRLF);
          w.writeBytes(part.content);
          w.writeAscii(CRLF);
        } else {
          writeContentTypeLine(w, part.contentType ?? "application/json");
          w.writeAscii(CRLF);
          w.writeUtf8(JSON.stringify(part.value));
          w.writeAscii(CRLF);
        }
      }

      w.writeAscii("--");
      w.writeUtf8(boundary);
      w.writeAscii("--");
      w.writeAscii(CRLF);

      sink.write(w.written);
      await sink.flush(signal);
    },
  };
}
