"""Runnable demo for recipe 006, constraining a base type with allOf.

Run it from ``docs/python/examples`` (see ../README.md). The regenerate.ps1 gate type-checks this with
mypy --strict and pyright, then executes it.
"""

from generated import build_small_batch, evaluate

# allOf tightens the base rather than adding to it: Batch requires size >= 1; SmallBatch also caps
# size <= 100. The effective constraint is 1 <= size <= 100, enforced by evaluate. The shape is unchanged.

# 1. In range. build compact bytes, then evaluate them.
print("1. size 50: ", evaluate(build_small_batch({"size": 50})))  # True

# 2. Above the maximum added by SmallBatch.
print("2. size 200:", evaluate({"size": 200}))  # False, maximum 100 (added here)

# 3. Below the minimum inherited from the base Batch.
print("3. size 0:  ", evaluate({"size": 0}))  # False, minimum 1 (from the base)
