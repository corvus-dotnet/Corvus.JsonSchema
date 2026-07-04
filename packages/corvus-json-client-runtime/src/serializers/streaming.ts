/**
 * Streaming response decomposition — NDJSON (`application/x-ndjson`) and Server-Sent Events
 * (`text/event-stream`). A generated streaming response keeps its raw {@link ReadableStream} and
 * exposes `async *enumerate{Status}Items()` / `enumerate{Status}SseItems()` accessors backed by these
 * readers; each yields lazily as the stream arrives (the TypeScript analogue of the C#
 * `JsonStreamReader.ReadItemsAsync` / `ReadSseItemsAsync` over `IAsyncEnumerable`).
 */

/** A parsed Server-Sent Event, carrying the decoded data plus the optional event metadata fields. */
export interface SseEvent<T> {
  /** The event payload, parsed from the joined `data:` lines as JSON. */
  readonly data: T;

  /** The `event:` field (event type), when present. */
  readonly event?: string;

  /** The `id:` field (last event id), when present. */
  readonly id?: string;

  /** The `retry:` field (reconnection time in ms), when present. */
  readonly retry?: number;
}

/**
 * Decodes a byte stream into UTF-8 text lines, splitting on `\n` and tolerating `\r\n`. Buffers across
 * chunk boundaries so a line split across two reads is reassembled; a final unterminated line is
 * yielded at end-of-stream.
 */
async function* readLines(
  stream: ReadableStream<Uint8Array>,
  signal?: AbortSignal,
): AsyncGenerator<string> {
  const reader = stream.getReader();
  const decoder = new TextDecoder();
  let buffer = "";
  try {
    for (;;) {
      if (signal?.aborted) {
        throw signal.reason ?? new Error("The streaming read was aborted.");
      }

      const { done, value } = await reader.read();
      if (done) {
        break;
      }

      if (value !== undefined) {
        buffer += decoder.decode(value, { stream: true });
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

    buffer += decoder.decode();
    if (buffer.length > 0) {
      yield buffer.endsWith("\r") ? buffer.slice(0, -1) : buffer;
    }
  } finally {
    reader.releaseLock();
  }
}

/** Yields each non-empty line of an `application/x-ndjson` stream, parsed as a JSON value. */
export async function* readNdjsonItems<T>(
  stream: ReadableStream<Uint8Array>,
  signal?: AbortSignal,
): AsyncGenerator<T> {
  for await (const line of readLines(stream, signal)) {
    const trimmed = line.trim();
    if (trimmed.length === 0) {
      continue;
    }

    yield JSON.parse(trimmed) as T;
  }
}

/**
 * Parses a `text/event-stream` into {@link SseEvent}s. Lines are buffered into a frame; a blank line
 * dispatches the frame (its joined `data:` lines parsed as JSON). Comment lines (`:`-prefixed) are
 * ignored; a trailing frame with no terminating blank line is dispatched at end-of-stream.
 */
export async function* readSseEvents<T>(
  stream: ReadableStream<Uint8Array>,
  signal?: AbortSignal,
): AsyncGenerator<SseEvent<T>> {
  let dataLines: string[] = [];
  let event: string | undefined;
  let id: string | undefined;
  let retry: number | undefined;

  const dispatch = (): SseEvent<T> | undefined => {
    if (dataLines.length === 0) {
      return undefined;
    }

    const data = JSON.parse(dataLines.join("\n")) as T;
    const result: SseEvent<T> = {
      data,
      ...(event !== undefined ? { event } : {}),
      ...(id !== undefined ? { id } : {}),
      ...(retry !== undefined ? { retry } : {}),
    };
    dataLines = [];
    event = undefined;
    id = undefined;
    retry = undefined;
    return result;
  };

  for await (const line of readLines(stream, signal)) {
    if (line.length === 0) {
      const dispatched = dispatch();
      if (dispatched !== undefined) {
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
  if (trailing !== undefined) {
    yield trailing;
  }
}

/** Yields just the parsed `data` payloads of a `text/event-stream`, dropping the event metadata. */
export async function* readSseItems<T>(
  stream: ReadableStream<Uint8Array>,
  signal?: AbortSignal,
): AsyncGenerator<T> {
  for await (const sseEvent of readSseEvents<T>(stream, signal)) {
    yield sseEvent.data;
  }
}
