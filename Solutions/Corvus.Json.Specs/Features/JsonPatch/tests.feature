
Feature: tests

Scenario: empty list, empty docs [716bbc67-05b8-4ed3-8517-f0e93ecd93ad]
	Given the document {}
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal {}

Scenario: empty patch list [6f6ccc82-861c-4744-8b6c-6b7aac353d6e]
	Given the document {"foo":1}
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: rearrangements OK? [f886ab99-3472-4339-926a-9107b7c88f7f]
	Given the document {"foo":1,"bar":2}
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal {"bar":2,"foo":1}

Scenario: rearrangements OK?  How about one level down ... array [2a85ad24-1dcc-407c-817d-73bad6bec089]
	Given the document [{"foo":1,"bar":2}]
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal [{"bar":2,"foo":1}]

Scenario: rearrangements OK?  How about one level down... [af22e1a4-c7ab-4fd7-9c0f-31d10e03bbe7]
	Given the document {"foo":{"foo":1,"bar":2}}
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal {"foo":{"bar":2,"foo":1}}

Scenario: add replaces any existing field [a0a37c20-540e-46c4-91ff-568b789c6f4d]
	Given the document {"foo":null}
	And the patch [{"op":"add","path":"/foo","value":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: toplevel array [7361b23e-8013-48e4-b6ba-6418e714f1b0]
	Given the document []
	And the patch [{"op":"add","path":"/0","value":"foo"}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo"]

Scenario: toplevel array, no change [7b667254-cbb2-4d9e-ae63-55f3acf1fb9f]
	Given the document ["foo"]
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal ["foo"]

Scenario: toplevel object, numeric string [bcf0cdb5-9242-443b-81b3-1cd03489c451]
	Given the document {}
	And the patch [{"op":"add","path":"/foo","value":"1"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"1"}

Scenario: toplevel object, integer [921958fa-11c3-4414-9993-d1cfed249e2c]
	Given the document {}
	And the patch [{"op":"add","path":"/foo","value":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: replace object document with array document? [11b09f4d-89bd-433d-ad62-84fa2f0b2dc3]
	Given the document {}
	And the patch [{"op":"add","path":"","value":[]}]
	When I apply the patch to the document
	Then the transformed document should equal []

Scenario: replace array document with object document? [9843c584-06b6-42c6-a378-8ff29ff44922]
	Given the document []
	And the patch [{"op":"add","path":"","value":{}}]
	When I apply the patch to the document
	Then the transformed document should equal {}

Scenario: append to root array document? [9b227da6-ab49-475c-9ce8-3de635ca24d1]
	Given the document []
	And the patch [{"op":"add","path":"/-","value":"hi"}]
	When I apply the patch to the document
	Then the transformed document should equal ["hi"]

Scenario: Add, / target [88c140d6-800b-42d8-9bf9-18027886a1ec]
	Given the document {}
	And the patch [{"op":"add","path":"/","value":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"":1}

Scenario: Add, /foo/ deep target (trailing slash) [89bfc69e-1d47-4075-8803-427a01a28df3]
	Given the document {"foo":{}}
	And the patch [{"op":"add","path":"/foo/","value":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":{"":1}}

Scenario: Add composite value at top level [71125936-afed-45d5-82e3-498d0905df70]
	Given the document {"foo":1}
	And the patch [{"op":"add","path":"/bar","value":[1,2]}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"bar":[1,2]}

Scenario: Add into composite value [c2c9b003-21b2-462b-ba97-7c29be950165]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	And the patch [{"op":"add","path":"/baz/0/foo","value":"world"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"baz":[{"qux":"hello","foo":"world"}]}

Scenario: Undescribed scenario [66910691-16e6-4d55-beac-a59212c49ed3]
	Given the document {"bar":[1,2]}
	And the patch [{"op":"add","path":"/bar/8","value":"5"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Undescribed scenario [830984d8-d030-4a15-869a-ab088737c5b8]
	Given the document {"bar":[1,2]}
	And the patch [{"op":"add","path":"/bar/-1","value":"5"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Undescribed scenario [7171b684-ba00-415a-8063-f11264650253]
	Given the document {"foo":1}
	And the patch [{"op":"add","path":"/bar","value":true}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"bar":true}

Scenario: Undescribed scenario [b0efed87-4eba-4dbe-8a91-2bd3a0678fb1]
	Given the document {"foo":1}
	And the patch [{"op":"add","path":"/bar","value":false}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"bar":false}

Scenario: Undescribed scenario [5a6a775c-81e8-4746-b7e9-c9b4b698b863]
	Given the document {"foo":1}
	And the patch [{"op":"add","path":"/bar","value":null}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"bar":null}

Scenario: 0 can be an array index or object element name [90977289-aec8-4865-9597-6c8b23a9b37e]
	Given the document {"foo":1}
	And the patch [{"op":"add","path":"/0","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"0":"bar"}

Scenario: Undescribed scenario [97953edc-77dd-4e9e-a27c-70edf5f58c41]
	Given the document ["foo"]
	And the patch [{"op":"add","path":"/1","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo","bar"]

Scenario: Undescribed scenario [94fcf51b-3ece-4ea5-993b-74df4859193d]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/1","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo","bar","sil"]

Scenario: Undescribed scenario [bfcf0e80-15b4-4ef0-ae11-02e51b07d654]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/0","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal ["bar","foo","sil"]

Scenario: push item to array via last index + 1 [7608d47a-9ead-447f-94d7-f668ba18841c]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/2","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo","sil","bar"]

Scenario: add item to array at index > length should fail [363ef266-13c7-45fa-bbb1-ed935cd4bb6f]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/3","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test against implementation-specific numeric parsing [9b6d11ea-1479-41fd-aa97-8062729e9eed]
	Given the document {"1e0":"foo"}
	And the patch [{"op":"test","path":"/1e0","value":"foo"}]
	When I apply the patch to the document
	Then the transformed document should equal {"1e0":"foo"}

Scenario: test with bad number should fail [cb4e3eac-fcb6-4ea3-9cb5-f81e5144e5e9]
	Given the document ["foo","bar"]
	And the patch [{"op":"test","path":"/1e0","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Undescribed scenario [edf470a1-b58a-429e-b945-c0b81a2ec43b]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/bar","value":42}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: value in array add not flattened [a976d2ec-147b-442e-9674-60177bd46d25]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/1","value":["bar","baz"]}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo",["bar","baz"],"sil"]

Scenario: Undescribed scenario [12e24076-54fa-4e5d-915f-cf4a58229b55]
	Given the document {"foo":1,"bar":[1,2,3,4]}
	And the patch [{"op":"remove","path":"/bar"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: Undescribed scenario [9f629176-e85e-4bc0-abe6-b998bc14c8ce]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	And the patch [{"op":"remove","path":"/baz/0/qux"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"baz":[{}]}

Scenario: Undescribed scenario [56d29574-22ea-4e78-8630-788defaae335]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	And the patch [{"op":"replace","path":"/foo","value":[1,2,3,4]}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":[1,2,3,4],"baz":[{"qux":"hello"}]}

Scenario: Undescribed scenario [f84c2f7a-b31f-4aa8-9eb6-f7d57ac51ffd]
	Given the document {"foo":[1,2,3,4],"baz":[{"qux":"hello"}]}
	And the patch [{"op":"replace","path":"/baz/0/qux","value":"world"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":[1,2,3,4],"baz":[{"qux":"world"}]}

Scenario: Undescribed scenario [b34d98ad-22a9-4dd5-b04a-3b83a24c4167]
	Given the document ["foo"]
	And the patch [{"op":"replace","path":"/0","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal ["bar"]

Scenario: Undescribed scenario [2d6432ca-03a4-470a-8335-4f439c775e9d]
	Given the document [""]
	And the patch [{"op":"replace","path":"/0","value":0}]
	When I apply the patch to the document
	Then the transformed document should equal [0]

Scenario: Undescribed scenario [699443a7-cde2-4235-9205-3cf9a32460b9]
	Given the document [""]
	And the patch [{"op":"replace","path":"/0","value":true}]
	When I apply the patch to the document
	Then the transformed document should equal [true]

Scenario: Undescribed scenario [55a551cc-4152-48f0-942c-bad31f0e3643]
	Given the document [""]
	And the patch [{"op":"replace","path":"/0","value":false}]
	When I apply the patch to the document
	Then the transformed document should equal [false]

Scenario: Undescribed scenario [65d3659e-aa95-49c4-826f-3642f431320d]
	Given the document [""]
	And the patch [{"op":"replace","path":"/0","value":null}]
	When I apply the patch to the document
	Then the transformed document should equal [null]

Scenario: value in array replace not flattened [741ed0f0-77ef-48f2-8a0b-35dc07b711f1]
	Given the document ["foo","sil"]
	And the patch [{"op":"replace","path":"/1","value":["bar","baz"]}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo",["bar","baz"]]

Scenario: replace whole document [79001539-524c-41ec-838e-7bba6ba38e74]
	Given the document {"foo":"bar"}
	And the patch [{"op":"replace","path":"","value":{"baz":"qux"}}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":"qux"}

Scenario: test replace with missing parent key should fail [bca103c2-1c87-4b62-84bc-30b2a816fbad]
	Given the document {"bar":"baz"}
	And the patch [{"op":"replace","path":"/foo/bar","value":false}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: spurious patch properties [ecbc3bff-9e8f-4db6-9596-da7a64fb448f]
	Given the document {"foo":1}
	And the patch [{"op":"test","path":"/foo","value":1,"spurious":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: null value should be valid obj property [f3bb6b40-6b7b-4786-a94c-858882df013c]
	Given the document {"foo":null}
	And the patch [{"op":"test","path":"/foo","value":null}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":null}

Scenario: null value should be valid obj property to be replaced with something truthy [9d7fbbb3-1ac4-45a3-b367-e21c18ee5219]
	Given the document {"foo":null}
	And the patch [{"op":"replace","path":"/foo","value":"truthy"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"truthy"}

Scenario: null value should be valid obj property to be moved [3c97e5ef-266b-483f-bc98-684b251b7073]
	Given the document {"foo":null}
	And the patch [{"op":"move","from":"/foo","path":"/bar"}]
	When I apply the patch to the document
	Then the transformed document should equal {"bar":null}

Scenario: null value should be valid obj property to be copied [030ff7b8-867f-4e45-b496-b9109f5375ce]
	Given the document {"foo":null}
	And the patch [{"op":"copy","from":"/foo","path":"/bar"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":null,"bar":null}

Scenario: null value should be valid obj property to be removed [e45fe93d-4b97-4306-b72f-4f63dc22a821]
	Given the document {"foo":null}
	And the patch [{"op":"remove","path":"/foo"}]
	When I apply the patch to the document
	Then the transformed document should equal {}

Scenario: null value should still be valid obj property replace other value [e4f1b2bf-ca70-4ddd-a376-17be7f3e9dad]
	Given the document {"foo":"bar"}
	And the patch [{"op":"replace","path":"/foo","value":null}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":null}

Scenario: test should pass despite rearrangement [aa2e6606-4f23-4b94-83ac-fa33a538e9e3]
	Given the document {"foo":{"foo":1,"bar":2}}
	And the patch [{"op":"test","path":"/foo","value":{"bar":2,"foo":1}}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":{"foo":1,"bar":2}}

Scenario: test should pass despite (nested) rearrangement [6c69f4cb-b63c-4a98-926f-d4f36bd19b07]
	Given the document {"foo":[{"foo":1,"bar":2}]}
	And the patch [{"op":"test","path":"/foo","value":[{"bar":2,"foo":1}]}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":[{"foo":1,"bar":2}]}

Scenario: test should pass - no error [6f3871d2-412f-4124-925c-061ac8240c10]
	Given the document {"foo":{"bar":[1,2,5,4]}}
	And the patch [{"op":"test","path":"/foo","value":{"bar":[1,2,5,4]}}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":{"bar":[1,2,5,4]}}

Scenario: Undescribed scenario [7600f57c-f12a-4e1d-be47-1474ef8f5b43]
	Given the document {"foo":{"bar":[1,2,5,4]}}
	And the patch [{"op":"test","path":"/foo","value":[1,2]}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Empty-string element [50f71116-21b8-4caf-ab4c-0c74159a807c]
	Given the document {"":1}
	And the patch [{"op":"test","path":"/","value":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"":1}

Scenario: Undescribed scenario [e27bef7e-0211-4776-bd4c-1e28d257a0e7]
	Given the document {"foo":["bar","baz"],"":0,"a/b":1,"c%d":2,"e^f":3,"g|h":4,"i\\j":5,"k\u0022l":6," ":7,"m~n":8}
	And the patch [{"op":"test","path":"/foo","value":["bar","baz"]},{"op":"test","path":"/foo/0","value":"bar"},{"op":"test","path":"/","value":0},{"op":"test","path":"/a~1b","value":1},{"op":"test","path":"/c%d","value":2},{"op":"test","path":"/e^f","value":3},{"op":"test","path":"/g|h","value":4},{"op":"test","path":"/i\\j","value":5},{"op":"test","path":"/k\u0022l","value":6},{"op":"test","path":"/ ","value":7},{"op":"test","path":"/m~0n","value":8}]
	When I apply the patch to the document
	Then the transformed document should equal {"":0," ":7,"a/b":1,"c%d":2,"e^f":3,"foo":["bar","baz"],"g|h":4,"i\\j":5,"k\u0022l":6,"m~n":8}

Scenario: Move to same location has no effect [33a301d8-7ad4-4b97-91f9-863c1afa8786]
	Given the document {"foo":1}
	And the patch [{"op":"move","from":"/foo","path":"/foo"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: Undescribed scenario [a7373a44-604c-4e1f-9842-d252ecfec115]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	And the patch [{"op":"move","from":"/foo","path":"/bar"}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":[{"qux":"hello"}],"bar":1}

Scenario: Undescribed scenario [039916df-3ca7-4d62-b639-b6c52f4ecc2e]
	Given the document {"baz":[{"qux":"hello"}],"bar":1}
	And the patch [{"op":"move","from":"/baz/0/qux","path":"/baz/1"}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":[{},"hello"],"bar":1}

Scenario: Undescribed scenario [6b621cc3-bd12-40d4-b697-81720c2d3b0e]
	Given the document {"baz":[{"qux":"hello"}],"bar":1}
	And the patch [{"op":"copy","from":"/baz/0","path":"/boo"}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":[{"qux":"hello"}],"bar":1,"boo":{"qux":"hello"}}

Scenario: replacing the root of the document is possible with add [e7cc8ad4-9889-4c18-a156-3f1c62b579a9]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"","value":{"baz":"qux"}}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":"qux"}

Scenario: Adding to "/-" adds to the end of the array [a0ec91c3-0d94-4cf9-b8ec-392448dfcfd9]
	Given the document [1,2]
	And the patch [{"op":"add","path":"/-","value":{"foo":["bar","baz"]}}]
	When I apply the patch to the document
	Then the transformed document should equal [1,2,{"foo":["bar","baz"]}]

Scenario: Adding to "/-" adds to the end of the array, even n levels down [ff9f63a2-cf55-4d8c-b145-49571c1a217e]
	Given the document [1,2,[3,[4,5]]]
	And the patch [{"op":"add","path":"/2/1/-","value":{"foo":["bar","baz"]}}]
	When I apply the patch to the document
	Then the transformed document should equal [1,2,[3,[4,5,{"foo":["bar","baz"]}]]]

Scenario: test remove with bad number should fail [fff4158a-4890-4406-a887-a6976058739f]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	And the patch [{"op":"remove","path":"/baz/1e0/qux"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test remove on array [933e62f3-5037-45dd-9617-df682008acbc]
	Given the document [1,2,3,4]
	And the patch [{"op":"remove","path":"/0"}]
	When I apply the patch to the document
	Then the transformed document should equal [2,3,4]

Scenario: test repeated removes [c313d1c9-70ee-4d0a-b689-314949b35471]
	Given the document [1,2,3,4]
	And the patch [{"op":"remove","path":"/1"},{"op":"remove","path":"/2"}]
	When I apply the patch to the document
	Then the transformed document should equal [1,3]

Scenario: test remove with bad index should fail [d12f8f42-460a-408c-9fa7-05a1e226c42e]
	Given the document [1,2,3,4]
	And the patch [{"op":"remove","path":"/1e0"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test replace with bad number should fail [00b7637b-2e62-4cf5-916c-81acf4bb8e32]
	Given the document [""]
	And the patch [{"op":"replace","path":"/1e0","value":false}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test copy with bad number should fail [46ba78bf-e76e-4ec3-89ad-f4770c4967d9]
	Given the document {"baz":[1,2,3],"bar":1}
	And the patch [{"op":"copy","from":"/baz/1e0","path":"/boo"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test move with bad number should fail [a5044e11-ced9-4c70-a4b5-bcc4f4f216cb]
	Given the document {"foo":1,"baz":[1,2,3,4]}
	And the patch [{"op":"move","from":"/baz/1e0","path":"/foo"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test add with bad number should fail [3432d885-238a-4619-b615-cd1f969aad4f]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/1e0","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing 'path' parameter [c9843019-6fa3-482c-b966-327716279df4]
	Given the document {}
	And the patch [{"op":"add","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: 'path' parameter with null value [30ce8ff1-2dc6-4b3b-828b-b5227f088646]
	Given the document {}
	And the patch [{"op":"add","path":null,"value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: invalid JSON Pointer token [2ca91e50-c065-4562-b0be-8b1a3def3da6]
	Given the document {}
	And the patch [{"op":"add","path":"foo","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing 'value' parameter to add [26e2f8a9-d262-41be-9bfb-e0fb4f3905b7]
	Given the document [1]
	And the patch [{"op":"add","path":"/-"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing 'value' parameter to replace [b61e52af-40d0-470a-b46a-7285f2febaa9]
	Given the document [1]
	And the patch [{"op":"replace","path":"/0"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing 'value' parameter to test [30313ca3-f459-4469-b98e-db8add6111da]
	Given the document [null]
	And the patch [{"op":"test","path":"/0"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing value parameter to test - where undef is falsy [2910a6ee-da73-498b-b2d6-9096ec9e29f2]
	Given the document [false]
	And the patch [{"op":"test","path":"/0"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing from parameter to copy [121fcb6c-326e-4f0d-ac8b-d383fefd3ba3]
	Given the document [1]
	And the patch [{"op":"copy","path":"/-"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing from location to copy [60369090-dc75-4829-b2e1-1f2315c71db7]
	Given the document {"foo":1}
	And the patch [{"op":"copy","from":"/bar","path":"/foo"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing from parameter to move [79eef47b-14d0-4c30-9ae3-840b7712b722]
	Given the document {"foo":1}
	And the patch [{"op":"move","path":""}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing from location to move [45c47772-2028-43ba-90a1-679dc3447e1e]
	Given the document {"foo":1}
	And the patch [{"op":"move","from":"/bar","path":"/foo"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: unrecognized op should fail [95f2132d-e6da-4790-b5b1-350e494bb974]
	Given the document {"foo":1}
	And the patch [{"op":"spam","path":"/foo","value":1}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test with bad array number that has leading zeros [82720b00-6a96-4cd2-8394-df8db0dd3f8e]
	Given the document ["foo","bar"]
	And the patch [{"op":"test","path":"/00","value":"foo"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test with bad array number that has leading zeros [0e2d4039-7ea7-48ba-8d66-c778e2270696]
	Given the document ["foo","bar"]
	And the patch [{"op":"test","path":"/01","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Removing nonexistent field [f87c2656-f744-4c2d-bf19-bd3f75c4d812]
	Given the document {"foo":"bar"}
	And the patch [{"op":"remove","path":"/baz"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Removing deep nonexistent path [3d9ad51c-e40e-429a-aa26-3ba0b9bc4746]
	Given the document {"foo":"bar"}
	And the patch [{"op":"remove","path":"/missing1/missing2"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Removing nonexistent index [0928a6c1-3eff-4cec-a226-7d81374ce236]
	Given the document ["foo","bar"]
	And the patch [{"op":"remove","path":"/2"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Patch with different capitalisation than doc [229a485e-ab71-4d54-b4e1-0c8b54bdf7cb]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"/FOO","value":"BAR"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"bar","FOO":"BAR"}
