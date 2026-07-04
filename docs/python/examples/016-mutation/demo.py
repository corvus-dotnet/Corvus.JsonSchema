"""Runnable demo for recipe 016, byte-native mutation.

Run it from ``docs/python/examples`` (see ../README.md). The regenerate.ps1 gate type-checks this with
mypy --strict and pyright, then executes it.
"""

from corvus_json_runtime import RmwArrayOps

from generated import Document, build_document, patch_document, with_defaults_document

# 1. Build a document from plain values.
doc: Document = {"id": "doc-1", "title": "Draft", "tags": ["a", "b"]}
data = build_document(doc)
print("1. built:        ", data.decode())

# 2. Byte-native member patch: change title, remove tags. Only the changed byte spans are rewritten; every
#    other byte is copied through verbatim.
member_patch = patch_document(data, {"title": "Final"}, ["tags"])
print("2. member patch: ", member_patch.decode())

# 3. Byte-native array-element edits: append an element and replace element 0 of `tags` in place.
tag_ops: RmwArrayOps = {"append": ["c"], "set": {0: "A"}}
array_patch = patch_document(data, {}, None, {"tags": tag_ops})
print("3. array patch:  ", array_patch.decode())

# 4. with_defaults fills every absent property that declares a `default` (title, version).
filled = with_defaults_document({"id": "doc-2"})
print("4. with_defaults:", filled)
