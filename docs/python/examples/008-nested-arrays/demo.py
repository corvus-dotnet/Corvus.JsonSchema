"""Runnable demo for recipe 008, arrays of higher rank.

Run it from ``docs/python/examples`` (see ../README.md). The regenerate.ps1 gate type-checks this with
mypy --strict and pyright, then executes it.
"""

from generated import Grid, build_grid, evaluate, parse

# 1. Build a Grid: cells is a rank-2 array (an array of arrays of number). The nesting continues for
#    rank-N, so a rank-3 schema would generate Sequence[Sequence[Sequence[float]]].
grid_in: Grid = {"cells": [[1, 2, 3], [4, 5, 6]]}
data = build_grid(grid_in)
print("1. valid:   ", evaluate(data))  # True

# 2. Parse and read with ordinary double-indexing; every level is typed.
grid = parse(data)
print("2. cell 1,2:", grid["cells"][1][2])  # 6
