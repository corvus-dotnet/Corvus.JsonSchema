"""A leap second is a valid date-time only at 23:59:60 UTC on a day that ended with one, in whatever offset it is
written, and a valid time whenever it is 23:59 in UTC. The cases of the Go module's test."""

from __future__ import annotations

import pytest

from corvus_json_schema.formats import date_time, time

DATE_TIMES = [
    ("1998-12-31T23:59:60Z", True),
    ("1998-12-31T15:59:60.123-08:00", True),
    ("1972-06-30T23:59:60Z", True),
    ("2016-12-31T23:59:60z", True),
    ("2017-01-01T00:59:60+01:00", True),
    ("1998-12-31T00:29:60-23:30", True),
    ("1999-01-01T00:29:60+00:30", True),
    ("1998-12-30T23:59:60-24:00", False),
    ("1998-12-31T23:59:60+00:00", True),
    ("1999-12-31T23:59:60Z", False),
    ("2017-12-31T23:59:60Z", False),
    ("1998-12-31T23:58:60Z", False),
    ("1998-12-31T22:59:60Z", False),
    ("1998-12-31T23:59:61Z", False),
    ("2016-12-31T22:59:60-01:00", True),
    ("2016-12-31T00:00:60Z", False),
    ("1998-12-31T23:59:60+23:59", False),
    ("1999-01-01T23:58:60+23:59", True),
    ("1998-12-31T00:00:60-23:59", True),
]

TIMES = [("23:59:60Z", True), ("15:59:60-08:00", True), ("00:29:60-23:30", True), ("23:58:60Z", False)]


@pytest.mark.parametrize(("text", "want"), DATE_TIMES)
def test_date_time(text: str, want: bool) -> None:
    assert date_time(text) is want


@pytest.mark.parametrize(("text", "want"), TIMES)
def test_time(text: str, want: bool) -> None:
    assert time(text) is want
