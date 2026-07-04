"""Runnable demo for recipe 020, JSON Merge Patch (RFC 7396).

RFC 6902 JSON Patch (apply_patch / create_patch) is not yet implemented in the Python runtime, so this
recipe shows the RFC 7396 merge-patch wrappers the generator emits.

Run it from ``docs/python/examples`` (see ../README.md). The regenerate.ps1 gate type-checks this with
mypy --strict and pyright, then executes it.
"""

import json

from generated import (
    Contact,
    apply_merge_patch_contact,
    build_canonical_contact,
    build_contact,
    create_merge_patch_contact,
)

# A document to patch, as canonical UTF-8 JSON bytes (the wire / persistence shape).
original: Contact = {
    "name": "Ada Lovelace",
    "email": "ada@example.com",
    "phones": ["+1-555-0100"],
    "address": {"city": "London", "zip": "EC1"},
    "version": 1,
}
data = build_contact(original)
print("original:        ", data.decode())

# 1. RFC 7396 merge patch: an overlay document. A member set to None deletes that key; nested objects
#    merge recursively. apply returns canonical bytes (RFC 8785).
merged = apply_merge_patch_contact(
    data,
    {
        "email": "ada@new.example.com",  # replace
        "address": {"zip": None},         # delete address.zip, keep address.city
        "phones": None,                   # delete phones entirely
    },
)
print("1. applyMerge:   ", merged.decode())

# 2. Diff two documents into the merge patch that turns one into the other (apply it, store it, or send it).
target: Contact = {"name": "Ada L.", "phones": ["+1-555-0100"], "address": {"city": "Oxford"}, "version": 2}
merge = create_merge_patch_contact(data, target)
print("2. createMerge:  ", json.dumps(merge))

# 3. Applying the merge patch to the original reproduces the (canonical) target.
print("3. round-trips:  ", apply_merge_patch_contact(data, merge) == build_canonical_contact(target))
