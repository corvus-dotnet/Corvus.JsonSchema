
Feature: tests

Scenario: empty list, empty docs [3bd3f18f-2cd3-4b29-b192-e75c9ec7dfd9]
	Given the document {}
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal {}

Scenario: empty patch list [53af56c6-b311-4126-a874-e3b02effffb8]
	Given the document {"foo":1}
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: rearrangements OK? [e2595ded-6d8c-406e-ae51-0c4725c884c0]
	Given the document {"foo":1,"bar":2}
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal {"bar":2,"foo":1}

Scenario: rearrangements OK?  How about one level down ... array [c908998d-1bfe-47ff-a880-65d3f67e6823]
	Given the document [{"foo":1,"bar":2}]
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal [{"bar":2,"foo":1}]

Scenario: rearrangements OK?  How about one level down... [cab7431c-67d0-421f-abd2-4446d650a52d]
	Given the document {"foo":{"foo":1,"bar":2}}
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal {"foo":{"bar":2,"foo":1}}

Scenario: add replaces any existing field [6b99e9fd-a604-4fcc-a558-9ea9053975fa]
	Given the document {"foo":null}
	And the patch [{"op":"add","path":"/foo","value":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: toplevel array [1fa3a78b-b5c3-4ccd-a8fa-cfbf05190fae]
	Given the document []
	And the patch [{"op":"add","path":"/0","value":"foo"}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo"]

Scenario: toplevel array, no change [6b3f6375-624c-41cc-9446-53c4c306af72]
	Given the document ["foo"]
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal ["foo"]

Scenario: toplevel object, numeric string [9270e397-a791-4196-98e9-7748b731d134]
	Given the document {}
	And the patch [{"op":"add","path":"/foo","value":"1"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"1"}

Scenario: toplevel object, integer [74d218e7-6c5c-4c8d-ab05-6032c38ab9a0]
	Given the document {}
	And the patch [{"op":"add","path":"/foo","value":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: replace object document with array document? [b8c353d1-fad4-47fb-9447-741034b5192d]
	Given the document {}
	And the patch [{"op":"add","path":"","value":[]}]
	When I apply the patch to the document
	Then the transformed document should equal []

Scenario: replace array document with object document? [3594d553-75d7-408f-90ba-4fe76313aa92]
	Given the document []
	And the patch [{"op":"add","path":"","value":{}}]
	When I apply the patch to the document
	Then the transformed document should equal {}

Scenario: append to root array document? [8bdfd67f-13f1-4354-b417-f1645aeb21bb]
	Given the document []
	And the patch [{"op":"add","path":"/-","value":"hi"}]
	When I apply the patch to the document
	Then the transformed document should equal ["hi"]

Scenario: Add, / target [0497b84c-8e27-48c4-80a0-bbe95cec4b35]
	Given the document {}
	And the patch [{"op":"add","path":"/","value":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"":1}

Scenario: Add, /foo/ deep target (trailing slash) [572b15f4-1fa1-44af-a534-9a6a1ef49dbf]
	Given the document {"foo":{}}
	And the patch [{"op":"add","path":"/foo/","value":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":{"":1}}

Scenario: Add composite value at top level [03b17d0b-dcdc-409c-8cd5-efdf1ebdb039]
	Given the document {"foo":1}
	And the patch [{"op":"add","path":"/bar","value":[1,2]}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"bar":[1,2]}

Scenario: Add into composite value [6247959a-8aaf-4de2-be8e-2d4ee6036e0e]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	And the patch [{"op":"add","path":"/baz/0/foo","value":"world"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"baz":[{"qux":"hello","foo":"world"}]}

Scenario: Undescribed scenario [66b36eeb-d02d-401a-bafd-01af7f98482d]
	Given the document {"bar":[1,2]}
	And the patch [{"op":"add","path":"/bar/8","value":"5"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Undescribed scenario [47239f43-de7d-44ea-8885-2c902a2f838e]
	Given the document {"bar":[1,2]}
	And the patch [{"op":"add","path":"/bar/-1","value":"5"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Undescribed scenario [89c44882-3ddd-4432-b597-48512ee70462]
	Given the document {"foo":1}
	And the patch [{"op":"add","path":"/bar","value":true}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"bar":true}

Scenario: Undescribed scenario [445fc9ba-4fb3-46af-b641-2d2c660b4e30]
	Given the document {"foo":1}
	And the patch [{"op":"add","path":"/bar","value":false}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"bar":false}

Scenario: Undescribed scenario [5b6336b8-44cf-4327-904e-98cdb90a1385]
	Given the document {"foo":1}
	And the patch [{"op":"add","path":"/bar","value":null}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"bar":null}

Scenario: 0 can be an array index or object element name [a6ce525a-cca2-48f1-8d14-dea32267ef52]
	Given the document {"foo":1}
	And the patch [{"op":"add","path":"/0","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"0":"bar"}

Scenario: Undescribed scenario [ad627842-00be-432c-add2-8be3aaba96f2]
	Given the document ["foo"]
	And the patch [{"op":"add","path":"/1","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo","bar"]

Scenario: Undescribed scenario [78e74c83-1b64-4541-bb60-4e5d9d068714]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/1","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo","bar","sil"]

Scenario: Undescribed scenario [b1b1c000-c4a5-4a02-86fc-f8d5d4d96455]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/0","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal ["bar","foo","sil"]

Scenario: push item to array via last index + 1 [ce2e5d29-1086-40d3-a0b8-9fcf5f149948]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/2","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo","sil","bar"]

Scenario: add item to array at index > length should fail [60b9050b-cdc4-41ec-b699-76bbde40e675]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/3","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test against implementation-specific numeric parsing [f0ff3906-2ba9-4b10-90a4-334635abfd1f]
	Given the document {"1e0":"foo"}
	And the patch [{"op":"test","path":"/1e0","value":"foo"}]
	When I apply the patch to the document
	Then the transformed document should equal {"1e0":"foo"}

Scenario: test with bad number should fail [a7be816f-e68d-4b42-9392-db30c1306b9d]
	Given the document ["foo","bar"]
	And the patch [{"op":"test","path":"/1e0","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Undescribed scenario [22fed12b-f9a4-4cbf-b324-7a54f81a8cff]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/bar","value":42}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: value in array add not flattened [294736fd-fca5-4b1b-a9b1-fe9c65771544]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/1","value":["bar","baz"]}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo",["bar","baz"],"sil"]

Scenario: Undescribed scenario [f367b31c-b678-4049-bb44-43bf7c6a04b7]
	Given the document {"foo":1,"bar":[1,2,3,4]}
	And the patch [{"op":"remove","path":"/bar"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: Undescribed scenario [3394e6a6-920a-4c53-a560-3a516fc859b4]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	And the patch [{"op":"remove","path":"/baz/0/qux"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"baz":[{}]}

Scenario: Undescribed scenario [ff4c93dd-49ca-41a9-80f3-1855bfc384f5]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	And the patch [{"op":"replace","path":"/foo","value":[1,2,3,4]}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":[1,2,3,4],"baz":[{"qux":"hello"}]}

Scenario: Undescribed scenario [dc891463-65bf-411e-99d5-412f516b35c0]
	Given the document {"foo":[1,2,3,4],"baz":[{"qux":"hello"}]}
	And the patch [{"op":"replace","path":"/baz/0/qux","value":"world"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":[1,2,3,4],"baz":[{"qux":"world"}]}

Scenario: Undescribed scenario [8e89d7bb-89fa-49d3-a2f6-9bc353b2caf7]
	Given the document ["foo"]
	And the patch [{"op":"replace","path":"/0","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal ["bar"]

Scenario: Undescribed scenario [8ce7235e-3301-4d5f-8a19-51b8144207f0]
	Given the document [""]
	And the patch [{"op":"replace","path":"/0","value":0}]
	When I apply the patch to the document
	Then the transformed document should equal [0]

Scenario: Undescribed scenario [b2f60b7d-ea2d-4f3a-a2fa-25de87775d7f]
	Given the document [""]
	And the patch [{"op":"replace","path":"/0","value":true}]
	When I apply the patch to the document
	Then the transformed document should equal [true]

Scenario: Undescribed scenario [fec282f2-e808-4815-bf4a-0000043cc5b0]
	Given the document [""]
	And the patch [{"op":"replace","path":"/0","value":false}]
	When I apply the patch to the document
	Then the transformed document should equal [false]

Scenario: Undescribed scenario [f261f08f-092d-486f-b529-baf018620e95]
	Given the document [""]
	And the patch [{"op":"replace","path":"/0","value":null}]
	When I apply the patch to the document
	Then the transformed document should equal [null]

Scenario: value in array replace not flattened [ef218964-211a-4c19-a115-c7d47ab66efd]
	Given the document ["foo","sil"]
	And the patch [{"op":"replace","path":"/1","value":["bar","baz"]}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo",["bar","baz"]]

Scenario: replace whole document [2265e8d4-a3ca-44fa-9424-863aac12177b]
	Given the document {"foo":"bar"}
	And the patch [{"op":"replace","path":"","value":{"baz":"qux"}}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":"qux"}

Scenario: test replace with missing parent key should fail [b3a7ebf1-f521-4f91-b93d-0ed4a3bceb0b]
	Given the document {"bar":"baz"}
	And the patch [{"op":"replace","path":"/foo/bar","value":false}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: spurious patch properties [93a6524d-8c73-47db-862a-944539965601]
	Given the document {"foo":1}
	And the patch [{"op":"test","path":"/foo","value":1,"spurious":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: null value should be valid obj property [d8b603ec-d930-4ab5-8abb-6cf2fdda56f9]
	Given the document {"foo":null}
	And the patch [{"op":"test","path":"/foo","value":null}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":null}

Scenario: null value should be valid obj property to be replaced with something truthy [7340e482-6cfb-47c5-8bb7-e45b006ad46a]
	Given the document {"foo":null}
	And the patch [{"op":"replace","path":"/foo","value":"truthy"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"truthy"}

Scenario: null value should be valid obj property to be moved [c4cdc00f-c9a7-4a20-8a6a-a9e1800b22f7]
	Given the document {"foo":null}
	And the patch [{"op":"move","from":"/foo","path":"/bar"}]
	When I apply the patch to the document
	Then the transformed document should equal {"bar":null}

Scenario: null value should be valid obj property to be copied [93450980-2de6-4ada-a42e-17394f6f1bf4]
	Given the document {"foo":null}
	And the patch [{"op":"copy","from":"/foo","path":"/bar"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":null,"bar":null}

Scenario: null value should be valid obj property to be removed [42dbaf47-7820-440f-92d9-2ea963ae732b]
	Given the document {"foo":null}
	And the patch [{"op":"remove","path":"/foo"}]
	When I apply the patch to the document
	Then the transformed document should equal {}

Scenario: null value should still be valid obj property replace other value [bb7e112c-57bb-4a7e-8d48-cf3eb84b3322]
	Given the document {"foo":"bar"}
	And the patch [{"op":"replace","path":"/foo","value":null}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":null}

Scenario: test should pass despite rearrangement [fd68644f-9a95-4ca6-82ad-d25ac2be4aaf]
	Given the document {"foo":{"foo":1,"bar":2}}
	And the patch [{"op":"test","path":"/foo","value":{"bar":2,"foo":1}}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":{"foo":1,"bar":2}}

Scenario: test should pass despite (nested) rearrangement [42eefdb5-bc58-4ed7-9044-4d49b03b6266]
	Given the document {"foo":[{"foo":1,"bar":2}]}
	And the patch [{"op":"test","path":"/foo","value":[{"bar":2,"foo":1}]}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":[{"foo":1,"bar":2}]}

Scenario: test should pass - no error [9fcb06f4-6638-4a98-9001-a580c840d9af]
	Given the document {"foo":{"bar":[1,2,5,4]}}
	And the patch [{"op":"test","path":"/foo","value":{"bar":[1,2,5,4]}}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":{"bar":[1,2,5,4]}}

Scenario: Undescribed scenario [6a14a9bb-ef37-48c9-9d95-0911c3c19284]
	Given the document {"foo":{"bar":[1,2,5,4]}}
	And the patch [{"op":"test","path":"/foo","value":[1,2]}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Empty-string element [07939ea8-77c8-4b85-b423-665ecfb7615e]
	Given the document {"":1}
	And the patch [{"op":"test","path":"/","value":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"":1}

Scenario: Undescribed scenario [cad75706-88bb-4d24-a3d9-5ed172a8987d]
	Given the document {"foo":["bar","baz"],"":0,"a/b":1,"c%d":2,"e^f":3,"g|h":4,"i\\j":5,"k\u0022l":6," ":7,"m~n":8}
	And the patch [{"op":"test","path":"/foo","value":["bar","baz"]},{"op":"test","path":"/foo/0","value":"bar"},{"op":"test","path":"/","value":0},{"op":"test","path":"/a~1b","value":1},{"op":"test","path":"/c%d","value":2},{"op":"test","path":"/e^f","value":3},{"op":"test","path":"/g|h","value":4},{"op":"test","path":"/i\\j","value":5},{"op":"test","path":"/k\u0022l","value":6},{"op":"test","path":"/ ","value":7},{"op":"test","path":"/m~0n","value":8}]
	When I apply the patch to the document
	Then the transformed document should equal {"":0," ":7,"a/b":1,"c%d":2,"e^f":3,"foo":["bar","baz"],"g|h":4,"i\\j":5,"k\u0022l":6,"m~n":8}

Scenario: Move to same location has no effect [752ef9ba-1235-446f-8c1d-ea8246910ba2]
	Given the document {"foo":1}
	And the patch [{"op":"move","from":"/foo","path":"/foo"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: Undescribed scenario [d512187b-aab6-4798-bbb8-f8a15497c2a8]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	And the patch [{"op":"move","from":"/foo","path":"/bar"}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":[{"qux":"hello"}],"bar":1}

Scenario: Undescribed scenario [d4ba2028-4420-4751-ad53-8176c15055dc]
	Given the document {"baz":[{"qux":"hello"}],"bar":1}
	And the patch [{"op":"move","from":"/baz/0/qux","path":"/baz/1"}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":[{},"hello"],"bar":1}

Scenario: Undescribed scenario [a51b39d2-f4f2-4baf-99a4-b498228619ff]
	Given the document {"baz":[{"qux":"hello"}],"bar":1}
	And the patch [{"op":"copy","from":"/baz/0","path":"/boo"}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":[{"qux":"hello"}],"bar":1,"boo":{"qux":"hello"}}

Scenario: replacing the root of the document is possible with add [ceb145f5-b536-42fa-988b-89f896ab2dee]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"","value":{"baz":"qux"}}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":"qux"}

Scenario: Adding to "/-" adds to the end of the array [3afa1546-16cd-427e-a951-e15f45caa8b7]
	Given the document [1,2]
	And the patch [{"op":"add","path":"/-","value":{"foo":["bar","baz"]}}]
	When I apply the patch to the document
	Then the transformed document should equal [1,2,{"foo":["bar","baz"]}]

Scenario: Adding to "/-" adds to the end of the array, even n levels down [2152083f-864f-49d0-8406-41e16609177c]
	Given the document [1,2,[3,[4,5]]]
	And the patch [{"op":"add","path":"/2/1/-","value":{"foo":["bar","baz"]}}]
	When I apply the patch to the document
	Then the transformed document should equal [1,2,[3,[4,5,{"foo":["bar","baz"]}]]]

Scenario: test remove with bad number should fail [15ef5728-2e4d-46f7-9821-7472ebdf15ca]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	And the patch [{"op":"remove","path":"/baz/1e0/qux"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test remove on array [b473e18e-7329-4500-892c-92c681ed3d79]
	Given the document [1,2,3,4]
	And the patch [{"op":"remove","path":"/0"}]
	When I apply the patch to the document
	Then the transformed document should equal [2,3,4]

Scenario: test repeated removes [bd10c562-fc10-4443-af16-44434c366c02]
	Given the document [1,2,3,4]
	And the patch [{"op":"remove","path":"/1"},{"op":"remove","path":"/2"}]
	When I apply the patch to the document
	Then the transformed document should equal [1,3]

Scenario: test remove with bad index should fail [1b76b028-d810-423b-86be-66dcaa1771cc]
	Given the document [1,2,3,4]
	And the patch [{"op":"remove","path":"/1e0"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test replace with bad number should fail [e093c368-5fb1-4365-beba-863fd31a00f2]
	Given the document [""]
	And the patch [{"op":"replace","path":"/1e0","value":false}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test copy with bad number should fail [392874e7-b537-4973-8b0f-1db04f8d7f90]
	Given the document {"baz":[1,2,3],"bar":1}
	And the patch [{"op":"copy","from":"/baz/1e0","path":"/boo"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test move with bad number should fail [035eaba5-1187-45d9-8acf-146e55ec3760]
	Given the document {"foo":1,"baz":[1,2,3,4]}
	And the patch [{"op":"move","from":"/baz/1e0","path":"/foo"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test add with bad number should fail [023296fa-2442-44d1-8dae-fb53b552ce7a]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/1e0","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing 'path' parameter [8a13b7a8-0166-4c33-8141-51a866cd78f1]
	Given the document {}
	And the patch [{"op":"add","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: 'path' parameter with null value [3262f87a-df38-42d8-85f9-c089fe07e574]
	Given the document {}
	And the patch [{"op":"add","path":null,"value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: invalid JSON Pointer token [4c045186-98ae-4c5b-9f4c-7321e03323e7]
	Given the document {}
	And the patch [{"op":"add","path":"foo","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing 'value' parameter to add [32a972e0-1480-4776-9622-92bbfbf7b447]
	Given the document [1]
	And the patch [{"op":"add","path":"/-"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing 'value' parameter to replace [aec99300-1e39-427b-8ef9-2f606f9d8156]
	Given the document [1]
	And the patch [{"op":"replace","path":"/0"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing 'value' parameter to test [7b23db48-2540-4df7-b243-a96a8141bdd7]
	Given the document [null]
	And the patch [{"op":"test","path":"/0"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing value parameter to test - where undef is falsy [9866f0a0-2e8e-40ee-a1e8-1376411ba02f]
	Given the document [false]
	And the patch [{"op":"test","path":"/0"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing from parameter to copy [cda9a168-ea33-4884-a2a5-113d851e4626]
	Given the document [1]
	And the patch [{"op":"copy","path":"/-"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing from location to copy [0177e003-c6b3-427d-aed9-7390cf7dd5d2]
	Given the document {"foo":1}
	And the patch [{"op":"copy","from":"/bar","path":"/foo"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing from parameter to move [b2ad961e-b32e-4aa2-8cf1-d1567d388d93]
	Given the document {"foo":1}
	And the patch [{"op":"move","path":""}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing from location to move [6c3a9f97-fe3a-4e11-b455-ed8aa50ade3c]
	Given the document {"foo":1}
	And the patch [{"op":"move","from":"/bar","path":"/foo"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: unrecognized op should fail [325febf0-d671-45be-b5fe-936c3faca8a7]
	Given the document {"foo":1}
	And the patch [{"op":"spam","path":"/foo","value":1}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test with bad array number that has leading zeros [90dd249c-4adf-41d9-a6fc-1f089d73e5fd]
	Given the document ["foo","bar"]
	And the patch [{"op":"test","path":"/00","value":"foo"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test with bad array number that has leading zeros [bb96bc18-0c28-48bb-a9a0-9221477b83ea]
	Given the document ["foo","bar"]
	And the patch [{"op":"test","path":"/01","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Removing nonexistent field [5c0749e0-67d9-46af-b767-d1b75a5d35f0]
	Given the document {"foo":"bar"}
	And the patch [{"op":"remove","path":"/baz"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Removing deep nonexistent path [742fa1c7-3df5-47fd-bd89-52be5e790674]
	Given the document {"foo":"bar"}
	And the patch [{"op":"remove","path":"/missing1/missing2"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Removing nonexistent index [7fafcde9-9ccd-4190-9b47-f2346dcbacc9]
	Given the document ["foo","bar"]
	And the patch [{"op":"remove","path":"/2"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Patch with different capitalisation than doc [7b72867a-a82e-4854-a0ba-2f60dcb2121d]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"/FOO","value":"BAR"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"bar","FOO":"BAR"}
