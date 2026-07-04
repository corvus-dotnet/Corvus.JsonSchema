"""Runnable demo for recipe 020: RFC 6902 JSON Patch and RFC 7396 JSON Merge Patch.

The generator emits both patch families over a type's canonical bytes. The RFC 6902 operation array
(``apply_patch_<t>`` / ``create_patch_<t>``) with add / remove / replace / move / copy / test, and the RFC 7396
overlay (``apply_merge_patch_<t>`` / ``create_merge_patch_<t>``). It also emits ``produce_<t>``, which runs a
recipe over a decoded draft and returns canonical bytes.

Run it from ``docs/python/examples`` (see ../README.md). The regenerate.ps1 gate type-checks this with
mypy --strict and pyright, then executes it.
"""

import json

from generated import (
    Contact,
    apply_merge_patch_contact,
    apply_patch_contact,
    build_canonical_contact,
    build_contact,
    create_merge_patch_contact,
    create_patch_contact,
    produce_contact,
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

# 1. RFC 6902 JSON Patch: an ordered array of operations, each naming a JSON Pointer path. A `test` guards a
#    precondition and aborts the whole patch on mismatch. apply returns canonical bytes (RFC 8785).
ops: list[object] = [
    {"op": "test", "path": "/version", "value": 1},                 # precondition: abort unless version is 1
    {"op": "add", "path": "/phones/-", "value": "+44-20-5550100"},  # append a phone
    {"op": "replace", "path": "/email", "value": "ada@new.example.com"},
    {"op": "copy", "from": "/name", "path": "/displayName"},         # copy name into a new displayName
    {"op": "move", "from": "/phones/0", "path": "/phones/1"},        # reorder the two phones
    {"op": "remove", "path": "/address/zip"},                       # drop address.zip, keep address.city
]
patched = apply_patch_contact(data, ops)
print("1. applyPatch:   ", patched.decode())

# 2. Diff two documents into the RFC 6902 patch (add / remove / replace) that turns one into the other.
target: Contact = {
    "name": "Ada Lovelace",
    "email": "ada@lovelace.example",
    "phones": ["+1-555-0100"],
    "address": {"city": "Oxford", "zip": "EC1"},
    "version": 2,
}
diff = create_patch_contact(data, target)
print("2. createPatch:  ", json.dumps(diff))
print("3. round-trips:  ", apply_patch_contact(data, diff) == build_canonical_contact(target))

# 4. RFC 7396 merge patch: an overlay document, the simpler partial-update form. A member set to None deletes
#    that key; nested objects merge recursively. apply returns canonical bytes.
merged = apply_merge_patch_contact(
    data,
    {
        "email": "ada@new.example.com",  # replace
        "address": {"zip": None},         # delete address.zip, keep address.city
        "phones": None,                   # delete phones entirely
    },
)
print("4. applyMerge:   ", merged.decode())

# 5. The merge-patch diff, where None marks each deletion.
merge = create_merge_patch_contact(data, target)
print("5. createMerge:  ", json.dumps(merge))


# 6. produce: run a recipe over a decoded draft. The recipe mutates the draft in place; produce returns the
#    result as canonical bytes and leaves the source untouched.
def promote(draft: Contact) -> None:
    draft["version"] = draft.get("version", 0) + 1
    draft["displayName"] = "Ada L."


produced = produce_contact(data, promote)
print("6. produce:      ", produced.decode())
