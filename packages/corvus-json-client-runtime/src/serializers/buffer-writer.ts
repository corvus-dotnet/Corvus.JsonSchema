/**
 * The byte-buffer primitives the generated request writers and body serializers build on — the
 * TypeScript analogue of the C# `IBufferWriter<byte>` / streaming `HttpContent` writers.
 */

/**
 * A streaming sink for request bodies that cannot be (or should not be) fully buffered — the
 * analogue of the C# `Func<Stream, CancellationToken, ValueTask>` body writer.
 */
export interface ByteSink {
  /** Appends bytes to the sink. */
  write(bytes: Uint8Array): void;

  /** Flushes buffered bytes downstream; honours the supplied abort signal. */
  flush(signal: AbortSignal): Promise<void>;
}

/**
 * A growable in-memory `IBufferWriter<byte>`: generated `writeResolvedPath` / `writeQueryString` /
 * `writeCookies` and the parameter-style helpers write into one of these. Instances are reusable
 * across calls via {@link reset} to avoid per-request allocation (the thread-static reuse pattern in
 * the C# `HttpClientTransport`).
 */
export class ByteWriter {
  private static readonly encoder = new TextEncoder();
  private buffer: Uint8Array;
  private length = 0;

  /**
   * Initializes a new writer.
   * @param initialCapacity The initial backing-buffer size in bytes.
   */
  public constructor(initialCapacity = 256) {
    this.buffer = new Uint8Array(initialCapacity);
  }

  /** The number of bytes written so far. */
  public get writtenCount(): number {
    return this.length;
  }

  /** A view (no copy) over the bytes written so far; valid until the next write or {@link reset}. */
  public get written(): Uint8Array {
    return this.buffer.subarray(0, this.length);
  }

  /** Appends raw bytes. */
  public writeBytes(bytes: Uint8Array): void {
    this.ensure(bytes.length);
    this.buffer.set(bytes, this.length);
    this.length += bytes.length;
  }

  /** Appends an ASCII literal; callers guarantee code points below 128 (e.g. the separators "&", "=", "?"). */
  public writeAscii(s: string): void {
    this.ensure(s.length);
    for (let i = 0; i < s.length; i++) {
      this.buffer[this.length++] = s.charCodeAt(i);
    }
  }

  /** Appends the UTF-8 encoding of an arbitrary string. */
  public writeUtf8(s: string): void {
    this.writeBytes(ByteWriter.encoder.encode(s));
  }

  /** Resets the writer for reuse without releasing the backing buffer. */
  public reset(): void {
    this.length = 0;
  }

  private ensure(extra: number): void {
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
}
