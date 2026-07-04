"""The evaluation tracker (for ``unevaluated*``) and the spec-output failure/annotation collector.

A faithful port of the TypeScript runtime's ``Ev`` and ``Results``. A generated ``evaluate`` runs
boolean-and-fail-fast by default (no collector). When detailed output is requested a :class:`Results` is
threaded through, gathering every failure and (in verbose mode) annotation in the JSON Schema spec output
shape (``keywordLocation`` / ``instanceLocation`` / ``absoluteKeywordLocation``).
"""

from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True, slots=True)
class Failure:
    """One validation failure, in the JSON Schema spec output shape."""

    keyword_location: str
    instance_location: str
    absolute_keyword_location: str | None = None


@dataclass(frozen=True, slots=True)
class Annotation:
    """One annotation contributed by a successfully validated subschema (verbose mode)."""

    keyword: str
    value: object
    keyword_location: str
    instance_location: str
    absolute_keyword_location: str | None = None


class Results:
    """Collects failures (and, in verbose mode, annotations) when detailed validation output is requested.

    The current instance-location and keyword-location are held as segment stacks, pushed/popped by the
    generated validators as they descend (:meth:`enter` / :meth:`leave`). A generated ``evaluate`` runs
    boolean-and-fail-fast with ``r is None``, so a validator only builds/threads location when a collector is
    present — the fast path computes no ``instanceLocation`` / ``keywordLocation`` at all.
    """

    def __init__(self, verbose: bool = False) -> None:
        self.verbose = verbose
        self.failures: list[Failure] = []
        self.annotations: list[Annotation] = []
        self._il: list[str] = []
        self._kl: list[str] = []

    @property
    def valid(self) -> bool:
        """True when no failure has been recorded."""
        return len(self.failures) == 0

    def enter(self, instance_segment: str, keyword_segment: str) -> None:
        """Descend into a sub-instance / subschema, pushing its location segments (collector present only)."""
        self._il.append(instance_segment)
        self._kl.append(keyword_segment)

    def leave(self) -> None:
        """Pop the segments pushed by the matching :meth:`enter`."""
        self._il.pop()
        self._kl.pop()

    def fork(self) -> Results:
        """A tentative sub-collector (anyOf/oneOf member) sharing verbosity and the current location path."""
        f = Results(self.verbose)
        f._il = list(self._il)
        f._kl = list(self._kl)
        return f

    def fail(self, keyword_suffix: str, absolute_keyword_location: str | None = None) -> None:
        """Record a failure at the current location plus a keyword suffix (e.g. ``"/type"``)."""
        self.failures.append(Failure("".join(self._kl) + keyword_suffix, "".join(self._il), absolute_keyword_location))

    def annotate(self, keyword: str, value: object, keyword_suffix: str, absolute_keyword_location: str | None = None) -> None:
        """Record an annotation at the current location plus a keyword suffix."""
        self.annotations.append(Annotation(keyword, value, "".join(self._kl) + keyword_suffix, "".join(self._il), absolute_keyword_location))

    def merge(self, other: Results) -> None:
        """Merge another collector's failures and annotations into this one (composition sub-collectors)."""
        self.failures.extend(other.failures)
        self.annotations.extend(other.annotations)


class Ev:
    """Tracks evaluated property positions and array indices for ``unevaluatedProperties`` / ``unevaluatedItems``.

    Locally-marked positions (``pl``/``il``) and those applied from a matched in-place applicator (``pa``/``ia``)
    are kept apart, exactly as the TypeScript ``Ev`` does; ``has_prop`` / ``has_item`` read the union. Python's
    arbitrary-precision integers serve as the bitmasks, so no overflow set is needed. ``n`` marks the shared
    no-op tracker (:data:`NOEV`), whose marks and merges are discarded.
    """

    __slots__ = ("n", "pl", "pa", "il", "ia")

    def __init__(self) -> None:
        self.n = False
        self.pl = 0
        self.pa = 0
        self.il = 0
        self.ia = 0

    def mark_prop(self, position: int) -> None:
        """Record that the property at enumeration ``position`` was evaluated locally."""
        if not self.n:
            self.pl |= 1 << position

    def has_prop(self, position: int) -> bool:
        """True when the property at enumeration ``position`` has been evaluated (locally or applied)."""
        return ((self.pl | self.pa) >> position) & 1 == 1

    def mark_item(self, index: int) -> None:
        """Record that array position ``index`` was evaluated locally."""
        if not self.n:
            self.il |= 1 << index

    def has_item(self, index: int) -> bool:
        """True when array position ``index`` has been evaluated (locally or applied)."""
        return ((self.il | self.ia) >> index) & 1 == 1

    def merge_props(self, child: Ev) -> None:
        """Merge a child tracker's evaluated properties into this one as applied (composition applicators)."""
        if not self.n:
            self.pa |= child.pl | child.pa

    def merge_items(self, child: Ev) -> None:
        """Merge a child tracker's evaluated array indices into this one as applied (composition applicators)."""
        if not self.n:
            self.ia |= child.il | child.ia


# The shared no-op tracker passed when a subschema does not consult unevaluated* (marks/merges discarded).
NOEV = Ev()
NOEV.n = True


def fresh() -> Ev:
    """A fresh evaluation tracker for a new instance scope."""
    return Ev()