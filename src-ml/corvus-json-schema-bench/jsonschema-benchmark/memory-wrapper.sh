#!/bin/sh
OUTPUT=$(/usr/bin/time -f %M,%x -o /dev/stdout -q "$@" | sed '$!N; s/\n/,/; P; D')
EXIT_STATUS="${OUTPUT##*,}"
echo "$OUTPUT" | sed 's/,[^,]*$//'
exit $EXIT_STATUS
