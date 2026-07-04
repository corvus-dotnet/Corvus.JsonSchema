"""Runnable demo for recipe 012, discriminated unions.

Run it from ``docs/python/examples`` (see ../README.md). The regenerate.ps1 gate type-checks this with
mypy --strict and pyright, then executes it.
"""

from generated import (
    Click,
    Event,
    KeyPress,
    build_click,
    build_key_press,
    evaluate,
    match_event,
    parse,
)


def describe(e: Event) -> str:
    # Every branch carries a `type` const, so this is a tagged union. match_event dispatches on the
    # discriminant to a per-branch handler and is generic in the return type, here str.
    return match_event(
        e,
        click=lambda c: f"click at {c['x']},{c['y']}",
        key_press=lambda k: f"key {k['key']}",
        scroll=lambda s: f"scroll {s['delta']}",
    )


# Build each branch by its own type, then parse to the Event union.
click: Click = {"type": "click", "x": 10, "y": 20}
event = parse(build_click(click))
print("valid:   ", evaluate(event))  # True
print(describe(event))

keypress: KeyPress = {"type": "keypress", "key": "Enter"}
print(describe(parse(build_key_press(keypress))))
