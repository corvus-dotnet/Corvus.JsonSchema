Feature: spec-examples-by-section

Scenario Outline: 3.2.1 Variable Expansion at level 0
	Given the variables
		| name       | value                              |
		| count      | ["one","two","three"]              |
		| dom        | ["example","com"]                  |
		| dub        | "me/too"                           |
		| hello      | "Hello World!"                     |
		| half       | "50%"                              |
		| var        | "value"                            |
		| who        | "fred"                             |
		| base       | "http://example.com/home/"         |
		| path       | "/foo/bar"                         |
		| list       | ["red","green","blue"]             |
		| keys       | {"semi":";","dot":".","comma":","} |
		| v          | "6"                                |
		| x          | "1024"                             |
		| y          | "768"                              |
		| empty      | ""                                 |
		| empty_keys | []                                 |
		| undef      | null                               |
	When I apply the variables to the template <template>
	Then the result should be one of <result>

	Examples:
		| template  | result                                              |
		| {count}   | ["one,two,three"]                                   |
		| {count*}  | ["one,two,three"]                                   |
		| {/count}  | ["/one,two,three"]                                  |
		| {/count*} | ["/one/two/three"]                                  |
		| {;count}  | [";count=one,two,three"]                            |
		| {;count*} | [";count=one;count=two;count=three"]                |
		| {?count}  | ["?count=one,two,three"]                            |
		| {?count*} | ["?count=one\u0026count=two\u0026count=three"]      |
		| {&count*} | ["\u0026count=one\u0026count=two\u0026count=three"] |

Scenario Outline: 3.2.2 Simple String Expansion at level 0
	Given the variables
		| name       | value                              |
		| count      | ["one","two","three"]              |
		| dom        | ["example","com"]                  |
		| dub        | "me/too"                           |
		| hello      | "Hello World!"                     |
		| half       | "50%"                              |
		| var        | "value"                            |
		| who        | "fred"                             |
		| base       | "http://example.com/home/"         |
		| path       | "/foo/bar"                         |
		| list       | ["red","green","blue"]             |
		| keys       | {"semi":";","dot":".","comma":","} |
		| v          | "6"                                |
		| x          | "1024"                             |
		| y          | "768"                              |
		| empty      | ""                                 |
		| empty_keys | []                                 |
		| undef      | null                               |
	When I apply the variables to the template <template>
	Then the result should be one of <result>

	Examples:
		| template    | result                                                                                                                                                              |
		| {var}       | ["value"]                                                                                                                                                           |
		| {hello}     | ["Hello%20World%21"]                                                                                                                                                |
		| {half}      | ["50%25"]                                                                                                                                                           |
		| O{empty}X   | ["OX"]                                                                                                                                                              |
		| O{undef}X   | ["OX"]                                                                                                                                                              |
		| {x,y}       | ["1024,768"]                                                                                                                                                        |
		| {x,hello,y} | ["1024,Hello%20World%21,768"]                                                                                                                                       |
		| ?{x,empty}  | ["?1024,"]                                                                                                                                                          |
		| ?{x,undef}  | ["?1024"]                                                                                                                                                           |
		| ?{undef,y}  | ["?768"]                                                                                                                                                            |
		| {var:3}     | ["val"]                                                                                                                                                             |
		| {var:30}    | ["value"]                                                                                                                                                           |
		| {list}      | ["red,green,blue"]                                                                                                                                                  |
		| {list*}     | ["red,green,blue"]                                                                                                                                                  |
		| {keys}      | ["comma,%2C,dot,.,semi,%3B","comma,%2C,semi,%3B,dot,.","dot,.,comma,%2C,semi,%3B","dot,.,semi,%3B,comma,%2C","semi,%3B,comma,%2C,dot,.","semi,%3B,dot,.,comma,%2C"] |
		| {keys*}     | ["comma=%2C,dot=.,semi=%3B","comma=%2C,semi=%3B,dot=.","dot=.,comma=%2C,semi=%3B","dot=.,semi=%3B,comma=%2C","semi=%3B,comma=%2C,dot=.","semi=%3B,dot=.,comma=%2C"] |

Scenario Outline: 3.2.3 Reserved Expansion at level 0
	Given the variables
		| name       | value                              |
		| count      | ["one","two","three"]              |
		| dom        | ["example","com"]                  |
		| dub        | "me/too"                           |
		| hello      | "Hello World!"                     |
		| half       | "50%"                              |
		| var        | "value"                            |
		| who        | "fred"                             |
		| base       | "http://example.com/home/"         |
		| path       | "/foo/bar"                         |
		| list       | ["red","green","blue"]             |
		| keys       | {"semi":";","dot":".","comma":","} |
		| v          | "6"                                |
		| x          | "1024"                             |
		| y          | "768"                              |
		| empty      | ""                                 |
		| empty_keys | []                                 |
		| undef      | null                               |
	When I apply the variables to the template <template>
	Then the result should be one of <result>

	Examples:
		| template            | result                                                                                                                                      |
		| {+var}              | ["value"]                                                                                                                                   |
		| {/var,empty}        | ["/value/"]                                                                                                                                 |
		| {/var,undef}        | ["/value"]                                                                                                                                  |
		| {+hello}            | ["Hello%20World!"]                                                                                                                          |
		| {+half}             | ["50%25"]                                                                                                                                   |
		| {base}index         | ["http%3A%2F%2Fexample.com%2Fhome%2Findex"]                                                                                                 |
		| {+base}index        | ["http://example.com/home/index"]                                                                                                           |
		| O{+empty}X          | ["OX"]                                                                                                                                      |
		| O{+undef}X          | ["OX"]                                                                                                                                      |
		| {+path}/here        | ["/foo/bar/here"]                                                                                                                           |
		| {+path:6}/here      | ["/foo/b/here"]                                                                                                                             |
		| here?ref={+path}    | ["here?ref=/foo/bar"]                                                                                                                       |
		| up{+path}{var}/here | ["up/foo/barvalue/here"]                                                                                                                    |
		| {+x,hello,y}        | ["1024,Hello%20World!,768"]                                                                                                                 |
		| {+path,x}/here      | ["/foo/bar,1024/here"]                                                                                                                      |
		| {+list}             | ["red,green,blue"]                                                                                                                          |
		| {+list*}            | ["red,green,blue"]                                                                                                                          |
		| {+keys}             | ["comma,,,dot,.,semi,;","comma,,,semi,;,dot,.","dot,.,comma,,,semi,;","dot,.,semi,;,comma,,","semi,;,comma,,,dot,.","semi,;,dot,.,comma,,"] |
		| {+keys*}            | ["comma=,,dot=.,semi=;","comma=,,semi=;,dot=.","dot=.,comma=,,semi=;","dot=.,semi=;,comma=,","semi=;,comma=,,dot=.","semi=;,dot=.,comma=,"] |

Scenario Outline: 3.2.4 Fragment Expansion at level 0
	Given the variables
		| name       | value                              |
		| count      | ["one","two","three"]              |
		| dom        | ["example","com"]                  |
		| dub        | "me/too"                           |
		| hello      | "Hello World!"                     |
		| half       | "50%"                              |
		| var        | "value"                            |
		| who        | "fred"                             |
		| base       | "http://example.com/home/"         |
		| path       | "/foo/bar"                         |
		| list       | ["red","green","blue"]             |
		| keys       | {"semi":";","dot":".","comma":","} |
		| v          | "6"                                |
		| x          | "1024"                             |
		| y          | "768"                              |
		| empty      | ""                                 |
		| empty_keys | []                                 |
		| undef      | null                               |
	When I apply the variables to the template <template>
	Then the result should be one of <result>

	Examples:
		| template       | result                                                                                                                                            |
		| {#var}         | ["#value"]                                                                                                                                        |
		| {#hello}       | ["#Hello%20World!"]                                                                                                                               |
		| {#half}        | ["#50%25"]                                                                                                                                        |
		| foo{#empty}    | ["foo#"]                                                                                                                                          |
		| foo{#undef}    | ["foo"]                                                                                                                                           |
		| {#x,hello,y}   | ["#1024,Hello%20World!,768"]                                                                                                                      |
		| {#path,x}/here | ["#/foo/bar,1024/here"]                                                                                                                           |
		| {#path:6}/here | ["#/foo/b/here"]                                                                                                                                  |
		| {#list}        | ["#red,green,blue"]                                                                                                                               |
		| {#list*}       | ["#red,green,blue"]                                                                                                                               |
		| {#keys}        | ["#comma,,,dot,.,semi,;","#comma,,,semi,;,dot,.","#dot,.,comma,,,semi,;","#dot,.,semi,;,comma,,","#semi,;,comma,,,dot,.","#semi,;,dot,.,comma,,"] |

Scenario Outline: 3.2.5 Label Expansion with Dot-Prefix at level 0
	Given the variables
		| name       | value                              |
		| count      | ["one","two","three"]              |
		| dom        | ["example","com"]                  |
		| dub        | "me/too"                           |
		| hello      | "Hello World!"                     |
		| half       | "50%"                              |
		| var        | "value"                            |
		| who        | "fred"                             |
		| base       | "http://example.com/home/"         |
		| path       | "/foo/bar"                         |
		| list       | ["red","green","blue"]             |
		| keys       | {"semi":";","dot":".","comma":","} |
		| v          | "6"                                |
		| x          | "1024"                             |
		| y          | "768"                              |
		| empty      | ""                                 |
		| empty_keys | []                                 |
		| undef      | null                               |
	When I apply the variables to the template <template>
	Then the result should be one of <result>

	Examples:
		| template        | result                                                                                                                                            |
		| {.who}          | [".fred"]                                                                                                                                         |
		| {.who,who}      | [".fred.fred"]                                                                                                                                    |
		| {.half,who}     | [".50%25.fred"]                                                                                                                                   |
		| www{.dom*}      | ["www.example.com"]                                                                                                                               |
		| X{.var}         | ["X.value"]                                                                                                                                       |
		| X{.var:3}       | ["X.val"]                                                                                                                                         |
		| X{.empty}       | ["X."]                                                                                                                                            |
		| X{.undef}       | ["X"]                                                                                                                                             |
		| X{.list}        | ["X.red,green,blue"]                                                                                                                              |
		| X{.list*}       | ["X.red.green.blue"]                                                                                                                              |
		| {#keys}         | ["#comma,,,dot,.,semi,;","#comma,,,semi,;,dot,.","#dot,.,comma,,,semi,;","#dot,.,semi,;,comma,,","#semi,;,comma,,,dot,.","#semi,;,dot,.,comma,,"] |
		| {#keys*}        | ["#comma=,,dot=.,semi=;","#comma=,,semi=;,dot=.","#dot=.,comma=,,semi=;","#dot=.,semi=;,comma=,","#semi=;,comma=,,dot=.","#semi=;,dot=.,comma=,"] |
		| X{.empty_keys}  | ["X"]                                                                                                                                             |
		| X{.empty_keys*} | ["X"]                                                                                                                                             |

Scenario Outline: 3.2.6 Path Segment Expansion at level 0
	Given the variables
		| name       | value                              |
		| count      | ["one","two","three"]              |
		| dom        | ["example","com"]                  |
		| dub        | "me/too"                           |
		| hello      | "Hello World!"                     |
		| half       | "50%"                              |
		| var        | "value"                            |
		| who        | "fred"                             |
		| base       | "http://example.com/home/"         |
		| path       | "/foo/bar"                         |
		| list       | ["red","green","blue"]             |
		| keys       | {"semi":";","dot":".","comma":","} |
		| v          | "6"                                |
		| x          | "1024"                             |
		| y          | "768"                              |
		| empty      | ""                                 |
		| empty_keys | []                                 |
		| undef      | null                               |
	When I apply the variables to the template <template>
	Then the result should be one of <result>

	Examples:
		| template        | result                                                                                                                                                                    |
		| {/who}          | ["/fred"]                                                                                                                                                                 |
		| {/who,who}      | ["/fred/fred"]                                                                                                                                                            |
		| {/half,who}     | ["/50%25/fred"]                                                                                                                                                           |
		| {/who,dub}      | ["/fred/me%2Ftoo"]                                                                                                                                                        |
		| {/var}          | ["/value"]                                                                                                                                                                |
		| {/var,empty}    | ["/value/"]                                                                                                                                                               |
		| {/var,undef}    | ["/value"]                                                                                                                                                                |
		| {/var,x}/here   | ["/value/1024/here"]                                                                                                                                                      |
		| {/var:1,var}    | ["/v/value"]                                                                                                                                                              |
		| {/list}         | ["/red,green,blue"]                                                                                                                                                       |
		| {/list*}        | ["/red/green/blue"]                                                                                                                                                       |
		| {/list*,path:4} | ["/red/green/blue/%2Ffoo"]                                                                                                                                                |
		| {/keys}         | ["/comma,%2C,dot,.,semi,%3B","/comma,%2C,semi,%3B,dot,.","/dot,.,comma,%2C,semi,%3B","/dot,.,semi,%3B,comma,%2C","/semi,%3B,comma,%2C,dot,.","/semi,%3B,dot,.,comma,%2C"] |
		| {/keys*}        | ["/comma=%2C/dot=./semi=%3B","/comma=%2C/semi=%3B/dot=.","/dot=./comma=%2C/semi=%3B","/dot=./semi=%3B/comma=%2C","/semi=%3B/comma=%2C/dot=.","/semi=%3B/dot=./comma=%2C"] |

Scenario Outline: 3.2.7 Path-Style Parameter Expansion at level 0
	Given the variables
		| name       | value                              |
		| count      | ["one","two","three"]              |
		| dom        | ["example","com"]                  |
		| dub        | "me/too"                           |
		| hello      | "Hello World!"                     |
		| half       | "50%"                              |
		| var        | "value"                            |
		| who        | "fred"                             |
		| base       | "http://example.com/home/"         |
		| path       | "/foo/bar"                         |
		| list       | ["red","green","blue"]             |
		| keys       | {"semi":";","dot":".","comma":","} |
		| v          | "6"                                |
		| x          | "1024"                             |
		| y          | "768"                              |
		| empty      | ""                                 |
		| empty_keys | []                                 |
		| undef      | null                               |
	When I apply the variables to the template <template>
	Then the result should be one of <result>

	Examples:
		| template       | result                                                                                                                                                                                                  |
		| {;who}         | [";who=fred"]                                                                                                                                                                                           |
		| {;half}        | [";half=50%25"]                                                                                                                                                                                         |
		| {;empty}       | [";empty"]                                                                                                                                                                                              |
		| {;hello:5}     | [";hello=Hello"]                                                                                                                                                                                        |
		| {;v,empty,who} | [";v=6;empty;who=fred"]                                                                                                                                                                                 |
		| {;v,bar,who}   | [";v=6;who=fred"]                                                                                                                                                                                       |
		| {;x,y}         | [";x=1024;y=768"]                                                                                                                                                                                       |
		| {;x,y,empty}   | [";x=1024;y=768;empty"]                                                                                                                                                                                 |
		| {;x,y,undef}   | [";x=1024;y=768"]                                                                                                                                                                                       |
		| {;list}        | [";list=red,green,blue"]                                                                                                                                                                                |
		| {;list*}       | [";list=red;list=green;list=blue"]                                                                                                                                                                      |
		| {;keys}        | [";keys=comma,%2C,dot,.,semi,%3B",";keys=comma,%2C,semi,%3B,dot,.",";keys=dot,.,comma,%2C,semi,%3B",";keys=dot,.,semi,%3B,comma,%2C",";keys=semi,%3B,comma,%2C,dot,.",";keys=semi,%3B,dot,.,comma,%2C"] |
		| {;keys*}       | [";comma=%2C;dot=.;semi=%3B",";comma=%2C;semi=%3B;dot=.",";dot=.;comma=%2C;semi=%3B",";dot=.;semi=%3B;comma=%2C",";semi=%3B;comma=%2C;dot=.",";semi=%3B;dot=.;comma=%2C"]                               |

Scenario Outline: 3.2.8 Form-Style Query Expansion at level 0
	Given the variables
		| name       | value                              |
		| count      | ["one","two","three"]              |
		| dom        | ["example","com"]                  |
		| dub        | "me/too"                           |
		| hello      | "Hello World!"                     |
		| half       | "50%"                              |
		| var        | "value"                            |
		| who        | "fred"                             |
		| base       | "http://example.com/home/"         |
		| path       | "/foo/bar"                         |
		| list       | ["red","green","blue"]             |
		| keys       | {"semi":";","dot":".","comma":","} |
		| v          | "6"                                |
		| x          | "1024"                             |
		| y          | "768"                              |
		| empty      | ""                                 |
		| empty_keys | []                                 |
		| undef      | null                               |
	When I apply the variables to the template <template>
	Then the result should be one of <result>

	Examples:
		| template     | result                                                                                                                                                                                                                                |
		| {?who}       | ["?who=fred"]                                                                                                                                                                                                                         |
		| {?half}      | ["?half=50%25"]                                                                                                                                                                                                                       |
		| {?x,y}       | ["?x=1024\u0026y=768"]                                                                                                                                                                                                                |
		| {?x,y,empty} | ["?x=1024\u0026y=768\u0026empty="]                                                                                                                                                                                                    |
		| {?x,y,undef} | ["?x=1024\u0026y=768"]                                                                                                                                                                                                                |
		| {?var:3}     | ["?var=val"]                                                                                                                                                                                                                          |
		| {?list}      | ["?list=red,green,blue"]                                                                                                                                                                                                              |
		| {?list*}     | ["?list=red\u0026list=green\u0026list=blue"]                                                                                                                                                                                          |
		| {?keys}      | ["?keys=comma,%2C,dot,.,semi,%3B","?keys=comma,%2C,semi,%3B,dot,.","?keys=dot,.,comma,%2C,semi,%3B","?keys=dot,.,semi,%3B,comma,%2C","?keys=semi,%3B,comma,%2C,dot,.","?keys=semi,%3B,dot,.,comma,%2C"]                               |
		| {?keys*}     | ["?comma=%2C\u0026dot=.\u0026semi=%3B","?comma=%2C\u0026semi=%3B\u0026dot=.","?dot=.\u0026comma=%2C\u0026semi=%3B","?dot=.\u0026semi=%3B\u0026comma=%2C","?semi=%3B\u0026comma=%2C\u0026dot=.","?semi=%3B\u0026dot=.\u0026comma=%2C"] |

Scenario Outline: 3.2.9 Form-Style Query Continuation at level 0
	Given the variables
		| name       | value                              |
		| count      | ["one","two","three"]              |
		| dom        | ["example","com"]                  |
		| dub        | "me/too"                           |
		| hello      | "Hello World!"                     |
		| half       | "50%"                              |
		| var        | "value"                            |
		| who        | "fred"                             |
		| base       | "http://example.com/home/"         |
		| path       | "/foo/bar"                         |
		| list       | ["red","green","blue"]             |
		| keys       | {"semi":";","dot":".","comma":","} |
		| v          | "6"                                |
		| x          | "1024"                             |
		| y          | "768"                              |
		| empty      | ""                                 |
		| empty_keys | []                                 |
		| undef      | null                               |
	When I apply the variables to the template <template>
	Then the result should be one of <result>

	Examples:
		| template       | result                                                                                                                                                                                                                                                              |
		| {&who}         | ["\u0026who=fred"]                                                                                                                                                                                                                                                  |
		| {&half}        | ["\u0026half=50%25"]                                                                                                                                                                                                                                                |
		| ?fixed=yes{&x} | ["?fixed=yes\u0026x=1024"]                                                                                                                                                                                                                                          |
		| {&var:3}       | ["\u0026var=val"]                                                                                                                                                                                                                                                   |
		| {&x,y,empty}   | ["\u0026x=1024\u0026y=768\u0026empty="]                                                                                                                                                                                                                             |
		| {&x,y,undef}   | ["\u0026x=1024\u0026y=768"]                                                                                                                                                                                                                                         |
		| {&list}        | ["\u0026list=red,green,blue"]                                                                                                                                                                                                                                       |
		| {&list*}       | ["\u0026list=red\u0026list=green\u0026list=blue"]                                                                                                                                                                                                                   |
		| {&keys}        | ["\u0026keys=comma,%2C,dot,.,semi,%3B","\u0026keys=comma,%2C,semi,%3B,dot,.","\u0026keys=dot,.,comma,%2C,semi,%3B","\u0026keys=dot,.,semi,%3B,comma,%2C","\u0026keys=semi,%3B,comma,%2C,dot,.","\u0026keys=semi,%3B,dot,.,comma,%2C"]                               |
		| {&keys*}       | ["\u0026comma=%2C\u0026dot=.\u0026semi=%3B","\u0026comma=%2C\u0026semi=%3B\u0026dot=.","\u0026dot=.\u0026comma=%2C\u0026semi=%3B","\u0026dot=.\u0026semi=%3B\u0026comma=%2C","\u0026semi=%3B\u0026comma=%2C\u0026dot=.","\u0026semi=%3B\u0026dot=.\u0026comma=%2C"] |