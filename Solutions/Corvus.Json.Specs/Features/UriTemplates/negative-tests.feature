Feature: negative-tests

Scenario Outline: Failure Tests at level 4
	Given the variables
		| name              | value                                                                                                       |
		| id                | "thing"                                                                                                     |
		| var               | "value"                                                                                                     |
		| hello             | "Hello World!"                                                                                              |
		| with space        | "fail"                                                                                                      |
		| leading_space     | "Hi!"                                                                                                       |
		| trailing_space    | "Bye!"                                                                                                      |
		| empty             | ""                                                                                                          |
		| path              | "/foo/bar"                                                                                                  |
		| x                 | "1024"                                                                                                      |
		| y                 | "768"                                                                                                       |
		| list              | ["red","green","blue"]                                                                                      |
		| keys              | {"semi":";","dot":".","comma":","}                                                                          |
		| example           | "red"                                                                                                       |
		| searchTerms       | "uri templates"                                                                                             |
		| ~thing            | "some-user"                                                                                                 |
		| default-graph-uri | ["http://www.example/book/","http://www.example/papers/"]                                                   |
		| query             | "PREFIX dc: \u003Chttp://purl.org/dc/elements/1.1/\u003E SELECT ?book ?who WHERE { ?book dc:creator ?who }" |
	When I apply the variables to the template <template>
	Then the result should be one of <result>

	Examples:
		| template                                | result  |
		| {/id*                                   | [false] |
		| /id*}                                   | [false] |
		| {/?id}                                  | [false] |
		| {var:prefix}                            | [false] |
		| {hello:2*}                              | [false] |
		| {??hello}                               | [false] |
		| {!hello}                                | [false] |
		| {with space}                            | [false] |
		| { leading_space}                        | [false] |
		| {trailing_space }                       | [false] |
		| {=path}                                 | [false] |
		| {$var}                                  | [false] |
		| {\|var*}                                | [false] |
		| {*keys?}                                | [false] |
		| {?empty=default,var}                    | [false] |
		| {var}{-prefix\|/-/\|var}                | [false] |
		| ?q={searchTerms}&amp;c={example:color?} | [false] |
		| x{?empty\|foo=none}                     | [false] |
		| /h{#hello+}                             | [false] |
		| /h#{hello+}                             | [false] |
		| {keys:1}                                | [false] |
		| {+keys:1}                               | [false] |
		| {;keys:1*}                              | [false] |
		| ?{-join\|&\|var,list}                   | [false] |
		| /people/{~thing}                        | [false] |
		| /{default-graph-uri}                    | [false] |
		| /sparql{?query,default-graph-uri}       | [false] |
		| /sparql{?query){&default-graph-uri*}    | [false] |
		| /resolution{?x, y}                      | [false] |