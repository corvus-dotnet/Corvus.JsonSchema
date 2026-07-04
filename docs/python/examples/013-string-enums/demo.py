"""Runnable demo for recipe 013, string enumerations.

Run it from ``docs/python/examples`` (see ../README.md). The regenerate.ps1 gate type-checks this with
mypy --strict and pyright, then executes it.
"""

from generated import Status, Task, build_task, evaluate, parse

# 1. Build a Task. status and priority are string-literal unions, so only enum members type-check.
task: Task = {"status": "in_progress", "priority": "high"}
data = build_task(task)
print("1. built:     ", data.decode())

# 2. Validate. evaluate returns a bool (no exceptions) and rejects any value outside the enum set.
print("2. valid:     ", evaluate(data))  # True
print("   bad value: ", evaluate({"status": "archived"}))  # False, not a member

# 3. Parse bytes back to a typed Task. status is the literal union "todo" | "in_progress" | "done".
parsed = parse(data)
print("3. status:    ", parsed["status"])

# 4. A Literal union keys an exhaustive lookup: every member must be handled or the type does not check.
label: dict[Status, str] = {"todo": "To do", "in_progress": "In progress", "done": "Done"}
print("4. label:     ", label[parsed["status"]])
