"""Writes a benchmark results page: one self-contained HTML file from a data file in the shape below.

The measurements are embedded as JSON and the charts are drawn in the browser, so the page can be committed, served
from GitHub Pages or copied to a bucket as it is. The data file (JSON):

    {
      "meta": {
        "title": "...", "lede": "one paragraph under the title", "repo": "owner/name",
        "commit": "sha", "date": "ISO 8601", "runUrl": "", "runs": 0 or the runs each measurement has,
        "facts": [["Host", "..."], ["Commit", "..."]],            # shown in the run header, in order
        "method": "<p>...</p><p>...</p>"                           # the 'How to read this' section, as HTML
      },
      "vocab": {                                                    # the words the page uses for its two axes
        "impl": "engine", "impls": "engines", "Impls": "Engines",
        "schema": "workload", "schemas": "workloads", "Schema": "Workload", "Schemas": "Workloads",
        "failed": "failed", "centre": "Mean", "spread": "Error",
        "barLegend": "mean of the run", "whiskerLegend": "whisker: the error of the mean"
      },
      "measures": {                                                 # the figures to compare on, in order
        "time": { "label": "Time", "long": "time per operation", "parts": ["time"] },
        "allocated": { "label": "Allocated", "long": "bytes allocated", "parts": ["allocated"], "bytes": true }
      },
      "impls": [{ "id": "...", "name": "...", "group": "...", "version": "..." }],
      "schemas": [{ "name": "...", "note": "...", "meta": [["instances", "333"]], "url": "", "urlLabel": "" }],
      "results": { "<impl id>": { "<schema name>": null | { "<part>": [runs...] | {"median": n, "min": n, "max": n} } } }
    }

A result of null is a failure (the implementation did not complete the schema); an absent entry was not run. A
part is either every run's value, in nanoseconds (or the unit the measure says), or a summary of it. Parts of a
measure are summed, so "parse + warm" is a measure with two parts.

Usage:
    python3 generate_site.py --data results.json --out index.html [--fragment]
"""

import argparse
import html, json
import os

TEMPLATE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "template.html")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--data", required=True)
    parser.add_argument("--out", required=True)
    # For hosts that supply the document skeleton themselves, such as a preview that wraps the page.
    parser.add_argument("--fragment", action="store_true", help="Emit the page without its document skeleton.")
    args = parser.parse_args()

    with open(args.data, encoding="utf-8") as f:
        data = json.load(f)
    for key in ("meta", "vocab", "measures", "impls", "schemas", "results"):
        if key not in data:
            raise SystemExit(f"the data has no '{key}'")

    payload = json.dumps(data, separators=(",", ":")).replace("</", "<\\/")
    with open(TEMPLATE, encoding="utf-8") as f:
        page = f.read().replace("/*BENCHMARK_DATA*/null", payload)
    # The page's title is the run's, and an icon when the data names one (meta.icon, a path the host serves):
    # without a link the browser asks for /favicon.ico and logs a 404 on a host that has none.
    meta = data.get("meta", {})
    head_tags = "<title>" + html.escape(meta.get("title") or "Benchmark results") + "</title>"
    if meta.get("icon"):
        head_tags += '\n<link rel="icon" href="' + html.escape(meta["icon"], quote=True) + '">'
    page = page.replace("<title>Benchmark results</title>", head_tags, 1)
    if not args.fragment:
        head, body = page.split("<header", 1)
        page = (
            '<!doctype html>\n<html lang="en">\n<head>\n<meta charset="utf-8">\n'
            '<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">\n'
            f"{head}</head>\n<body>\n<header{body}</body>\n</html>\n"
        )

    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as f:
        f.write(page)


if __name__ == "__main__":
    main()
