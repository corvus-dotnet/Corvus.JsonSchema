// Bowtie (https://github.com/bowtie-json-schema/bowtie) harness for the Corvus.JsonSchema TypeScript evaluator.
// Speaks IHOP over stdin/stdout: one JSON request per line, one JSON response per line.
import os from 'node:os';
import process from 'node:process';
import readline from 'node:readline';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

// CORVUS_JSON_SCHEMA lets a local checkout run the harness against its own build.
const { compile, Dialect, JsonSchemaResultsCollector, ResultsLevel, enumerateAnnotations, schemaLocationFragment } = await import(
  process.env.CORVUS_JSON_SCHEMA ?? '@corvus-dotnet/json-schema'
);

const require = createRequire(import.meta.url);
const version = process.env.CORVUS_JSON_SCHEMA
  ? require(fileURLToPath(new URL('../package.json', process.env.CORVUS_JSON_SCHEMA))).version
  : require('@corvus-dotnet/json-schema/package.json').version;

const DIALECTS = {
  'https://json-schema.org/draft/2020-12/schema': Dialect.Draft202012,
  'https://json-schema.org/draft/2019-09/schema': Dialect.Draft201909,
  'http://json-schema.org/draft-07/schema#': Dialect.Draft7,
  'http://json-schema.org/draft-06/schema#': Dialect.Draft6,
  'http://json-schema.org/draft-04/schema#': Dialect.Draft4,
};

let started = false;
let dialect = Dialect.Draft202012;

function errored(error) {
  return { errored: true, context: { message: String(error?.message ?? error), traceback: String(error?.stack ?? '') } };
}

function stripFragment(uri) {
  const hash = uri.indexOf('#');
  return hash < 0 ? uri : uri.slice(0, hash);
}

const commands = {
  start(request) {
    if (request.version !== 1) throw new Error(`Unsupported IHOP version ${request.version}`);
    started = true;
    return {
      version: 1,
      implementation: {
        language: 'javascript',
        name: 'corvus-jsonschema',
        version,
        homepage: 'https://github.com/corvus-dotnet/Corvus.JsonSchema',
        documentation: 'https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-ts/corvus-json-schema',
        issues: 'https://github.com/corvus-dotnet/Corvus.JsonSchema/issues',
        source: 'https://github.com/corvus-dotnet/Corvus.JsonSchema',
        dialects: Object.keys(DIALECTS),
        os: os.platform(),
        os_version: os.release(),
        language_version: process.version,
      },
    };
  },

  dialect(request) {
    if (!started) throw new Error('Not started');
    dialect = DIALECTS[request.dialect] ?? dialect;
    return { ok: true };
  },

  run(request) {
    if (!started) throw new Error('Not started');
    const testCase = request.case;
    const registry = new Map();
    for (const [uri, schema] of Object.entries(testCase.registry ?? {})) registry.set(stripFragment(uri), schema);
    let validate;
    try {
      validate = compile(testCase.schema, {
        defaultDialect: dialect,
        resolveDocument: (uri) => registry.get(uri) ?? registry.get(stripFragment(uri)),
      });
    } catch (error) {
      return { seq: request.seq, ...errored(error) };
    }
    return {
      seq: request.seq,
      results: testCase.tests.map((test) => {
        try {
          if (request.output !== 'annotations') return { valid: validate(test.instance) };
          const collector = JsonSchemaResultsCollector.create(ResultsLevel.Verbose);
          const valid = validate.evaluate(test.instance, collector);
          const annotations = [...enumerateAnnotations(collector)].map((a) => ({
            keyword: a.keyword.replace(/~1/g, '/').replace(/~0/g, '~'),
            instanceLocation: a.instanceLocation,
            keywordLocation: schemaLocationFragment(a.schemaLocation + '/' + a.keyword),
            annotation: JSON.parse(a.value),
          }));
          return { valid, annotations };
        } catch (error) {
          return errored(error);
        }
      }),
    };
  },

  stop() {
    if (!started) throw new Error('Not started');
    process.exit(0);
  },
};

const input = readline.createInterface({ input: process.stdin, terminal: false });
for await (const line of input) {
  if (line.trim().length === 0) continue;
  const request = JSON.parse(line);
  const response = commands[request.cmd](request);
  process.stdout.write(JSON.stringify(response) + '\n');
}
