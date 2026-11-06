package jsonschema

import "testing"

// A leap second is a valid date-time only at 23:59:60 UTC on a day that ended with one, in whatever offset it is
// written, and a valid time whenever it is 23:59 in UTC.
func TestLeapSeconds(t *testing.T) {
	dateTimes := []struct {
		text  string
		valid bool
	}{
		{"1998-12-31T23:59:60Z", true},
		{"1998-12-31T15:59:60.123-08:00", true},
		{"1972-06-30T23:59:60Z", true},
		{"2016-12-31T23:59:60z", true},
		{"2017-01-01T00:59:60+01:00", true},
		{"1998-12-31T00:29:60-23:30", true},
		{"1999-01-01T00:29:60+00:30", true},
		{"1998-12-30T23:59:60-24:00", false},
		{"1998-12-31T23:59:60+00:00", true},
		{"1999-12-31T23:59:60Z", false},
		{"2017-12-31T23:59:60Z", false},
		{"1998-12-31T23:58:60Z", false},
		{"1998-12-31T22:59:60Z", false},
		{"1998-12-31T23:59:61Z", false},
		{"2016-12-31T22:59:60-01:00", true},
		{"2016-12-31T00:00:60Z", false},
		{"1998-12-31T23:59:60+23:59", false},
		{"1999-01-01T23:58:60+23:59", true},
		{"1998-12-31T00:00:60-23:59", true},
	}
	for _, c := range dateTimes {
		if got := isDateTime(c.text); got != c.valid {
			t.Errorf("isDateTime(%q) = %v, want %v", c.text, got, c.valid)
		}
	}
	times := []struct {
		text  string
		valid bool
	}{
		{"23:59:60Z", true},
		{"15:59:60-08:00", true},
		{"00:29:60-23:30", true},
		{"23:58:60Z", false},
	}
	for _, c := range times {
		if got := isTime(c.text); got != c.valid {
			t.Errorf("isTime(%q) = %v, want %v", c.text, got, c.valid)
		}
	}
}
