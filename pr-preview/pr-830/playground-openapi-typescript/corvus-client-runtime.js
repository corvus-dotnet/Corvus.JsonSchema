// packages/corvus-json-client-runtime/src/contracts/operation-method.ts
var OperationMethod = /* @__PURE__ */ ((OperationMethod2) => {
  OperationMethod2[OperationMethod2["Get"] = 0] = "Get";
  OperationMethod2[OperationMethod2["Put"] = 1] = "Put";
  OperationMethod2[OperationMethod2["Post"] = 2] = "Post";
  OperationMethod2[OperationMethod2["Delete"] = 3] = "Delete";
  OperationMethod2[OperationMethod2["Options"] = 4] = "Options";
  OperationMethod2[OperationMethod2["Head"] = 5] = "Head";
  OperationMethod2[OperationMethod2["Patch"] = 6] = "Patch";
  OperationMethod2[OperationMethod2["Trace"] = 7] = "Trace";
  OperationMethod2[OperationMethod2["Query"] = 8] = "Query";
  OperationMethod2[OperationMethod2["Publish"] = 9] = "Publish";
  OperationMethod2[OperationMethod2["Subscribe"] = 10] = "Subscribe";
  OperationMethod2[OperationMethod2["Custom"] = 11] = "Custom";
  return OperationMethod2;
})(OperationMethod || {});

// packages/corvus-json-client-runtime/src/contracts/validation-mode.ts
var ValidationMode = /* @__PURE__ */ ((ValidationMode2) => {
  ValidationMode2[ValidationMode2["None"] = 0] = "None";
  ValidationMode2[ValidationMode2["Basic"] = 1] = "Basic";
  ValidationMode2[ValidationMode2["Detailed"] = 2] = "Detailed";
  return ValidationMode2;
})(ValidationMode || {});

// packages/corvus-json-client-runtime/src/serializers/buffer-writer.ts
var ByteWriter = class _ByteWriter {
  static encoder = new TextEncoder();
  buffer;
  length = 0;
  /**
   * Initializes a new writer.
   * @param initialCapacity The initial backing-buffer size in bytes.
   */
  constructor(initialCapacity = 256) {
    this.buffer = new Uint8Array(initialCapacity);
  }
  /** The number of bytes written so far. */
  get writtenCount() {
    return this.length;
  }
  /** A view (no copy) over the bytes written so far; valid until the next write or {@link reset}. */
  get written() {
    return this.buffer.subarray(0, this.length);
  }
  /** Appends raw bytes. */
  writeBytes(bytes) {
    this.ensure(bytes.length);
    this.buffer.set(bytes, this.length);
    this.length += bytes.length;
  }
  /** Appends an ASCII literal; callers guarantee code points below 128 (e.g. the separators "&", "=", "?"). */
  writeAscii(s) {
    this.ensure(s.length);
    for (let i = 0; i < s.length; i++) {
      this.buffer[this.length++] = s.charCodeAt(i);
    }
  }
  /** Appends the UTF-8 encoding of an arbitrary string. */
  writeUtf8(s) {
    this.writeBytes(_ByteWriter.encoder.encode(s));
  }
  /** Resets the writer for reuse without releasing the backing buffer. */
  reset() {
    this.length = 0;
  }
  ensure(extra) {
    const needed = this.length + extra;
    if (needed <= this.buffer.length) {
      return;
    }
    let capacity = this.buffer.length * 2;
    while (capacity < needed) {
      capacity *= 2;
    }
    const grown = new Uint8Array(capacity);
    grown.set(this.buffer.subarray(0, this.length));
    this.buffer = grown;
  }
};

// packages/corvus-json-client-runtime/src/serializers/percent-encoding.ts
var HEX = "0123456789ABCDEF";
var encoder = new TextEncoder();
var UNRESERVED = buildSet("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~");
var RESERVED = buildSet(":/?#[]@!$&'()*+,;=");
function buildSet(chars) {
  const set = new Uint8Array(128);
  for (let i = 0; i < chars.length; i++) {
    set[chars.charCodeAt(i)] = 1;
  }
  return set;
}
function encode(value, allowReserved) {
  const bytes = encoder.encode(value);
  let out = "";
  for (let i = 0; i < bytes.length; i++) {
    const b = bytes[i];
    if (b < 128 && (UNRESERVED[b] === 1 || allowReserved && RESERVED[b] === 1)) {
      out += String.fromCharCode(b);
    } else {
      out += "%" + HEX[b >> 4] + HEX[b & 15];
    }
  }
  return out;
}
function encodeData(value) {
  return encode(value, false);
}
function encodeAllowReserved(value) {
  return encode(value, true);
}

// packages/corvus-json-client-runtime/src/serializers/style.ts
function scalarText(value) {
  return typeof value === "string" ? value : String(value);
}
function isArrayValue(value) {
  return Array.isArray(value);
}
function isObject(value) {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}
function encodeValue(text, allowReserved) {
  return allowReserved ? encodeAllowReserved(text) : encodeData(text);
}
function writePathParam(w, name, value, style, explode, allowReserved = false) {
  if (isArrayValue(value)) {
    writePathArray(w, name, value, style, explode, allowReserved);
    return;
  }
  if (isObject(value)) {
    writePathObject(w, name, value, style, explode, allowReserved);
    return;
  }
  writePathPrefix(w, name, style);
  w.writeAscii(encodeValue(scalarText(value), allowReserved));
}
function writePathPrefix(w, name, style) {
  if (style === "label") {
    w.writeAscii(".");
  } else if (style === "matrix") {
    w.writeAscii(";");
    w.writeAscii(encodeData(name));
    w.writeAscii("=");
  }
}
function writePathArray(w, name, value, style, explode, allowReserved) {
  const encodedName = encodeData(name);
  if (style === "label") {
    w.writeAscii(".");
  } else if (style === "matrix" && !explode) {
    w.writeAscii(";");
    w.writeAscii(encodedName);
    w.writeAscii("=");
  } else if (style === "matrix") {
    w.writeAscii(";");
  }
  if (style === "matrix" && explode) {
    w.writeAscii(encodedName);
    w.writeAscii("=");
  }
  const separator = style === "label" && explode ? "." : style === "matrix" && explode ? `;${encodedName}=` : ",";
  let first = true;
  for (const item of value) {
    if (!first) {
      w.writeAscii(separator);
    }
    w.writeAscii(encodeValue(scalarText(item), allowReserved));
    first = false;
  }
}
function writePathObject(w, name, value, style, explode, allowReserved) {
  const encodedName = encodeData(name);
  if (style === "label") {
    w.writeAscii(".");
  } else if (style === "matrix" && !explode) {
    w.writeAscii(";");
    w.writeAscii(encodedName);
    w.writeAscii("=");
  } else if (style === "matrix") {
    w.writeAscii(";");
  }
  const separator = style === "label" && explode ? "." : style === "matrix" && explode ? ";" : ",";
  const kvSeparator = explode ? "=" : ",";
  let first = true;
  for (const key of Object.keys(value)) {
    if (!first) {
      w.writeAscii(separator);
    }
    w.writeAscii(encodeData(key));
    w.writeAscii(kvSeparator);
    w.writeAscii(encodeValue(scalarText(value[key]), allowReserved));
    first = false;
  }
}
function writeQueryParam(w, name, value, style, explode, allowReserved, first) {
  if (isArrayValue(value)) {
    return writeQueryArray(w, name, value, style, explode, allowReserved, first);
  }
  if (isObject(value)) {
    return writeQueryObject(w, name, value, style, explode, allowReserved, first);
  }
  if (!first) {
    w.writeAscii("&");
  }
  w.writeAscii(encodeData(name));
  w.writeAscii("=");
  w.writeAscii(encodeValue(scalarText(value), allowReserved));
  return 1;
}
function writeQueryArray(w, name, value, style, explode, allowReserved, first) {
  if (value.length === 0) {
    return 0;
  }
  const encodedName = encodeData(name);
  if (style === "form" && explode) {
    let pairs = 0;
    for (const item of value) {
      if (!first || pairs > 0) {
        w.writeAscii("&");
      }
      w.writeAscii(encodedName);
      w.writeAscii("=");
      w.writeAscii(encodeValue(scalarText(item), allowReserved));
      pairs++;
    }
    return pairs;
  }
  const itemSep = style === "spaceDelimited" ? "%20" : style === "pipeDelimited" ? "%7C" : ",";
  if (!first) {
    w.writeAscii("&");
  }
  w.writeAscii(encodedName);
  w.writeAscii("=");
  let firstItem = true;
  for (const item of value) {
    if (!firstItem) {
      w.writeAscii(itemSep);
    }
    w.writeAscii(encodeValue(scalarText(item), allowReserved));
    firstItem = false;
  }
  return 1;
}
function writeQueryObject(w, name, value, style, explode, allowReserved, first) {
  const keys = Object.keys(value);
  if (keys.length === 0) {
    return 0;
  }
  const encodedName = encodeData(name);
  if (style === "deepObject") {
    let pairs = 0;
    for (const key of keys) {
      if (!first || pairs > 0) {
        w.writeAscii("&");
      }
      w.writeAscii(encodedName);
      w.writeAscii("%5B");
      w.writeAscii(encodeData(key));
      w.writeAscii("%5D=");
      w.writeAscii(encodeValue(scalarText(value[key]), allowReserved));
      pairs++;
    }
    return pairs;
  }
  if (explode) {
    let pairs = 0;
    for (const key of keys) {
      if (!first || pairs > 0) {
        w.writeAscii("&");
      }
      w.writeAscii(encodeData(key));
      w.writeAscii("=");
      w.writeAscii(encodeValue(scalarText(value[key]), allowReserved));
      pairs++;
    }
    return pairs;
  }
  const kvSep = style === "spaceDelimited" ? "%20" : style === "pipeDelimited" ? "%7C" : ",";
  if (!first) {
    w.writeAscii("&");
  }
  w.writeAscii(encodedName);
  w.writeAscii("=");
  let firstProp = true;
  for (const key of keys) {
    if (!firstProp) {
      w.writeAscii(kvSep);
    }
    w.writeAscii(encodeData(key));
    w.writeAscii(kvSep);
    w.writeAscii(encodeValue(scalarText(value[key]), allowReserved));
    firstProp = false;
  }
  return 1;
}
function writeHeaderParam(value, explode) {
  if (isArrayValue(value)) {
    return value.map((item) => scalarText(item)).join(",");
  }
  if (isObject(value)) {
    const kvSep = explode ? "=" : ",";
    return Object.keys(value).map((key) => `${key}${kvSep}${scalarText(value[key])}`).join(",");
  }
  return scalarText(value);
}
function writeCookieParam(w, name, value, style, explode, first) {
  if (isArrayValue(value)) {
    return writeCookieArray(w, name, value, explode, first);
  }
  if (isObject(value)) {
    return writeCookieObject(w, name, value, explode, first);
  }
  if (!first) {
    w.writeAscii("; ");
  }
  w.writeAscii(name);
  w.writeAscii("=");
  const text = scalarText(value);
  w.writeAscii(style === "cookie" ? text : encodeData(text));
  return 1;
}
function writeCookieArray(w, name, value, explode, first) {
  if (value.length === 0) {
    return 0;
  }
  if (explode) {
    let pairs = 0;
    for (const item of value) {
      if (!first || pairs > 0) {
        w.writeAscii("; ");
      }
      w.writeAscii(name);
      w.writeAscii("=");
      w.writeAscii(scalarText(item));
      pairs++;
    }
    return pairs;
  }
  if (!first) {
    w.writeAscii("; ");
  }
  w.writeAscii(name);
  w.writeAscii("=");
  let firstItem = true;
  for (const item of value) {
    if (!firstItem) {
      w.writeAscii(",");
    }
    w.writeAscii(scalarText(item));
    firstItem = false;
  }
  return 1;
}
function writeCookieObject(w, name, value, explode, first) {
  const keys = Object.keys(value);
  if (keys.length === 0) {
    return 0;
  }
  if (explode) {
    let pairs = 0;
    for (const key of keys) {
      if (!first || pairs > 0) {
        w.writeAscii("; ");
      }
      w.writeAscii(key);
      w.writeAscii("=");
      w.writeAscii(scalarText(value[key]));
      pairs++;
    }
    return pairs;
  }
  if (!first) {
    w.writeAscii("; ");
  }
  w.writeAscii(name);
  w.writeAscii("=");
  let firstProp = true;
  for (const key of keys) {
    if (!firstProp) {
      w.writeAscii(",");
    }
    w.writeAscii(key);
    w.writeAscii(",");
    w.writeAscii(scalarText(value[key]));
    firstProp = false;
  }
  return 1;
}

// packages/corvus-json-client-runtime/src/serializers/form-urlencoded.ts
function writeFormUrlEncoded(w, value, encodings) {
  let first = true;
  for (const name of Object.keys(value)) {
    const propValue = value[name];
    if (propValue === void 0) {
      continue;
    }
    const enc = encodings?.[name];
    const style = enc?.style ?? "form";
    const explode = enc?.explode ?? style === "form";
    const allowReserved = enc?.allowReserved ?? false;
    if (propValue === null) {
      if (!first) {
        w.writeAscii("&");
      }
      w.writeAscii(encodeData(name));
      w.writeAscii("=");
      first = false;
      continue;
    }
    const pairs = writeQueryParam(w, name, propValue, style, explode, allowReserved, first);
    if (pairs > 0) {
      first = false;
    }
  }
}
function formUrlEncodedBytes(value, encodings) {
  const w = new ByteWriter();
  writeFormUrlEncoded(w, value, encodings);
  return w.written.slice();
}

// packages/corvus-json-client-runtime/src/serializers/header.ts
function parseHeaderString(raw) {
  return raw;
}
function parseHeaderNumber(raw) {
  return Number(raw);
}
function parseHeaderBoolean(raw) {
  return raw === "true";
}
function parseHeaderArray(raw, parseElement) {
  if (raw.length === 0) {
    return [];
  }
  return raw.split(",").map((element) => parseElement(element.trim()));
}
function parseHeaderObject(raw, explode) {
  const out = {};
  if (raw.length === 0) {
    return out;
  }
  const parts = raw.split(",");
  if (explode) {
    for (const part of parts) {
      const eq = part.indexOf("=");
      if (eq >= 0) {
        out[part.slice(0, eq).trim()] = part.slice(eq + 1).trim();
      }
    }
  } else {
    for (let i = 0; i + 1 < parts.length; i += 2) {
      out[parts[i].trim()] = parts[i + 1].trim();
    }
  }
  return out;
}

// packages/corvus-json-client-runtime/src/serializers/json-pointer.ts
function getByPointer(root, pointer) {
  if (pointer.length === 0) {
    return root;
  }
  let current = root;
  for (const rawSegment of pointer.split("/").slice(1)) {
    if (current === null || typeof current !== "object") {
      return void 0;
    }
    const segment = rawSegment.replace(/~1/g, "/").replace(/~0/g, "~");
    current = current[segment];
  }
  return current;
}

// packages/corvus-json-client-runtime/src/serializers/streaming.ts
async function* readLines(stream, signal) {
  const reader = stream.getReader();
  const decoder2 = new TextDecoder();
  let buffer = "";
  try {
    for (; ; ) {
      if (signal?.aborted) {
        throw signal.reason ?? new Error("The streaming read was aborted.");
      }
      const { done, value } = await reader.read();
      if (done) {
        break;
      }
      if (value !== void 0) {
        buffer += decoder2.decode(value, { stream: true });
        let newline = buffer.indexOf("\n");
        while (newline >= 0) {
          let line = buffer.slice(0, newline);
          buffer = buffer.slice(newline + 1);
          if (line.endsWith("\r")) {
            line = line.slice(0, -1);
          }
          yield line;
          newline = buffer.indexOf("\n");
        }
      }
    }
    buffer += decoder2.decode();
    if (buffer.length > 0) {
      yield buffer.endsWith("\r") ? buffer.slice(0, -1) : buffer;
    }
  } finally {
    reader.releaseLock();
  }
}
async function* readNdjsonItems(stream, signal) {
  for await (const line of readLines(stream, signal)) {
    const trimmed = line.trim();
    if (trimmed.length === 0) {
      continue;
    }
    yield JSON.parse(trimmed);
  }
}
async function* readSseEvents(stream, signal) {
  let dataLines = [];
  let event;
  let id;
  let retry;
  const dispatch = () => {
    if (dataLines.length === 0) {
      return void 0;
    }
    const data = JSON.parse(dataLines.join("\n"));
    const result = {
      data,
      ...event !== void 0 ? { event } : {},
      ...id !== void 0 ? { id } : {},
      ...retry !== void 0 ? { retry } : {}
    };
    dataLines = [];
    event = void 0;
    id = void 0;
    retry = void 0;
    return result;
  };
  for await (const line of readLines(stream, signal)) {
    if (line.length === 0) {
      const dispatched = dispatch();
      if (dispatched !== void 0) {
        yield dispatched;
      }
      continue;
    }
    if (line.startsWith(":")) {
      continue;
    }
    const colon = line.indexOf(":");
    const field = colon >= 0 ? line.slice(0, colon) : line;
    let fieldValue = colon >= 0 ? line.slice(colon + 1) : "";
    if (fieldValue.startsWith(" ")) {
      fieldValue = fieldValue.slice(1);
    }
    switch (field) {
      case "data":
        dataLines.push(fieldValue);
        break;
      case "event":
        event = fieldValue;
        break;
      case "id":
        id = fieldValue;
        break;
      case "retry": {
        const parsed = Number(fieldValue);
        if (!Number.isNaN(parsed)) {
          retry = parsed;
        }
        break;
      }
      default:
        break;
    }
  }
  const trailing = dispatch();
  if (trailing !== void 0) {
    yield trailing;
  }
}
async function* readSseItems(stream, signal) {
  for await (const sseEvent of readSseEvents(stream, signal)) {
    yield sseEvent.data;
  }
}

// packages/corvus-json-client-runtime/src/serializers/multipart.ts
var CRLF = "\r\n";
function randomBoundaryToken(prefix) {
  return prefix + crypto.randomUUID().replace(/-/g, "");
}
function writeContentTypeLine(w, contentType) {
  w.writeAscii("Content-Type: ");
  w.writeUtf8(contentType);
  w.writeAscii(CRLF);
}
function writeFormField(w, boundary, name, value, contentTypeOverride) {
  w.writeAscii("--");
  w.writeUtf8(boundary);
  w.writeAscii(CRLF);
  w.writeAscii('Content-Disposition: form-data; name="');
  w.writeUtf8(name);
  w.writeAscii('"');
  w.writeAscii(CRLF);
  if (value === null) {
    if (contentTypeOverride !== void 0) {
      writeContentTypeLine(w, contentTypeOverride);
    }
    w.writeAscii(CRLF);
  } else if (typeof value === "object") {
    writeContentTypeLine(w, contentTypeOverride ?? "application/json");
    w.writeAscii(CRLF);
    w.writeUtf8(JSON.stringify(value));
  } else {
    if (contentTypeOverride !== void 0) {
      writeContentTypeLine(w, contentTypeOverride);
    }
    w.writeAscii(CRLF);
    w.writeUtf8(typeof value === "string" ? value : String(value));
  }
  w.writeAscii(CRLF);
}
function writeFormBinaryPart(w, boundary, name, part) {
  w.writeAscii("--");
  w.writeUtf8(boundary);
  w.writeAscii(CRLF);
  w.writeAscii('Content-Disposition: form-data; name="');
  w.writeUtf8(name);
  w.writeAscii('"');
  if (part.filename !== void 0) {
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
function multipartFormData(fields, binaryParts, options) {
  const boundary = options?.boundary ?? randomBoundaryToken("");
  const contentTypes = options?.fieldContentTypes;
  return {
    kind: "writer",
    contentType: `multipart/form-data; boundary=${boundary}`,
    async write(sink, signal) {
      const w = new ByteWriter();
      for (const name of Object.keys(fields)) {
        const value = fields[name];
        if (value === void 0) {
          continue;
        }
        writeFormField(w, boundary, name, value, contentTypes?.[name]);
      }
      if (binaryParts !== void 0) {
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
    }
  };
}
function multipartMixed(parts, options) {
  const boundary = options?.boundary ?? randomBoundaryToken("----CorvusBoundary");
  return {
    kind: "writer",
    contentType: `multipart/mixed; boundary=${boundary}`,
    async write(sink, signal) {
      const w = new ByteWriter();
      for (const part of parts) {
        w.writeAscii("--");
        w.writeUtf8(boundary);
        w.writeAscii(CRLF);
        if (part.kind === "binary") {
          writeContentTypeLine(w, part.contentType ?? "application/octet-stream");
          if (part.filename !== void 0) {
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
    }
  };
}

// packages/corvus-json-client-runtime/src/auth/bearer.ts
function bearerToken(tokenOrFactory) {
  if (typeof tokenOrFactory === "string") {
    const header = `Bearer ${tokenOrFactory}`;
    return {
      authenticate(request) {
        request.headers.set("Authorization", header);
      }
    };
  }
  return {
    async authenticate(request, signal) {
      request.headers.set("Authorization", `Bearer ${await tokenOrFactory(signal)}`);
    }
  };
}

// packages/corvus-json-client-runtime/src/auth/api-key.ts
function apiKey(value, parameterName, location) {
  return {
    authenticate(request) {
      switch (location) {
        case "header":
          request.headers.set(parameterName, value);
          break;
        case "query": {
          const separator = request.url.includes("?") ? "&" : "?";
          request.url += `${separator}${encodeData(parameterName)}=${encodeData(value)}`;
          break;
        }
        case "cookie": {
          const existing = request.headers.get("Cookie");
          const cookie = `${parameterName}=${value}`;
          request.headers.set("Cookie", existing ? `${existing}; ${cookie}` : cookie);
          break;
        }
      }
    }
  };
}

// packages/corvus-json-client-runtime/src/auth/basic.ts
var encoder2 = new TextEncoder();
function base64Utf8(text) {
  const bytes = encoder2.encode(text);
  let binary = "";
  for (let i = 0; i < bytes.length; i++) {
    binary += String.fromCharCode(bytes[i]);
  }
  return btoa(binary);
}
function basicAuth(username, password) {
  const header = `Basic ${base64Utf8(`${username}:${password}`)}`;
  return {
    authenticate(request) {
      request.headers.set("Authorization", header);
    }
  };
}

// packages/corvus-json-client-runtime/src/middleware/handler.ts
function compose(handlers, terminal) {
  return handlers.reduceRight(
    (next, handler) => (request) => handler(request, next),
    terminal
  );
}

// packages/corvus-json-client-runtime/src/middleware/retry.ts
function retryHandler(options) {
  const maxRetries = options?.maxRetries ?? 3;
  const baseDelayMs = options?.baseDelayMs ?? 500;
  const maxDelayMs = options?.maxDelayMs ?? 3e4;
  const retryableStatusCodes = options?.retryableStatusCodes ?? [429, 502, 503, 504];
  const respectRetryAfter = options?.respectRetryAfter ?? true;
  return async (request, next) => {
    for (let attempt = 0; ; attempt++) {
      const res = await next(request);
      const retryable = retryableStatusCodes.includes(res.statusCode);
      const replayable = request.body.kind !== "stream";
      if (attempt >= maxRetries || !retryable || !replayable) {
        return res;
      }
      await res.body?.cancel().catch(() => {
      });
      await res.dispose?.();
      const retryAfterMs = respectRetryAfter ? parseRetryAfter(res.headers.get("Retry-After")) : null;
      const delayMs = retryAfterMs ?? Math.random() * Math.min(maxDelayMs, baseDelayMs * 2 ** attempt);
      await delay(delayMs, request.signal);
    }
  };
}
function parseRetryAfter(value) {
  if (value === null) {
    return null;
  }
  const trimmed = value.trim();
  if (/^\d+$/.test(trimmed)) {
    return Number(trimmed) * 1e3;
  }
  const date = Date.parse(trimmed);
  if (!Number.isNaN(date)) {
    return Math.max(0, date - Date.now());
  }
  return null;
}
function delay(ms, signal) {
  return new Promise((resolve, reject) => {
    if (signal.aborted) {
      reject(signal.reason instanceof Error ? signal.reason : new DOMException("Aborted", "AbortError"));
      return;
    }
    const onAbort = () => {
      clearTimeout(timer);
      reject(signal.reason instanceof Error ? signal.reason : new DOMException("Aborted", "AbortError"));
    };
    const timer = setTimeout(() => {
      signal.removeEventListener("abort", onAbort);
      resolve();
    }, ms);
    signal.addEventListener("abort", onAbort, { once: true });
  });
}

// packages/corvus-json-client-runtime/src/middleware/redirect.ts
var REDIRECT_STATUS = /* @__PURE__ */ new Set([301, 302, 303, 307, 308]);
function redirectHandler(options) {
  const maxRedirects = options?.maxRedirects ?? 20;
  return async (request, next) => {
    let current = request;
    let res = await next(current);
    let hops = 0;
    while (REDIRECT_STATUS.has(res.statusCode) && hops < maxRedirects) {
      const location = res.headers.get("Location");
      if (location === null) {
        break;
      }
      const target = new URL(location, current.url);
      await res.body?.cancel().catch(() => {
      });
      await res.dispose?.();
      const headers = new Headers(current.headers);
      let method = current.method;
      let body = current.body;
      const downgradeToGet = res.statusCode === 303 || (res.statusCode === 301 || res.statusCode === 302) && current.method === "POST";
      if (downgradeToGet) {
        method = "GET";
        body = { kind: "none" };
        headers.delete("Content-Type");
        headers.delete("Content-Length");
      }
      if (target.origin !== new URL(current.url).origin) {
        headers.delete("Authorization");
        headers.delete("Cookie");
      }
      const nextRequest = {
        method,
        url: target.toString(),
        headers,
        body,
        signal: current.signal
      };
      current = nextRequest;
      res = await next(current);
      hops++;
    }
    return res;
  };
}

// packages/corvus-json-client-runtime/src/middleware/timeout.ts
function timeoutHandler(options) {
  return (request, next) => {
    const timeout = AbortSignal.timeout(options.timeoutMs);
    const signal = AbortSignal.any([request.signal, timeout]);
    return next({ ...request, signal });
  };
}

// packages/corvus-json-client-runtime/src/middleware/defaults.ts
function defaultHandlers(options) {
  return [
    redirectHandler(options?.redirect),
    retryHandler(options?.retry),
    timeoutHandler(options?.timeout ?? { timeoutMs: 1e5 })
  ];
}

// packages/corvus-json-client-runtime/src/transports/abstract-transport.ts
var NEVER_SIGNAL = new AbortController().signal;
var decoder = new TextDecoder();
var AbstractApiTransport = class {
  /** The normalized base URL (trailing slash stripped). */
  baseUrl;
  /** The authentication provider, or `undefined` for unauthenticated requests. */
  authenticationProvider;
  /** The frozen handler chain, outermost first. */
  handlers;
  /** A reusable writer for the URI path + query. Safe to share: build-wire contains no await. */
  uriWriter = new ByteWriter(512);
  /** A reusable writer for the cookie header value. Safe to share: build-wire contains no await. */
  cookieWriter = new ByteWriter(256);
  /**
   * Initializes the transport from the shared options.
   * @param options The construction options.
   */
  constructor(options) {
    this.baseUrl = String(options.baseUrl).replace(/\/$/, "");
    if (options.authenticationProvider !== void 0) {
      this.authenticationProvider = options.authenticationProvider;
    }
    this.handlers = Object.freeze(options.handlers ? [...options.handlers] : []);
  }
  /** @inheritdoc */
  async send(request, factory, body, signal) {
    const wire = this.buildWireRequest(request, body ?? { kind: "none" }, signal ?? NEVER_SIGNAL);
    await this.authenticationProvider?.authenticate(wire, wire.signal);
    const run = compose(this.handlers, (req) => this.dispatch(req));
    const wireResponse = await run(wire);
    const context = {
      statusCode: wireResponse.statusCode,
      body: wireResponse.body,
      contentType: mediaType(wireResponse.headers),
      headers: headersAdapter(wireResponse.headers),
      transport: this,
      ...signal !== void 0 ? { signal } : {}
    };
    try {
      return await factory.create(context);
    } catch (e) {
      await wireResponse.dispose?.();
      throw e;
    }
  }
  /**
   * Releases resources owned by this transport. The default is a no-op; concrete transports override
   * when they own a connection pool or agent.
   */
  async [Symbol.asyncDispose]() {
  }
  /**
   * Builds the byte-level wire request from a generated {@link ApiRequest} — mirrors the C#
   * `BuildHttpRequest`/`BuildUri`/`MapMethod`. Runs entirely synchronously: the reusable writers are
   * reset at entry and consumed before the method returns, and a fresh {@link Headers} is allocated per
   * call so the snapshot never aliases shared state.
   * @param request The composed per-operation request.
   * @param body The request body descriptor.
   * @param signal The abort signal for this request.
   * @returns A fresh wire request snapshot.
   */
  buildWireRequest(request, body, signal) {
    const url = this.buildUrl(request);
    const headers = new Headers();
    if (request.hasHeaderParameters) {
      request.writeHeaders((name, value) => headers.append(name, value));
    }
    if (request.hasCookieParameters) {
      this.cookieWriter.reset();
      const cookieBytes = request.writeCookies(this.cookieWriter);
      if (cookieBytes > 0) {
        headers.set("Cookie", decoder.decode(this.cookieWriter.written));
      }
    }
    if (body.kind !== "none") {
      headers.set("Content-Type", body.contentType);
    }
    return {
      method: mapMethod(request),
      url,
      headers,
      body,
      signal
    };
  }
  /**
   * Composes the absolute request URL by STRING CONCATENATION — never `new URL(path, base)`, which would
   * drop a base path prefix. The base URL has its trailing slash already stripped; the resolved path (or
   * the template) begins with `/`. Mirrors the C# `BuildUri`: write the `?` optimistically and drop it
   * when the query string is empty.
   * @param request The composed per-operation request.
   * @returns The absolute request URL.
   */
  buildUrl(request) {
    const writer = this.uriWriter;
    writer.reset();
    if (request.hasPathParameters) {
      request.writeResolvedPath(writer);
    } else {
      writer.writeUtf8(request.pathTemplate);
    }
    const path = decoder.decode(writer.written);
    if (request.hasQueryParameters) {
      writer.reset();
      const queryBytes = request.writeQueryString(writer);
      if (queryBytes > 0) {
        return `${this.baseUrl}${path}?${decoder.decode(writer.written)}`;
      }
    }
    return `${this.baseUrl}${path}`;
  }
};
function mapMethod(request) {
  switch (request.method) {
    case 0 /* Get */:
      return "GET";
    case 1 /* Put */:
      return "PUT";
    case 2 /* Post */:
      return "POST";
    case 3 /* Delete */:
      return "DELETE";
    case 4 /* Options */:
      return "OPTIONS";
    case 5 /* Head */:
      return "HEAD";
    case 6 /* Patch */:
      return "PATCH";
    case 7 /* Trace */:
      return "TRACE";
    case 8 /* Query */:
      return "QUERY";
    case 11 /* Custom */: {
      const name = request.customMethodName;
      if (name === void 0 || name.length === 0) {
        throw new Error("A Custom operation method requires a non-empty customMethodName.");
      }
      return name;
    }
    case 9 /* Publish */:
    case 10 /* Subscribe */:
      throw new Error(
        `The AsyncAPI operation method ${OperationMethod[request.method]} has no HTTP wire mapping.`
      );
    default:
      throw new Error(`Unknown operation method: ${String(request.method)}.`);
  }
}
function mediaType(headers) {
  const value = headers.get("Content-Type");
  if (value === null) {
    return null;
  }
  const semicolon = value.indexOf(";");
  const media = semicolon === -1 ? value : value.slice(0, semicolon);
  return media.trim();
}
function headersAdapter(headers) {
  return {
    tryGet(name) {
      return headers.get(name) ?? void 0;
    }
  };
}

// packages/corvus-json-client-runtime/src/transports/fetch-transport.ts
var FetchApiTransport = class extends AbstractApiTransport {
  fetchImpl;
  redirect;
  /**
   * Initializes a new fetch transport.
   * @param options The construction options.
   */
  constructor(options) {
    super(options);
    this.fetchImpl = options.fetchImpl ?? globalThis.fetch.bind(globalThis);
    this.redirect = options.redirect ?? "follow";
  }
  /** @inheritdoc */
  async dispatch(wire) {
    const init = {
      method: wire.method,
      headers: wire.headers,
      signal: wire.signal,
      redirect: this.redirect
    };
    switch (wire.body.kind) {
      case "none":
        break;
      case "bytes":
        init.body = wire.body.content;
        break;
      case "stream":
        init.body = wire.body.content;
        init.duplex = "half";
        break;
      case "writer": {
        const writer = new ByteWriter();
        const sink = {
          write(bytes) {
            writer.writeBytes(bytes);
          },
          async flush() {
          }
        };
        await wire.body.write(sink, wire.signal);
        init.body = writer.written;
        break;
      }
    }
    const response = await this.fetchImpl(wire.url, init);
    return {
      statusCode: response.status,
      headers: response.headers,
      body: response.body
    };
  }
  /** @inheritdoc */
  async [Symbol.asyncDispose]() {
  }
};
export {
  AbstractApiTransport,
  ByteWriter,
  FetchApiTransport,
  OperationMethod,
  ValidationMode,
  apiKey,
  basicAuth,
  bearerToken,
  compose,
  defaultHandlers,
  encodeAllowReserved,
  encodeData,
  formUrlEncodedBytes,
  getByPointer,
  multipartFormData,
  multipartMixed,
  parseHeaderArray,
  parseHeaderBoolean,
  parseHeaderNumber,
  parseHeaderObject,
  parseHeaderString,
  readNdjsonItems,
  readSseEvents,
  readSseItems,
  redirectHandler,
  retryHandler,
  timeoutHandler,
  writeCookieParam,
  writeFormUrlEncoded,
  writeHeaderParam,
  writePathParam,
  writeQueryParam
};
