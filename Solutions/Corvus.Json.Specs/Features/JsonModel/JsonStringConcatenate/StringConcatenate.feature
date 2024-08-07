Feature: JsonStringConcatenate
	Concatenate multiple JSON values into a json string

Scenario Outline: Concatenate multiple JSON values to a JSON string value
	Given the JsonElement backed JsonArray <jsonValue>
	When the values are concatenated to a JsonString
	And the values are concatenated to a JsonDate
	And the values are concatenated to a JsonDateTime
	And the values are concatenated to a JsonDuration
	And the values are concatenated to a JsonEmail
	And the values are concatenated to a JsonHostname
	And the values are concatenated to a JsonIdnEmail
	And the values are concatenated to a JsonIdnHostname
	And the values are concatenated to a JsonIpV4
	And the values are concatenated to a JsonIpV6
	And the values are concatenated to a JsonIri
	And the values are concatenated to a JsonIriReference
	And the values are concatenated to a JsonPointer
	And the values are concatenated to a JsonRegex
	And the values are concatenated to a JsonRelativePointer
	And the values are concatenated to a JsonTime
	And the values are concatenated to a JsonUri
	And the values are concatenated to a JsonUriReference
	And the values are concatenated to a JsonUuid
	And the values are concatenated to a JsonContent
	And the values are concatenated to a JsonContentPre201909
	And the values are concatenated to a JsonBase64Content
	And the values are concatenated to a JsonBase64String
	And the values are concatenated to a JsonBase64StringPre201909


	Then the results should be equal to the JsonString <result>

Examples:
	| jsonValue                                                                                        | result                                                            |
	| ["Hello", "world"]                                                                               | Helloworld                                                        |
	| [true, "world"]                                                                                  | trueworld                                                         |
	| ["Hello", true]                                                                                  | Hellotrue                                                         |
	| [3.0, "world"]                                                                                   | 3.0world                                                          |
	| ["Hello", 3.0]                                                                                   | Hello3.0                                                          |
	| [null, "world"]                                                                                  | nullworld                                                         |
	| ["Hello", null]                                                                                  | Hellonull                                                         |
	| [[1,2,3], "world"]                                                                               | [1,2,3]world                                                      |
	| ["Hello", [1,2,3]]                                                                               | Hello[1,2,3]                                                      |
	| [{"foo": 3}, "world"]                                                                            | {"foo":3}world                                                    |
	| ["Hello", {"foo": 3}]                                                                            | Hello{"foo":3}                                                    |
	| ["Hello", "there", "world"]                                                                      | Hellothereworld                                                   |
	| [true, "there", "world"]                                                                         | truethereworld                                                    |
	| ["Hello", "there", true]                                                                         | Hellotheretrue                                                    |
	| [3.0, "there", "world"]                                                                          | 3.0thereworld                                                     |
	| ["Hello", "there", 3.0]                                                                          | Hellothere3.0                                                     |
	| [null, "there", "world"]                                                                         | nullthereworld                                                    |
	| ["Hello", "there", null]                                                                         | Hellotherenull                                                    |
	| [[1,2,3], "there", "world"]                                                                      | [1,2,3]thereworld                                                 |
	| ["Hello", "there", [1,2,3]]                                                                      | Hellothere[1,2,3]                                                 |
	| [{"foo": 3}, "there", "world"]                                                                   | {"foo":3}thereworld                                               |
	| ["Hello", "there", {"foo": 3}]                                                                   | Hellothere{"foo":3}                                               |
	| ["Hello", "there", "again", "world"]                                                             | Hellothereagainworld                                              |
	| [true, "there", "again", "world"]                                                                | truethereagainworld                                               |
	| ["Hello", "there", "again", true]                                                                | Hellothereagaintrue                                               |
	| [3.0, "there", "again", "world"]                                                                 | 3.0thereagainworld                                                |
	| ["Hello", "there", "again", 3.0]                                                                 | Hellothereagain3.0                                                |
	| [null, "there", "again", "world"]                                                                | nullthereagainworld                                               |
	| ["Hello", "there", "again", null]                                                                | Hellothereagainnull                                               |
	| [[1,2,3], "there", "again", "world"]                                                             | [1,2,3]thereagainworld                                            |
	| ["Hello", "there", "again", [1,2,3]]                                                             | Hellothereagain[1,2,3]                                            |
	| [{"foo": 3}, "there", "again", "world"]                                                          | {"foo":3}thereagainworld                                          |
	| ["Hello", "there", "again", {"foo": 3}]                                                          | Hellothereagain{"foo":3}                                          |
	| ["Hello", "there", "again", "beautiful", "world"]                                                | Hellothereagainbeautifulworld                                     |
	| [true, "there", "again", "beautiful", "world"]                                                   | truethereagainbeautifulworld                                      |
	| ["Hello", "there", "again", "beautiful", true]                                                   | Hellothereagainbeautifultrue                                      |
	| [3.0, "there", "again", "beautiful", "world"]                                                    | 3.0thereagainbeautifulworld                                       |
	| ["Hello", "there", "again", "beautiful", 3.0]                                                    | Hellothereagainbeautiful3.0                                       |
	| [null, "there", "again", "beautiful", "world"]                                                   | nullthereagainbeautifulworld                                      |
	| ["Hello", "there", "again", "beautiful", null]                                                   | Hellothereagainbeautifulnull                                      |
	| [[1,2,3], "there", "again", "beautiful", "world"]                                                | [1,2,3]thereagainbeautifulworld                                   |
	| ["Hello", "there", "again", "beautiful", [1,2,3]]                                                | Hellothereagainbeautiful[1,2,3]                                   |
	| [{"foo": 3}, "there", "again", "beautiful", "world"]                                             | {"foo":3}thereagainbeautifulworld                                 |
	| ["Hello", "there", "again", "beautiful", {"foo": 3}]                                             | Hellothereagainbeautiful{"foo":3}                                 |
	| ["Hello", "there", "again", "beautiful", "extraordinary", "world"]                               | Hellothereagainbeautifulextraordinaryworld                        |
	| [true, "there", "again", "beautiful", "extraordinary", "world"]                                  | truethereagainbeautifulextraordinaryworld                         |
	| ["Hello", "there", "again", "beautiful", "extraordinary", true]                                  | Hellothereagainbeautifulextraordinarytrue                         |
	| [3.0, "there", "again", "beautiful", "extraordinary", "world"]                                   | 3.0thereagainbeautifulextraordinaryworld                          |
	| ["Hello", "there", "again", "beautiful", "extraordinary", 3.0]                                   | Hellothereagainbeautifulextraordinary3.0                          |
	| [null, "there", "again", "beautiful", "extraordinary", "world"]                                  | nullthereagainbeautifulextraordinaryworld                         |
	| ["Hello", "there", "again", "beautiful", "extraordinary", null]                                  | Hellothereagainbeautifulextraordinarynull                         |
	| [[1,2,3], "there", "again", "beautiful", "extraordinary", "world"]                               | [1,2,3]thereagainbeautifulextraordinaryworld                      |
	| ["Hello", "there", "again", "beautiful", "extraordinary", [1,2,3]]                               | Hellothereagainbeautifulextraordinary[1,2,3]                      |
	| [{"foo": 3}, "there", "again", "beautiful", "extraordinary", "world"]                            | {"foo":3}thereagainbeautifulextraordinaryworld                    |
	| ["Hello", "there", "again", "beautiful", "extraordinary", {"foo": 3}]                            | Hellothereagainbeautifulextraordinary{"foo":3}                    |
	| ["Hello", "there", "again", "beautiful", "extraordinary", "magnificent", "world"]                | Hellothereagainbeautifulextraordinarymagnificentworld             |
	| [true, "there", "again", "beautiful", "extraordinary", "magnificent", "world"]                   | truethereagainbeautifulextraordinarymagnificentworld              |
	| ["Hello", "there", "again", "beautiful", "extraordinary", "magnificent", true]                   | Hellothereagainbeautifulextraordinarymagnificenttrue              |
	| [3.0, "there", "again", "beautiful", "extraordinary", "magnificent", "world"]                    | 3.0thereagainbeautifulextraordinarymagnificentworld               |
	| ["Hello", "there", "again", "beautiful", "extraordinary", "magnificent", 3.0]                    | Hellothereagainbeautifulextraordinarymagnificent3.0               |
	| [null, "there", "again", "beautiful", "extraordinary", "magnificent", "world"]                   | nullthereagainbeautifulextraordinarymagnificentworld              |
	| ["Hello", "there", "again", "beautiful", "extraordinary", "magnificent", null]                   | Hellothereagainbeautifulextraordinarymagnificentnull              |
	| [[1,2,3], "there", "again", "beautiful", "extraordinary", "magnificent", "world"]                | [1,2,3]thereagainbeautifulextraordinarymagnificentworld           |
	| ["Hello", "there", "again", "beautiful", "extraordinary", "magnificent", [1,2,3]]                | Hellothereagainbeautifulextraordinarymagnificent[1,2,3]           |
	| [{"foo": 3}, "there", "again", "beautiful", "extraordinary", "magnificent", "world"]             | {"foo":3}thereagainbeautifulextraordinarymagnificentworld         |
	| ["Hello", "there", "again", "beautiful", "extraordinary", "magnificent", {"foo": 3}]             | Hellothereagainbeautifulextraordinarymagnificent{"foo":3}         |
	| ["Hello", "there", "again", "beautiful", "extraordinary", "magnificent", "glorious", "world"]    | Hellothereagainbeautifulextraordinarymagnificentgloriousworld     |
	| [true, "there", "again", "beautiful", "extraordinary", "magnificent", "glorious", "world"]       | truethereagainbeautifulextraordinarymagnificentgloriousworld      |
	| ["Hello", "there", "again", "beautiful", "extraordinary", "magnificent", "glorious", true]       | Hellothereagainbeautifulextraordinarymagnificentglorioustrue      |
	| [3.0, "there", "again", "beautiful", "extraordinary", "magnificent", "glorious", "world"]        | 3.0thereagainbeautifulextraordinarymagnificentgloriousworld       |
	| ["Hello", "there", "again", "beautiful", "extraordinary", "magnificent", "glorious", 3.0]        | Hellothereagainbeautifulextraordinarymagnificentglorious3.0       |
	| [null, "there", "again", "beautiful", "extraordinary", "magnificent", "glorious", "world"]       | nullthereagainbeautifulextraordinarymagnificentgloriousworld      |
	| ["Hello", "there", "again", "beautiful", "extraordinary", "magnificent", "glorious", null]       | Hellothereagainbeautifulextraordinarymagnificentgloriousnull      |
	| [[1,2,3], "there", "again", "beautiful", "extraordinary", "magnificent", "glorious", "world"]    | [1,2,3]thereagainbeautifulextraordinarymagnificentgloriousworld   |
	| ["Hello", "there", "again", "beautiful", "extraordinary", "magnificent", "glorious", [1,2,3]]    | Hellothereagainbeautifulextraordinarymagnificentglorious[1,2,3]   |
	| [{"foo": 3}, "there", "again", "beautiful", "extraordinary", "magnificent", "glorious", "world"] | {"foo":3}thereagainbeautifulextraordinarymagnificentgloriousworld |
	| ["Hello", "there", "again", "beautiful", "extraordinary", "magnificent", "glorious", {"foo": 3}] | Hellothereagainbeautifulextraordinarymagnificentglorious{"foo":3} |
	| ["there", true, "world"]                                                                         | theretrueworld                                                    |
	| ["there", 3.0, "world"]                                                                          | there3.0world                                                     |
	| ["there", null, "world"]                                                                         | therenullworld                                                    |
	| ["there", [1,2,3], "world"]                                                                      | there[1,2,3]world                                                 |
	| ["there", {"foo": 3}, "world"]                                                                   | there{"foo":3}world                                               |