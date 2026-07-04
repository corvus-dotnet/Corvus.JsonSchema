"""Runnable demo for recipe 011, untagged unions.

Run it from ``docs/python/examples`` (see ../README.md). The regenerate.ps1 gate type-checks this with
mypy --strict and pyright, then executes it.
"""

from generated import Value, is_one_of, match_value


def describe(v: Value) -> str:
    # match_<t> tries each member guard in order and calls the matching keyword handler. It is generic in the
    # return type, so every branch must produce the same type (here str).
    return match_value(
        v,
        one_of=lambda s: f"string {s!r}",
        one_of2=lambda n: f"number {n}",
        one_of3=lambda b: f"boolean {b}",
    )


values: list[Value] = ["hello", 42, True]
for value in values:
    print(describe(value))

# The is_<member> guards narrow a value on their own (they are typing.TypeGuard).
maybe: Value = "world"
if is_one_of(maybe):
    print("length:", len(maybe))  # maybe is str here
