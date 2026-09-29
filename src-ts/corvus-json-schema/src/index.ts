// Public API: compile a schema to a validator in memory, or emit it as a standalone ES module.

import { CodeGenerator, GeneratedCode } from './codegen.js';
import { CollectingProgram, evaluateWithCollector, serializeProgram } from './collecting.js';
import { CompiledSchema, SchemaCompiler } from './compiler.js';
import { JsonSchemaResultsCollector } from './results.js';
import { CompileOptions, SchemaCompilationError } from './options.js';
import * as runtime from './runtime.js';

export { Dialect } from './dialect.js';
export { JsonSchemaResultsCollector, ResultsLevel, enumerateAnnotations, collectAnnotations, schemaLocationFragment } from './results.js';
export type { SchemaResult, Annotation } from './results.js';
export { SchemaCompilationError, SchemaEvaluationDepthError } from './options.js';
export type { CompileOptions, DocumentResolver, FormatValidator } from './options.js';

/** A compiled validator: returns true when the instance is valid against the schema. */
export interface Validator {
  (instance: unknown): boolean;
  /**
   * Evaluates the instance, reporting results to the collector when one is given (every keyword is evaluated and
   * reported, at the collector's level); without a collector this is the validator itself.
   */
  evaluate(instance: unknown, collector?: JsonSchemaResultsCollector): boolean;
  /** The generated JavaScript (the body of the validator's module). */
  readonly source: string;
}

/** Options for {@link generateModule}. */
export interface ModuleOptions extends CompileOptions {
  /** The import specifier for the runtime helpers. Defaults to `@corvus-dotnet/json-schema/runtime`. */
  runtimeImport?: string;
  /**
   * Also export `evaluate(instance, collector)`, embedding the program image for results collection (imports
   * `@corvus-dotnet/json-schema/collecting`, or `collectingImport`). Defaults to true.
   */
  collecting?: boolean;
  /** The import specifier for the collecting evaluator. Defaults to `@corvus-dotnet/json-schema/collecting`. */
  collectingImport?: string;
}

function parseSchema(schema: unknown): unknown {
  return typeof schema === 'string' ? JSON.parse(schema) : schema;
}

function generate(schema: unknown, options?: CompileOptions): { g: GeneratedCode; program: CompiledSchema } {
  const program = SchemaCompiler.compile(parseSchema(schema), options);
  return { g: new CodeGenerator(program).generate(), program };
}

function collectingProgram(program: CompiledSchema): CollectingProgram {
  return {
    nodes: program.nodes,
    root: program.root,
    usesDynamicScope: program.usesDynamicScope,
    maxDepth: program.options.maxDepth,
    formats: program.options.formats,
  };
}

function assemble(g: GeneratedCode): string {
  let s = '';
  if (g.usesDynamicScope) s += 'const DS = [];\nlet dsp = 0;\n';
  if (g.usesDepth) s += `let depth = 0;\nconst MAXDEPTH = ${g.maxDepth};\n`;
  s += g.declarations + '\n';
  if (g.usesDynamicScope || g.usesDepth) {
    s += 'function validate(x) {\n';
    if (g.usesDynamicScope) s += `  dsp = 0;\n  DS[dsp++] = ${g.rootResource};\n`;
    if (g.usesDepth) s += '  depth = 0;\n';
    s += `  return ${g.root}(x);\n}\n`;
  } else {
    s += `const validate = ${g.root};\n`;
  }
  return s;
}

/**
 * Compiles a schema (a parsed JSON value, or JSON text) into a validator. The schema is loaded, its references are
 * resolved and analysed, and the result is emitted as specialised JavaScript and instantiated with `new Function`.
 * Use {@link generateModule} instead where evaluating generated code at run time is not allowed (a strict CSP).
 */
export function compile(schema: unknown, options?: CompileOptions): Validator {
  const { g, program } = generate(schema, options);
  const source = assemble(g);
  let validate: (x: unknown) => boolean;
  try {
    validate = new Function('R', 'F', '"use strict";\n' + source + 'return validate;')(runtime, g.customFormats);
  } catch (e) {
    throw new SchemaCompilationError(`The schema produced invalid code: ${(e as Error).message}`);
  }
  const collecting = collectingProgram(program);
  const evaluate = (x: unknown, collector?: JsonSchemaResultsCollector): boolean =>
    collector === undefined ? validate(x) : evaluateWithCollector(collecting, x, collector);
  const validator = (g.usesDynamicScope || g.usesDepth ? (x: unknown) => validate(x) : validate) as Validator;
  Object.defineProperty(validator, 'source', { value: source });
  Object.defineProperty(validator, 'evaluate', { value: evaluate });
  return validator;
}

/**
 * Generates a standalone ES module for the schema: `export default function validate(instance): boolean`. The module
 * depends only on the runtime helpers (`@corvus-dotnet/json-schema/runtime`), so the schema compiler is not needed
 * where it runs. Custom format functions cannot be serialised; supply them to {@link compile} instead.
 */
export function generateModule(schema: unknown, options?: ModuleOptions): string {
  const { g, program } = generate(schema, options);
  if (g.customFormats.length > 0) {
    throw new SchemaCompilationError('Custom format functions cannot be emitted into a standalone module.');
  }
  const runtimeImport = options?.runtimeImport ?? '@corvus-dotnet/json-schema/runtime';
  const collecting = options?.collecting ?? true;
  let text = '// Generated by @corvus-dotnet/json-schema. Do not edit.\n' + `import * as R from ${JSON.stringify(runtimeImport)};\n`;
  if (collecting) {
    const collectingImport = options?.collectingImport ?? '@corvus-dotnet/json-schema/collecting';
    text += `import { evaluateWithCollector, loadProgram } from ${JSON.stringify(collectingImport)};\n`;
  }
  text += '\n' + assemble(g) + '\nexport { validate };\nexport default validate;\n';
  if (collecting) {
    text +=
      `\nconst image = ${JSON.stringify(serializeProgram(collectingProgram(program)))};\n` +
      'let program;\n' +
      '/** Evaluates the instance, reporting to the results collector when one is given. */\n' +
      'export function evaluate(instance, collector) {\n' +
      '  if (collector === undefined) return validate(instance);\n' +
      '  program ??= loadProgram(image);\n' +
      '  return evaluateWithCollector(program, instance, collector);\n' +
      '}\n';
  }
  return text;
}
