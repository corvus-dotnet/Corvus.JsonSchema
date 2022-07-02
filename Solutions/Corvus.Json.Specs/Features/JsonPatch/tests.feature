
Feature: tests

Scenario: empty list, empty docs [6fe224af-01fd-41ea-8ef3-059cbe82258e]
	Given the document {}
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal {}

Scenario: empty patch list [812350f9-d7af-4173-b75c-56f2f2661ea8]
	Given the document {"foo":1}
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: rearrangements OK? [f6293c6d-9158-4949-ab48-2af0be0b9558]
	Given the document {"foo":1,"bar":2}
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal {"bar":2,"foo":1}

Scenario: rearrangements OK?  How about one level down ... array [679b5304-9f93-4802-806c-9de29ff96251]
	Given the document [{"foo":1,"bar":2}]
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal [{"bar":2,"foo":1}]

Scenario: rearrangements OK?  How about one level down... [88478ef4-c86d-4a71-9545-cafde6009e61]
	Given the document {"foo":{"foo":1,"bar":2}}
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal {"foo":{"bar":2,"foo":1}}

Scenario: add replaces any existing field [fdf98f6a-d172-4187-bfc0-4de9b92964fb]
	Given the document {"foo":null}
	And the patch [{"op":"add","path":"/foo","value":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: toplevel array [fa4b723e-a22a-4246-823f-464bcf29bb37]
	Given the document []
	And the patch [{"op":"add","path":"/0","value":"foo"}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo"]

Scenario: toplevel array, no change [4bac034f-c25e-4f28-b045-b8c9bcef7d82]
	Given the document ["foo"]
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal ["foo"]

Scenario: toplevel object, numeric string [d972aa3b-f055-4017-967a-8c84c32aaa77]
	Given the document {}
	And the patch [{"op":"add","path":"/foo","value":"1"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"1"}

Scenario: toplevel object, integer [3b7b95c8-af2a-43f6-8854-d4832b7354a3]
	Given the document {}
	And the patch [{"op":"add","path":"/foo","value":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: replace object document with array document? [3ac6a41f-d665-44ed-b2c6-fa07deb22a27]
	Given the document {}
	And the patch [{"op":"add","path":"","value":[]}]
	When I apply the patch to the document
	Then the transformed document should equal []

Scenario: replace array document with object document? [d490fd22-5e54-4621-a761-002a8eb56c51]
	Given the document []
	And the patch [{"op":"add","path":"","value":{}}]
	When I apply the patch to the document
	Then the transformed document should equal {}

Scenario: append to root array document? [78626ff9-bbdd-4d6b-be6e-f80c1b415edc]
	Given the document []
	And the patch [{"op":"add","path":"/-","value":"hi"}]
	When I apply the patch to the document
	Then the transformed document should equal ["hi"]

Scenario: Add, / target [252aa57f-a161-4691-a2db-d187b9894958]
	Given the document {}
	And the patch [{"op":"add","path":"/","value":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"":1}

Scenario: Add, /foo/ deep target (trailing slash) [017295a2-503e-4044-a6ee-d7703aa6287c]
	Given the document {"foo":{}}
	And the patch [{"op":"add","path":"/foo/","value":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":{"":1}}

Scenario: Add composite value at top level [bef353d1-b021-4b2e-8d34-02a689e334c3]
	Given the document {"foo":1}
	And the patch [{"op":"add","path":"/bar","value":[1,2]}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"bar":[1,2]}

Scenario: Add into composite value [7fd81f31-9af1-45ea-9f84-523600d23d56]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	And the patch [{"op":"add","path":"/baz/0/foo","value":"world"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"baz":[{"qux":"hello","foo":"world"}]}

Scenario: Undescribed scenario [48195f92-d987-4e1b-98f7-b57037ee7585]
	Given the document {"bar":[1,2]}
	And the patch [{"op":"add","path":"/bar/8","value":"5"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Undescribed scenario [65fdf6a0-e4db-4bd8-bc9b-3628fb6f6e51]
	Given the document {"bar":[1,2]}
	And the patch [{"op":"add","path":"/bar/-1","value":"5"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Undescribed scenario [da8c213e-f7b6-4b84-8937-980f868131fb]
	Given the document {"foo":1}
	And the patch [{"op":"add","path":"/bar","value":true}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"bar":true}

Scenario: Undescribed scenario [aa77c6c2-cef6-4c20-8bea-b2e8c5450e96]
	Given the document {"foo":1}
	And the patch [{"op":"add","path":"/bar","value":false}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"bar":false}

Scenario: Undescribed scenario [2409a235-124f-41ec-a778-f21614e3f6d3]
	Given the document {"foo":1}
	And the patch [{"op":"add","path":"/bar","value":null}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"bar":null}

Scenario: 0 can be an array index or object element name [5ee36362-23fe-47f6-b45a-693653a55104]
	Given the document {"foo":1}
	And the patch [{"op":"add","path":"/0","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"0":"bar"}

Scenario: Undescribed scenario [d0280508-3809-45a5-b5c1-62db337f1931]
	Given the document ["foo"]
	And the patch [{"op":"add","path":"/1","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo","bar"]

Scenario: Undescribed scenario [8d34e027-3026-4388-8e27-e46ac41d6eca]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/1","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo","bar","sil"]

Scenario: Undescribed scenario [fcfff65a-9214-4640-8615-186f9d30e59b]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/0","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal ["bar","foo","sil"]

Scenario: push item to array via last index + 1 [533fc18a-770f-4a49-b325-3214283df40e]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/2","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo","sil","bar"]

Scenario: add item to array at index > length should fail [672c1926-eb11-40be-8d10-3eff831a29b2]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/3","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test against implementation-specific numeric parsing [c3592f0d-239a-42e9-8bab-4b1eb89cf2f2]
	Given the document {"1e0":"foo"}
	And the patch [{"op":"test","path":"/1e0","value":"foo"}]
	When I apply the patch to the document
	Then the transformed document should equal {"1e0":"foo"}

Scenario: test with bad number should fail [0d902ab6-59b9-4546-9cdf-d095e56eb200]
	Given the document ["foo","bar"]
	And the patch [{"op":"test","path":"/1e0","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Undescribed scenario [d674d355-27e2-4e06-bee7-861313015fb1]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/bar","value":42}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: value in array add not flattened [d415feea-95ac-4673-9699-b92d10b9fa6d]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/1","value":["bar","baz"]}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo",["bar","baz"],"sil"]

Scenario: Undescribed scenario [062ab205-7479-4b90-84ea-1c99eccbf952]
	Given the document {"foo":1,"bar":[1,2,3,4]}
	And the patch [{"op":"remove","path":"/bar"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: Undescribed scenario [4e0bd787-4546-4bc7-a979-f12d49a62ea6]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	And the patch [{"op":"remove","path":"/baz/0/qux"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"baz":[{}]}

Scenario: Undescribed scenario [39202b16-00d3-4100-9b3a-d5c1e9f76be3]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	And the patch [{"op":"replace","path":"/foo","value":[1,2,3,4]}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":[1,2,3,4],"baz":[{"qux":"hello"}]}

Scenario: Undescribed scenario [958a4930-d0be-4b81-8e75-a3f8d5cb1614]
	Given the document {"foo":[1,2,3,4],"baz":[{"qux":"hello"}]}
	And the patch [{"op":"replace","path":"/baz/0/qux","value":"world"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":[1,2,3,4],"baz":[{"qux":"world"}]}

Scenario: Undescribed scenario [52aa1522-7986-4278-b1a5-d2424a063191]
	Given the document ["foo"]
	And the patch [{"op":"replace","path":"/0","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal ["bar"]

Scenario: Undescribed scenario [b8460fb3-5646-4566-90ce-d6f24b3f288d]
	Given the document [""]
	And the patch [{"op":"replace","path":"/0","value":0}]
	When I apply the patch to the document
	Then the transformed document should equal [0]

Scenario: Undescribed scenario [8b479d5b-9bbf-40e1-b6a5-3485a6b8524c]
	Given the document [""]
	And the patch [{"op":"replace","path":"/0","value":true}]
	When I apply the patch to the document
	Then the transformed document should equal [true]

Scenario: Undescribed scenario [276d0306-8083-47bf-a7c5-08a908d7e36b]
	Given the document [""]
	And the patch [{"op":"replace","path":"/0","value":false}]
	When I apply the patch to the document
	Then the transformed document should equal [false]

Scenario: Undescribed scenario [a0d8f04f-ef8a-41db-b590-52213a5cc27e]
	Given the document [""]
	And the patch [{"op":"replace","path":"/0","value":null}]
	When I apply the patch to the document
	Then the transformed document should equal [null]

Scenario: value in array replace not flattened [689d496d-1d91-4fcc-a57b-5bb09be312c4]
	Given the document ["foo","sil"]
	And the patch [{"op":"replace","path":"/1","value":["bar","baz"]}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo",["bar","baz"]]

Scenario: replace whole document [7ad1bb61-d220-4c8c-9efd-a4a78be0f801]
	Given the document {"foo":"bar"}
	And the patch [{"op":"replace","path":"","value":{"baz":"qux"}}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":"qux"}

Scenario: test replace with missing parent key should fail [525b66c4-97a0-405f-99c0-31e55fc76411]
	Given the document {"bar":"baz"}
	And the patch [{"op":"replace","path":"/foo/bar","value":false}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: spurious patch properties [2df10f3f-fd54-438a-beee-cad39178fc48]
	Given the document {"foo":1}
	And the patch [{"op":"test","path":"/foo","value":1,"spurious":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: null value should be valid obj property [4996af65-1ee4-4d53-ab10-8cec53f25b14]
	Given the document {"foo":null}
	And the patch [{"op":"test","path":"/foo","value":null}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":null}

Scenario: null value should be valid obj property to be replaced with something truthy [84d5be28-222e-4b5e-b1e0-bb88b6735d64]
	Given the document {"foo":null}
	And the patch [{"op":"replace","path":"/foo","value":"truthy"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"truthy"}

Scenario: null value should be valid obj property to be moved [d03aa448-bc3f-40a7-9021-32f7f6d58bc1]
	Given the document {"foo":null}
	And the patch [{"op":"move","from":"/foo","path":"/bar"}]
	When I apply the patch to the document
	Then the transformed document should equal {"bar":null}

Scenario: null value should be valid obj property to be copied [740d46d2-76f7-43a5-bbb8-59e7b60ad9f4]
	Given the document {"foo":null}
	And the patch [{"op":"copy","from":"/foo","path":"/bar"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":null,"bar":null}

Scenario: null value should be valid obj property to be removed [f56ca0b7-5d6e-4d69-bfa9-9ea6c146503a]
	Given the document {"foo":null}
	And the patch [{"op":"remove","path":"/foo"}]
	When I apply the patch to the document
	Then the transformed document should equal {}

Scenario: null value should still be valid obj property replace other value [82a6dc21-3970-4764-8ebd-4dbc184aba4b]
	Given the document {"foo":"bar"}
	And the patch [{"op":"replace","path":"/foo","value":null}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":null}

Scenario: test should pass despite rearrangement [1b007bd0-dd43-4f10-a6a3-3f34dceb56d7]
	Given the document {"foo":{"foo":1,"bar":2}}
	And the patch [{"op":"test","path":"/foo","value":{"bar":2,"foo":1}}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":{"foo":1,"bar":2}}

Scenario: test should pass despite (nested) rearrangement [7aaedb1a-c1c4-463c-922f-f324344637bb]
	Given the document {"foo":[{"foo":1,"bar":2}]}
	And the patch [{"op":"test","path":"/foo","value":[{"bar":2,"foo":1}]}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":[{"foo":1,"bar":2}]}

Scenario: test should pass - no error [45bb0144-9e36-4a57-a70c-a2d05c684f3e]
	Given the document {"foo":{"bar":[1,2,5,4]}}
	And the patch [{"op":"test","path":"/foo","value":{"bar":[1,2,5,4]}}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":{"bar":[1,2,5,4]}}

Scenario: Undescribed scenario [7db7b290-37cc-417f-b929-931e044ded58]
	Given the document {"foo":{"bar":[1,2,5,4]}}
	And the patch [{"op":"test","path":"/foo","value":[1,2]}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Empty-string element [b2b1ddd3-dd7e-4f01-a7d7-9d386d74a24c]
	Given the document {"":1}
	And the patch [{"op":"test","path":"/","value":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"":1}

Scenario: Undescribed scenario [9bc39849-6c41-4962-ac53-9b9b5d8dc647]
	Given the document {"foo":["bar","baz"],"":0,"a/b":1,"c%d":2,"e^f":3,"g|h":4,"i\\j":5,"k\u0022l":6," ":7,"m~n":8}
	And the patch [{"op":"test","path":"/foo","value":["bar","baz"]},{"op":"test","path":"/foo/0","value":"bar"},{"op":"test","path":"/","value":0},{"op":"test","path":"/a~1b","value":1},{"op":"test","path":"/c%d","value":2},{"op":"test","path":"/e^f","value":3},{"op":"test","path":"/g|h","value":4},{"op":"test","path":"/i\\j","value":5},{"op":"test","path":"/k\u0022l","value":6},{"op":"test","path":"/ ","value":7},{"op":"test","path":"/m~0n","value":8}]
	When I apply the patch to the document
	Then the transformed document should equal {"":0," ":7,"a/b":1,"c%d":2,"e^f":3,"foo":["bar","baz"],"g|h":4,"i\\j":5,"k\u0022l":6,"m~n":8}

Scenario: Move to same location has no effect [fc526370-680b-4bdf-aa0f-8a71f4a27f38]
	Given the document {"foo":1}
	And the patch [{"op":"move","from":"/foo","path":"/foo"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: Undescribed scenario [933f8c32-29a4-410f-833e-3f25110f134c]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	And the patch [{"op":"move","from":"/foo","path":"/bar"}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":[{"qux":"hello"}],"bar":1}

Scenario: Undescribed scenario [eeed4a48-9cf4-46c4-b439-2bb8b5aca7f6]
	Given the document {"baz":[{"qux":"hello"}],"bar":1}
	And the patch [{"op":"move","from":"/baz/0/qux","path":"/baz/1"}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":[{},"hello"],"bar":1}

Scenario: Undescribed scenario [d30792bf-c4de-4239-8003-e7fc13c785bb]
	Given the document {"baz":[{"qux":"hello"}],"bar":1}
	And the patch [{"op":"copy","from":"/baz/0","path":"/boo"}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":[{"qux":"hello"}],"bar":1,"boo":{"qux":"hello"}}

Scenario: replacing the root of the document is possible with add [a28c748e-773b-496f-bbf8-8dd4939c412c]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"","value":{"baz":"qux"}}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":"qux"}

Scenario: Adding to "/-" adds to the end of the array [ca26b8c5-3adb-43d1-adbf-d8057c3ee4ac]
	Given the document [1,2]
	And the patch [{"op":"add","path":"/-","value":{"foo":["bar","baz"]}}]
	When I apply the patch to the document
	Then the transformed document should equal [1,2,{"foo":["bar","baz"]}]

Scenario: Adding to "/-" adds to the end of the array, even n levels down [01107baf-0085-45a1-94cd-ac1595192925]
	Given the document [1,2,[3,[4,5]]]
	And the patch [{"op":"add","path":"/2/1/-","value":{"foo":["bar","baz"]}}]
	When I apply the patch to the document
	Then the transformed document should equal [1,2,[3,[4,5,{"foo":["bar","baz"]}]]]

Scenario: test remove with bad number should fail [5ccf7633-eeb6-4782-87b4-160d0baf3cda]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	And the patch [{"op":"remove","path":"/baz/1e0/qux"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test remove on array [345e929e-0b47-42d3-90ac-ae6bd6fa3a19]
	Given the document [1,2,3,4]
	And the patch [{"op":"remove","path":"/0"}]
	When I apply the patch to the document
	Then the transformed document should equal [2,3,4]

Scenario: test repeated removes [40ecbd5a-ca9a-4f67-a645-5f5b9e9d9a80]
	Given the document [1,2,3,4]
	And the patch [{"op":"remove","path":"/1"},{"op":"remove","path":"/2"}]
	When I apply the patch to the document
	Then the transformed document should equal [1,3]

Scenario: test remove with bad index should fail [eb3c3632-9e13-4c18-8e9f-bbcc1471f690]
	Given the document [1,2,3,4]
	And the patch [{"op":"remove","path":"/1e0"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test replace with bad number should fail [059b8cea-e436-4449-a239-13836d6765cf]
	Given the document [""]
	And the patch [{"op":"replace","path":"/1e0","value":false}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test copy with bad number should fail [781e1b28-acf7-457c-9d0c-414fd9dff195]
	Given the document {"baz":[1,2,3],"bar":1}
	And the patch [{"op":"copy","from":"/baz/1e0","path":"/boo"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test move with bad number should fail [02f21653-e88c-4af6-9741-24f244bb4285]
	Given the document {"foo":1,"baz":[1,2,3,4]}
	And the patch [{"op":"move","from":"/baz/1e0","path":"/foo"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test add with bad number should fail [09fe2be4-6fd7-4ebd-8ff9-22a9dc95a75d]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/1e0","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing 'path' parameter [71cc5cd9-09c5-4653-9b3d-cf58cca499e3]
	Given the document {}
	And the patch [{"op":"add","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: 'path' parameter with null value [ff99fb45-1f93-45b7-aa09-79078321e346]
	Given the document {}
	And the patch [{"op":"add","path":null,"value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: invalid JSON Pointer token [21afcbda-ce0e-45d5-af26-7dc62c8cdd88]
	Given the document {}
	And the patch [{"op":"add","path":"foo","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing 'value' parameter to add [7e531041-1fda-458d-8c9d-9686d0fdebea]
	Given the document [1]
	And the patch [{"op":"add","path":"/-"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing 'value' parameter to replace [ec304a02-2236-47c7-acf8-a16562fca648]
	Given the document [1]
	And the patch [{"op":"replace","path":"/0"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing 'value' parameter to test [a8a28e8a-9ef3-4bf7-8c49-98963b6c9cf9]
	Given the document [null]
	And the patch [{"op":"test","path":"/0"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing value parameter to test - where undef is falsy [eef7e28f-67af-43cc-965c-5f5826a02972]
	Given the document [false]
	And the patch [{"op":"test","path":"/0"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing from parameter to copy [bd6a5f9d-d65f-4127-8410-e9b198428772]
	Given the document [1]
	And the patch [{"op":"copy","path":"/-"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing from location to copy [75b5183a-1714-4a0f-beed-a3eaabd712dc]
	Given the document {"foo":1}
	And the patch [{"op":"copy","from":"/bar","path":"/foo"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing from parameter to move [17374620-9177-4128-9f44-9c8b27e6f972]
	Given the document {"foo":1}
	And the patch [{"op":"move","path":""}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing from location to move [d8a79558-af74-4183-996a-f651e59d1409]
	Given the document {"foo":1}
	And the patch [{"op":"move","from":"/bar","path":"/foo"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: unrecognized op should fail [9532a3c6-fa98-4303-a6f5-91194d1d4f4d]
	Given the document {"foo":1}
	And the patch [{"op":"spam","path":"/foo","value":1}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test with bad array number that has leading zeros [b6e9e808-807d-4303-888a-b84cd258f7e5]
	Given the document ["foo","bar"]
	And the patch [{"op":"test","path":"/00","value":"foo"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test with bad array number that has leading zeros [0ad982d1-4feb-4050-b12a-aea377e3e700]
	Given the document ["foo","bar"]
	And the patch [{"op":"test","path":"/01","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Removing nonexistent field [0d295705-a19d-4858-b6af-4e6ad1934432]
	Given the document {"foo":"bar"}
	And the patch [{"op":"remove","path":"/baz"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Removing deep nonexistent path [d1b19490-f34c-4b44-9979-a4b3705f787c]
	Given the document {"foo":"bar"}
	And the patch [{"op":"remove","path":"/missing1/missing2"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Removing nonexistent index [c3090d0e-5417-4f64-820d-0b3b7bae43ae]
	Given the document ["foo","bar"]
	And the patch [{"op":"remove","path":"/2"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Patch with different capitalisation than doc [5fa9b416-693e-4137-b6a9-dc06d702211b]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"/FOO","value":"BAR"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"bar","FOO":"BAR"}
