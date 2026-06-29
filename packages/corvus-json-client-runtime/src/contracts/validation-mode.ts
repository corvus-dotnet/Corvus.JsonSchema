/**
 * Validation strictness applied by a generated request/response. Mirrors the C# `ValidationMode`
 * (Corvus.Text.Json.OpenApi/ValidationMode.cs).
 *
 * - `None` — skip validation (the response default).
 * - `Basic` — a boolean evaluate; throw on failure (the request default).
 * - `Detailed` — collect per-keyword failures for diagnostics before throwing.
 */
export enum ValidationMode {
  None = 0,
  Basic = 1,
  Detailed = 2,
}
