Feature: spec-examples

Scenario Outline: Level 1 Examples at level 1
	Given the variables
		| name  | value          |
		| var   | "value"        |
		| hello | "Hello World!" |
	When I apply the variables to the template <template>
	Then the result should be one of <result>

	Examples:
		| template | result               |
		| {var}    | ["value"]            |
		| {hello}  | ["Hello%20World%21"] |

Scenario Outline: Level 2 Examples at level 2
	Given the variables
		| name  | value          |
		| var   | "value"        |
		| hello | "Hello World!" |
		| path  | "/foo/bar"     |
	When I apply the variables to the template <template>
	Then the result should be one of <result>

	Examples:
		| template         | result                |
		| {+var}           | ["value"]             |
		| {+hello}         | ["Hello%20World!"]    |
		| {+path}/here     | ["/foo/bar/here"]     |
		| here?ref={+path} | ["here?ref=/foo/bar"] |

Scenario Outline: Level 3 Examples at level 3
	Given the variables
		| name  | value          |
		| var   | "value"        |
		| hello | "Hello World!" |
		| empty | ""             |
		| path  | "/foo/bar"     |
		| x     | "1024"         |
		| y     | "768"          |
	When I apply the variables to the template <template>
	Then the result should be one of <result>

	Examples:
		| template       | result                                  |
		| map?{x,y}      | ["map?1024,768"]                        |
		| {x,hello,y}    | ["1024,Hello%20World%21,768"]           |
		| {+x,hello,y}   | ["1024,Hello%20World!,768"]             |
		| {+path,x}/here | ["/foo/bar,1024/here"]                  |
		| {#x,hello,y}   | ["#1024,Hello%20World!,768"]            |
		| {#path,x}/here | ["#/foo/bar,1024/here"]                 |
		| X{.var}        | ["X.value"]                             |
		| X{.x,y}        | ["X.1024.768"]                          |
		| {/var}         | ["/value"]                              |
		| {/var,x}/here  | ["/value/1024/here"]                    |
		| {;x,y}         | [";x=1024;y=768"]                       |
		| {;x,y,empty}   | [";x=1024;y=768;empty"]                 |
		| {?x,y}         | ["?x=1024\u0026y=768"]                  |
		| {?x,y,empty}   | ["?x=1024\u0026y=768\u0026empty="]      |
		| ?fixed=yes{&x} | ["?fixed=yes\u0026x=1024"]              |
		| {&x,y,empty}   | ["\u0026x=1024\u0026y=768\u0026empty="] |

Scenario Outline: Level 4 Examples at level 4
	Given the variables
		| name  | value                              |
		| var   | "value"                            |
		| hello | "Hello World!"                     |
		| path  | "/foo/bar"                         |
		| list  | ["red","green","blue"]             |
		| keys  | {"semi":";","dot":".","comma":","} |
	When I apply the variables to the template <template>
	Then the result should be one of <result>

	Examples:
		| template        | result                                                                                                                                                                                                                                                              |
		| {var:3}         | ["val"]                                                                                                                                                                                                                                                             |
		| {var:30}        | ["value"]                                                                                                                                                                                                                                                           |
		| {list}          | ["red,green,blue"]                                                                                                                                                                                                                                                  |
		| {list*}         | ["red,green,blue"]                                                                                                                                                                                                                                                  |
		| {keys}          | ["comma,%2C,dot,.,semi,%3B","comma,%2C,semi,%3B,dot,.","dot,.,comma,%2C,semi,%3B","dot,.,semi,%3B,comma,%2C","semi,%3B,comma,%2C,dot,.","semi,%3B,dot,.,comma,%2C"]                                                                                                 |
		| {keys*}         | ["comma=%2C,dot=.,semi=%3B","comma=%2C,semi=%3B,dot=.","dot=.,comma=%2C,semi=%3B","dot=.,semi=%3B,comma=%2C","semi=%3B,comma=%2C,dot=.","semi=%3B,dot=.,comma=%2C"]                                                                                                 |
		| {+path:6}/here  | ["/foo/b/here"]                                                                                                                                                                                                                                                     |
		| {+list}         | ["red,green,blue"]                                                                                                                                                                                                                                                  |
		| {+list*}        | ["red,green,blue"]                                                                                                                                                                                                                                                  |
		| {+keys}         | ["comma,,,dot,.,semi,;","comma,,,semi,;,dot,.","dot,.,comma,,,semi,;","dot,.,semi,;,comma,,","semi,;,comma,,,dot,.","semi,;,dot,.,comma,,"]                                                                                                                         |
		| {+keys*}        | ["comma=,,dot=.,semi=;","comma=,,semi=;,dot=.","dot=.,comma=,,semi=;","dot=.,semi=;,comma=,","semi=;,comma=,,dot=.","semi=;,dot=.,comma=,"]                                                                                                                         |
		| {#path:6}/here  | ["#/foo/b/here"]                                                                                                                                                                                                                                                    |
		| {#list}         | ["#red,green,blue"]                                                                                                                                                                                                                                                 |
		| {#list*}        | ["#red,green,blue"]                                                                                                                                                                                                                                                 |
		| {#keys}         | ["#comma,,,dot,.,semi,;","#comma,,,semi,;,dot,.","#dot,.,comma,,,semi,;","#dot,.,semi,;,comma,,","#semi,;,comma,,,dot,.","#semi,;,dot,.,comma,,"]                                                                                                                   |
		| {#keys*}        | ["#comma=,,dot=.,semi=;","#comma=,,semi=;,dot=.","#dot=.,comma=,,semi=;","#dot=.,semi=;,comma=,","#semi=;,comma=,,dot=.","#semi=;,dot=.,comma=,"]                                                                                                                   |
		| X{.var:3}       | ["X.val"]                                                                                                                                                                                                                                                           |
		| X{.list}        | ["X.red,green,blue"]                                                                                                                                                                                                                                                |
		| X{.list*}       | ["X.red.green.blue"]                                                                                                                                                                                                                                                |
		| X{.keys}        | ["X.comma,%2C,dot,.,semi,%3B","X.comma,%2C,semi,%3B,dot,.","X.dot,.,comma,%2C,semi,%3B","X.dot,.,semi,%3B,comma,%2C","X.semi,%3B,comma,%2C,dot,.","X.semi,%3B,dot,.,comma,%2C"]                                                                                     |
		| {/var:1,var}    | ["/v/value"]                                                                                                                                                                                                                                                        |
		| {/list}         | ["/red,green,blue"]                                                                                                                                                                                                                                                 |
		| {/list*}        | ["/red/green/blue"]                                                                                                                                                                                                                                                 |
		| {/list*,path:4} | ["/red/green/blue/%2Ffoo"]                                                                                                                                                                                                                                          |
		| {/keys}         | ["/comma,%2C,dot,.,semi,%3B","/comma,%2C,semi,%3B,dot,.","/dot,.,comma,%2C,semi,%3B","/dot,.,semi,%3B,comma,%2C","/semi,%3B,comma,%2C,dot,.","/semi,%3B,dot,.,comma,%2C"]                                                                                           |
		| {/keys*}        | ["/comma=%2C/dot=./semi=%3B","/comma=%2C/semi=%3B/dot=.","/dot=./comma=%2C/semi=%3B","/dot=./semi=%3B/comma=%2C","/semi=%3B/comma=%2C/dot=.","/semi=%3B/dot=./comma=%2C"]                                                                                           |
		| {;hello:5}      | [";hello=Hello"]                                                                                                                                                                                                                                                    |
		| {;list}         | [";list=red,green,blue"]                                                                                                                                                                                                                                            |
		| {;list*}        | [";list=red;list=green;list=blue"]                                                                                                                                                                                                                                  |
		| {;keys}         | [";keys=comma,%2C,dot,.,semi,%3B",";keys=comma,%2C,semi,%3B,dot,.",";keys=dot,.,comma,%2C,semi,%3B",";keys=dot,.,semi,%3B,comma,%2C",";keys=semi,%3B,comma,%2C,dot,.",";keys=semi,%3B,dot,.,comma,%2C"]                                                             |
		| {;keys*}        | [";comma=%2C;dot=.;semi=%3B",";comma=%2C;semi=%3B;dot=.",";dot=.;comma=%2C;semi=%3B",";dot=.;semi=%3B;comma=%2C",";semi=%3B;comma=%2C;dot=.",";semi=%3B;dot=.;comma=%2C"]                                                                                           |
		| {?var:3}        | ["?var=val"]                                                                                                                                                                                                                                                        |
		| {?list}         | ["?list=red,green,blue"]                                                                                                                                                                                                                                            |
		| {?list*}        | ["?list=red\u0026list=green\u0026list=blue"]                                                                                                                                                                                                                        |
		| {?keys}         | ["?keys=comma,%2C,dot,.,semi,%3B","?keys=comma,%2C,semi,%3B,dot,.","?keys=dot,.,comma,%2C,semi,%3B","?keys=dot,.,semi,%3B,comma,%2C","?keys=semi,%3B,comma,%2C,dot,.","?keys=semi,%3B,dot,.,comma,%2C"]                                                             |
		| {?keys*}        | ["?comma=%2C\u0026dot=.\u0026semi=%3B","?comma=%2C\u0026semi=%3B\u0026dot=.","?dot=.\u0026comma=%2C\u0026semi=%3B","?dot=.\u0026semi=%3B\u0026comma=%2C","?semi=%3B\u0026comma=%2C\u0026dot=.","?semi=%3B\u0026dot=.\u0026comma=%2C"]                               |
		| {&var:3}        | ["\u0026var=val"]                                                                                                                                                                                                                                                   |
		| {&list}         | ["\u0026list=red,green,blue"]                                                                                                                                                                                                                                       |
		| {&list*}        | ["\u0026list=red\u0026list=green\u0026list=blue"]                                                                                                                                                                                                                   |
		| {&keys}         | ["\u0026keys=comma,%2C,dot,.,semi,%3B","\u0026keys=comma,%2C,semi,%3B,dot,.","\u0026keys=dot,.,comma,%2C,semi,%3B","\u0026keys=dot,.,semi,%3B,comma,%2C","\u0026keys=semi,%3B,comma,%2C,dot,.","\u0026keys=semi,%3B,dot,.,comma,%2C"]                               |
		| {&keys*}        | ["\u0026comma=%2C\u0026dot=.\u0026semi=%3B","\u0026comma=%2C\u0026semi=%3B\u0026dot=.","\u0026dot=.\u0026comma=%2C\u0026semi=%3B","\u0026dot=.\u0026semi=%3B\u0026comma=%2C","\u0026semi=%3B\u0026comma=%2C\u0026dot=.","\u0026semi=%3B\u0026dot=.\u0026comma=%2C"] |