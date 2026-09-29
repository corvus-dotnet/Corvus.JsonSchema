#!/usr/bin/env node
// corvus-json-schema: emit a standalone validator module for a schema, or validate JSON documents against a schema.
//
//   corvus-json-schema generate <schema.json> [-o <out.mjs>] [--runtime-import <specifier>] [options]
//   corvus-json-schema validate <schema.json> <instance.json>... [options]
//
// Options: --default-dialect <4|6|7|2019-09|2020-12>, --assert-format, --base-uri <uri>, --entry-point <ref>,
//          --schema-dir <dir> (resolve remote $refs from local files named after the URI's last path segment).

import fs from 'node:fs';
import path from 'node:path';
import { compile, Dialect, generateModule, ModuleOptions } from './index.js';

const DIALECTS: Record<string, Dialect> = {
  '4': Dialect.Draft4,
  '6': Dialect.Draft6,
  '7': Dialect.Draft7,
  '2019-09': Dialect.Draft201909,
  '2020-12': Dialect.Draft202012,
};

function usage(): never {
  console.error(
    [
      'Usage:',
      '  corvus-json-schema generate <schema.json> [-o <out.mjs>] [--runtime-import <specifier>] [options]',
      '  corvus-json-schema validate <schema.json> <instance.json>... [options]',
      '',
      'Options:',
      '  --default-dialect <4|6|7|2019-09|2020-12>  dialect for schemas without $schema (default 2020-12)',
      '  --assert-format                            assert "format" (otherwise it is an annotation)',
      '  --base-uri <uri>                           base URI of the root schema',
      '  --entry-point <reference>                  evaluate from a subschema, e.g. #/$defs/item',
      '  --schema-dir <dir>                         resolve referenced documents from files in <dir>',
    ].join('\n'),
  );
  process.exit(2);
}

const args = process.argv.slice(2);
const command = args.shift();
const positional: string[] = [];
const options: ModuleOptions = {};
let output: string | undefined;
let schemaDir: string | undefined;
while (args.length > 0) {
  const a = args.shift()!;
  const value = (): string => args.shift() ?? usage();
  switch (a) {
    case '-o':
    case '--output':
      output = value();
      break;
    case '--runtime-import':
      options.runtimeImport = value();
      break;
    case '--default-dialect': {
      const d = DIALECTS[value().replace(/^draft-?/, '')];
      if (d === undefined) usage();
      options.defaultDialect = d;
      break;
    }
    case '--assert-format':
      options.assertFormat = true;
      break;
    case '--base-uri':
      options.baseUri = value();
      break;
    case '--entry-point':
      options.entryPoint = value();
      break;
    case '--schema-dir':
      schemaDir = value();
      break;
    default:
      if (a.startsWith('-')) usage();
      positional.push(a);
  }
}

if (schemaDir !== undefined) {
  const dir = schemaDir;
  options.resolveDocument = (uri: string) => {
    const file = path.join(dir, decodeURIComponent(new URL(uri).pathname.split('/').pop() ?? ''));
    return fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : undefined;
  };
}

const readJson = (file: string): unknown => JSON.parse(fs.readFileSync(file, 'utf8'));

if (command === 'generate' && positional.length === 1) {
  const source = generateModule(readJson(positional[0]), options);
  if (output === undefined) process.stdout.write(source);
  else fs.writeFileSync(output, source);
} else if (command === 'validate' && positional.length >= 2) {
  const validate = compile(readJson(positional[0]), options);
  let failed = 0;
  for (const file of positional.slice(1)) {
    const valid = validate(readJson(file));
    if (!valid) failed++;
    console.log(`${file}: ${valid ? 'valid' : 'invalid'}`);
  }
  process.exit(failed === 0 ? 0 : 1);
} else {
  usage();
}
