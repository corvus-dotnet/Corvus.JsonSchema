/**
 * HTTP / operation methods a generated request can carry. Mirrors the C# `OperationMethod`
 * (Corvus.Text.Json.OpenApi/OperationMethod.cs): the standard HTTP verbs, the AsyncAPI
 * publish/subscribe pair, plus `Custom` for OpenAPI 3.2 `additionalOperations`.
 */
export enum OperationMethod {
  Get,
  Put,
  Post,
  Delete,
  Options,
  Head,
  Patch,
  Trace,
  Query,
  Publish,
  Subscribe,
  Custom,
}
