"""corvus-json-schema: emit a standalone validator module for a schema, or validate JSON documents against a schema.

    corvus-json-schema generate <schema.json> [-o <out.py>] [--runtime-import <module>] [options]
    corvus-json-schema validate <schema.json> <instance.json>... [options]

Options: --default-dialect <4|6|7|2019-09|2020-12>, --assert-format, --base-uri <uri>, --entry-point <ref>,
--schema-dir <dir> (resolve remote $refs from local files named after the URI's last path segment).
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any
from urllib.parse import unquote, urlsplit

from . import Dialect, compile, generate_module

DIALECTS = {
    "4": Dialect.DRAFT4,
    "6": Dialect.DRAFT6,
    "7": Dialect.DRAFT7,
    "2019-09": Dialect.DRAFT201909,
    "2020-12": Dialect.DRAFT202012,
}


def _read_json(file: str) -> Any:
    return json.loads(Path(file).read_text(encoding="utf-8"))


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        prog="corvus-json-schema", description=__doc__.split("\n\n")[0] if __doc__ else None
    )
    parser.add_argument("command", choices=["generate", "validate"])
    parser.add_argument("files", nargs="+")
    parser.add_argument("-o", "--output", help="the file to write the generated module to (generate)")
    parser.add_argument(
        "--runtime-import", default="corvus_json_schema.runtime", help="the module of the runtime helpers"
    )
    parser.add_argument("--default-dialect", help="dialect for schemas without $schema (default 2020-12)")
    parser.add_argument("--assert-format", action="store_true", help='assert "format" (otherwise it is an annotation)')
    parser.add_argument("--base-uri", help="base URI of the root schema")
    parser.add_argument("--entry-point", help="evaluate from a subschema, e.g. #/$defs/item")
    parser.add_argument("--schema-dir", help="resolve referenced documents from files in this directory")
    args = parser.parse_args(argv)

    options: dict[str, Any] = {}
    if args.default_dialect is not None:
        name = args.default_dialect.removeprefix("draft").removeprefix("-")
        if name not in DIALECTS:
            parser.error(f"unknown dialect '{args.default_dialect}'")
        options["default_dialect"] = DIALECTS[name]
    if args.assert_format:
        options["assert_format"] = True
    if args.base_uri is not None:
        options["base_uri"] = args.base_uri
    if args.entry_point is not None:
        options["entry_point"] = args.entry_point
    if args.schema_dir is not None:
        directory = Path(args.schema_dir)

        def resolve(uri: str) -> Any:
            file = directory / unquote(urlsplit(uri).path.split("/")[-1])
            return json.loads(file.read_text(encoding="utf-8")) if file.is_file() else None

        options["resolve_document"] = resolve

    if args.command == "generate":
        if len(args.files) != 1:
            parser.error("generate takes one schema file")
        source = generate_module(_read_json(args.files[0]), runtime_import=args.runtime_import, **options)
        if args.output is None:
            sys.stdout.write(source)
        else:
            Path(args.output).write_text(source, encoding="utf-8")
        return 0

    if len(args.files) < 2:
        parser.error("validate takes a schema file and at least one instance file")
    validate = compile(_read_json(args.files[0]), **options)
    failed = 0
    for file in args.files[1:]:
        valid = validate(_read_json(file))
        if not valid:
            failed += 1
        print(f"{file}: {'valid' if valid else 'invalid'}")
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
