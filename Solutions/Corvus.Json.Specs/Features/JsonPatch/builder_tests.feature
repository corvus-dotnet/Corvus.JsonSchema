
Feature: builder_tests

Scenario: empty list, empty docs [f4dd0fca-0697-4ebe-8302-7eed646909f2]
	Given the document {}
	When I build the patch []
	Then the patch result should equal {}

Scenario: empty patch list [831b480e-834e-4301-ac9a-34fcda904238]
	Given the document {"foo":1}
	When I build the patch []
	Then the patch result should equal {"foo":1}

Scenario: rearrangements OK? [68689b01-42f3-48c6-b7bb-1fcce450f403]
	Given the document {"foo":1,"bar":2}
	When I build the patch []
	Then the patch result should equal {"bar":2,"foo":1}

Scenario: rearrangements OK?  How about one level down ... array [23ef81a9-4ba0-49b3-8da3-27f72d26d310]
	Given the document [{"foo":1,"bar":2}]
	When I build the patch []
	Then the patch result should equal [{"bar":2,"foo":1}]

Scenario: rearrangements OK?  How about one level down... [f75b61f2-d981-4cfc-a263-24e6b340fe72]
	Given the document {"foo":{"foo":1,"bar":2}}
	When I build the patch []
	Then the patch result should equal {"foo":{"bar":2,"foo":1}}

Scenario: add replaces any existing field [a214b875-4459-456d-b8ec-051d6d887cfb]
	Given the document {"foo":null}
	When I build the patch [{"op":"add","path":"/foo","value":1}]
	Then the patch result should equal {"foo":1}

Scenario: toplevel array [70af2ce4-f519-4323-ad97-0cae79792889]
	Given the document []
	When I build the patch [{"op":"add","path":"/0","value":"foo"}]
	Then the patch result should equal ["foo"]

Scenario: toplevel array, no change [04015daa-6de7-4622-a494-c363f04e10ed]
	Given the document ["foo"]
	When I build the patch []
	Then the patch result should equal ["foo"]

Scenario: toplevel object, numeric string [82b66599-af68-4ea2-9182-9752fa97411b]
	Given the document {}
	When I build the patch [{"op":"add","path":"/foo","value":"1"}]
	Then the patch result should equal {"foo":"1"}

Scenario: toplevel object, integer [6a46aecd-6b68-42b1-9aff-eafa47e0a645]
	Given the document {}
	When I build the patch [{"op":"add","path":"/foo","value":1}]
	Then the patch result should equal {"foo":1}

Scenario: replace object document with array document? [cd721b40-9bd0-43f5-bd4c-79c81505c5fc]
	Given the document {}
	When I build the patch [{"op":"add","path":"","value":[]}]
	Then the patch result should equal []

Scenario: replace array document with object document? [a96154a9-3f60-49e2-9b27-82676a5bfe4b]
	Given the document []
	When I build the patch [{"op":"add","path":"","value":{}}]
	Then the patch result should equal {}

Scenario: append to root array document? [2f3fa9da-1b07-48fa-b2ac-0f6c9504c72b]
	Given the document []
	When I build the patch [{"op":"add","path":"/-","value":"hi"}]
	Then the patch result should equal ["hi"]

Scenario: Add, / target [87c1242c-ef02-405f-8649-13cedaa1ffb9]
	Given the document {}
	When I build the patch [{"op":"add","path":"/","value":1}]
	Then the patch result should equal {"":1}

Scenario: Add, /foo/ deep target (trailing slash) [7e8b8ecd-2840-48dc-85c1-aa09aee5b7bc]
	Given the document {"foo":{}}
	When I build the patch [{"op":"add","path":"/foo/","value":1}]
	Then the patch result should equal {"foo":{"":1}}

Scenario: Add composite value at top level [af2d7cb3-41ec-4707-8c37-814cc196a8b1]
	Given the document {"foo":1}
	When I build the patch [{"op":"add","path":"/bar","value":[1,2]}]
	Then the patch result should equal {"foo":1,"bar":[1,2]}

Scenario: Add into composite value [3cadda69-7eaa-445d-a8f8-6a532cb0eb37]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	When I build the patch [{"op":"add","path":"/baz/0/foo","value":"world"}]
	Then the patch result should equal {"foo":1,"baz":[{"qux":"hello","foo":"world"}]}

Scenario: Undescribed scenario [28f22532-d19f-4d8d-85b2-1e457bb11c6a]
	Given the document {"bar":[1,2]}
	When I build the patch [{"op":"add","path":"/bar/8","value":"5"}]
	Then a patch exception should be thrown

Scenario: Undescribed scenario [9c271872-6504-4775-9de8-b9f9a9ed9db1]
	Given the document {"bar":[1,2]}
	When I build the patch [{"op":"add","path":"/bar/-1","value":"5"}]
	Then a patch exception should be thrown

Scenario: Undescribed scenario [4dadf16e-cf6b-4df7-b471-b517c314015c]
	Given the document {"foo":1}
	When I build the patch [{"op":"add","path":"/bar","value":true}]
	Then the patch result should equal {"foo":1,"bar":true}

Scenario: Undescribed scenario [8064d118-f7a9-4600-bc9a-bcd22ce653f3]
	Given the document {"foo":1}
	When I build the patch [{"op":"add","path":"/bar","value":false}]
	Then the patch result should equal {"foo":1,"bar":false}

Scenario: Undescribed scenario [946fd51d-2830-426d-93e5-de46438bfde9]
	Given the document {"foo":1}
	When I build the patch [{"op":"add","path":"/bar","value":null}]
	Then the patch result should equal {"foo":1,"bar":null}

Scenario: 0 can be an array index or object element name [78c7dd22-c32c-4478-831d-bb5abec6ba60]
	Given the document {"foo":1}
	When I build the patch [{"op":"add","path":"/0","value":"bar"}]
	Then the patch result should equal {"foo":1,"0":"bar"}

Scenario: Undescribed scenario [07ab1a16-e153-4c37-bd23-e41402ecba34]
	Given the document ["foo"]
	When I build the patch [{"op":"add","path":"/1","value":"bar"}]
	Then the patch result should equal ["foo","bar"]

Scenario: Undescribed scenario [300dcf43-6d48-49e7-b269-e180507460f3]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"add","path":"/1","value":"bar"}]
	Then the patch result should equal ["foo","bar","sil"]

Scenario: Undescribed scenario [e7ba679b-b322-4631-ad6d-bfd896c4a5d0]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"add","path":"/0","value":"bar"}]
	Then the patch result should equal ["bar","foo","sil"]

Scenario: push item to array via last index + 1 [471b7c07-297b-417e-bf60-7cde27c6e492]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"add","path":"/2","value":"bar"}]
	Then the patch result should equal ["foo","sil","bar"]

Scenario: add item to array at index > length should fail [f35eca1c-7926-419d-b4f5-9149c0b393b9]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"add","path":"/3","value":"bar"}]
	Then a patch exception should be thrown

Scenario: test against implementation-specific numeric parsing [bc06e36d-84f5-410f-bf7f-a11bf365391c]
	Given the document {"1e0":"foo"}
	When I build the patch [{"op":"test","path":"/1e0","value":"foo"}]
	Then the patch result should equal {"1e0":"foo"}

Scenario: test with bad number should fail [fbdde180-5aba-442b-9ebf-eaa9dea97d8d]
	Given the document ["foo","bar"]
	When I build the patch [{"op":"test","path":"/1e0","value":"bar"}]
	Then a patch exception should be thrown

Scenario: Undescribed scenario [bbefe611-d4c2-473f-bd34-1c93f23f4551]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"add","path":"/bar","value":42}]
	Then a patch exception should be thrown

Scenario: value in array add not flattened [05d03af7-295b-49af-af48-ce50dd600a67]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"add","path":"/1","value":["bar","baz"]}]
	Then the patch result should equal ["foo",["bar","baz"],"sil"]

Scenario: Undescribed scenario [b2db1b51-741d-40d0-8fe8-fc428211480e]
	Given the document {"foo":1,"bar":[1,2,3,4]}
	When I build the patch [{"op":"remove","path":"/bar"}]
	Then the patch result should equal {"foo":1}

Scenario: Undescribed scenario [40051fe2-2923-4f54-a9eb-0fac9cc49dd5]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	When I build the patch [{"op":"remove","path":"/baz/0/qux"}]
	Then the patch result should equal {"foo":1,"baz":[{}]}

Scenario: Undescribed scenario [f56caef8-7790-4d94-95cd-3a2bf568f4f0]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	When I build the patch [{"op":"replace","path":"/foo","value":[1,2,3,4]}]
	Then the patch result should equal {"foo":[1,2,3,4],"baz":[{"qux":"hello"}]}

Scenario: Undescribed scenario [25eb96b2-bd9a-4d66-b3c5-5a26c5a89ee8]
	Given the document {"foo":[1,2,3,4],"baz":[{"qux":"hello"}]}
	When I build the patch [{"op":"replace","path":"/baz/0/qux","value":"world"}]
	Then the patch result should equal {"foo":[1,2,3,4],"baz":[{"qux":"world"}]}

Scenario: Undescribed scenario [9fbebe4e-c3f8-4e40-bf82-b30f2a2cd40f]
	Given the document ["foo"]
	When I build the patch [{"op":"replace","path":"/0","value":"bar"}]
	Then the patch result should equal ["bar"]

Scenario: Undescribed scenario [f4b089f4-c751-43bd-8964-7d6db7aaec08]
	Given the document [""]
	When I build the patch [{"op":"replace","path":"/0","value":0}]
	Then the patch result should equal [0]

Scenario: Undescribed scenario [052427d1-3bab-4bef-bbe3-07cf5d912d97]
	Given the document [""]
	When I build the patch [{"op":"replace","path":"/0","value":true}]
	Then the patch result should equal [true]

Scenario: Undescribed scenario [682bc910-2138-47bb-a505-254be8fd842d]
	Given the document [""]
	When I build the patch [{"op":"replace","path":"/0","value":false}]
	Then the patch result should equal [false]

Scenario: Undescribed scenario [b22504a9-3737-4e1a-bf7a-4258e25ce82f]
	Given the document [""]
	When I build the patch [{"op":"replace","path":"/0","value":null}]
	Then the patch result should equal [null]

Scenario: value in array replace not flattened [d428342b-b4e7-40f2-a035-9c26ff6b0ebf]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"replace","path":"/1","value":["bar","baz"]}]
	Then the patch result should equal ["foo",["bar","baz"]]

Scenario: replace whole document [5e90a27d-97dd-48fb-892e-993f5329abb8]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"replace","path":"","value":{"baz":"qux"}}]
	Then the patch result should equal {"baz":"qux"}

Scenario: test replace with missing parent key should fail [24a4e3c2-7d41-4893-b5e8-47bad5c27b9d]
	Given the document {"bar":"baz"}
	When I build the patch [{"op":"replace","path":"/foo/bar","value":false}]
	Then a patch exception should be thrown

Scenario: spurious patch properties [61d3a1df-dc0f-40ba-8a49-3e7c8f52cbd1]
	Given the document {"foo":1}
	When I build the patch [{"op":"test","path":"/foo","value":1,"spurious":1}]
	Then the patch result should equal {"foo":1}

Scenario: null value should be valid obj property [c210e6fc-61e1-47e4-8b34-a831783acafe]
	Given the document {"foo":null}
	When I build the patch [{"op":"test","path":"/foo","value":null}]
	Then the patch result should equal {"foo":null}

Scenario: null value should be valid obj property to be replaced with something truthy [4de46acd-7a14-4a91-92d9-4b674a7fd25b]
	Given the document {"foo":null}
	When I build the patch [{"op":"replace","path":"/foo","value":"truthy"}]
	Then the patch result should equal {"foo":"truthy"}

Scenario: null value should be valid obj property to be moved [476d64f7-d880-4f5c-a939-e5908dbd1107]
	Given the document {"foo":null}
	When I build the patch [{"op":"move","from":"/foo","path":"/bar"}]
	Then the patch result should equal {"bar":null}

Scenario: null value should be valid obj property to be copied [332eba97-5eea-4666-8b74-0eadf9293258]
	Given the document {"foo":null}
	When I build the patch [{"op":"copy","from":"/foo","path":"/bar"}]
	Then the patch result should equal {"foo":null,"bar":null}

Scenario: null value should be valid obj property to be removed [b4d63a16-5966-42aa-8da6-1da8302abfde]
	Given the document {"foo":null}
	When I build the patch [{"op":"remove","path":"/foo"}]
	Then the patch result should equal {}

Scenario: null value should still be valid obj property replace other value [3d7e279c-15cb-40e3-b814-c879414b6b38]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"replace","path":"/foo","value":null}]
	Then the patch result should equal {"foo":null}

Scenario: test should pass despite rearrangement [cc8bc008-b997-42f7-a925-774ae5799059]
	Given the document {"foo":{"foo":1,"bar":2}}
	When I build the patch [{"op":"test","path":"/foo","value":{"bar":2,"foo":1}}]
	Then the patch result should equal {"foo":{"foo":1,"bar":2}}

Scenario: test should pass despite (nested) rearrangement [f443fa43-21e4-4e5b-8d70-b60d09e79f58]
	Given the document {"foo":[{"foo":1,"bar":2}]}
	When I build the patch [{"op":"test","path":"/foo","value":[{"bar":2,"foo":1}]}]
	Then the patch result should equal {"foo":[{"foo":1,"bar":2}]}

Scenario: test should pass - no error [0115188d-9bc1-4659-ad69-8497bf4ebac7]
	Given the document {"foo":{"bar":[1,2,5,4]}}
	When I build the patch [{"op":"test","path":"/foo","value":{"bar":[1,2,5,4]}}]
	Then the patch result should equal {"foo":{"bar":[1,2,5,4]}}

Scenario: Undescribed scenario [b0bce60d-95fa-4387-b1c4-dba91e9ccf09]
	Given the document {"foo":{"bar":[1,2,5,4]}}
	When I build the patch [{"op":"test","path":"/foo","value":[1,2]}]
	Then a patch exception should be thrown

Scenario: Empty-string element [ff1fe5a1-25a2-4adb-ab85-5e60b1359b88]
	Given the document {"":1}
	When I build the patch [{"op":"test","path":"/","value":1}]
	Then the patch result should equal {"":1}

Scenario: Undescribed scenario [5697785f-fe60-4ac8-aee6-13fdc64f3f5f]
	Given the document {"foo":["bar","baz"],"":0,"a/b":1,"c%d":2,"e^f":3,"g|h":4,"i\\j":5,"k\u0022l":6," ":7,"m~n":8}
	When I build the patch [{"op":"test","path":"/foo","value":["bar","baz"]},{"op":"test","path":"/foo/0","value":"bar"},{"op":"test","path":"/","value":0},{"op":"test","path":"/a~1b","value":1},{"op":"test","path":"/c%d","value":2},{"op":"test","path":"/e^f","value":3},{"op":"test","path":"/g|h","value":4},{"op":"test","path":"/i\\j","value":5},{"op":"test","path":"/k\u0022l","value":6},{"op":"test","path":"/ ","value":7},{"op":"test","path":"/m~0n","value":8}]
	Then the patch result should equal {"":0," ":7,"a/b":1,"c%d":2,"e^f":3,"foo":["bar","baz"],"g|h":4,"i\\j":5,"k\u0022l":6,"m~n":8}

Scenario: Move to same location has no effect [2f1c624f-d3e8-44d9-84e6-c6b237aaebe7]
	Given the document {"foo":1}
	When I build the patch [{"op":"move","from":"/foo","path":"/foo"}]
	Then the patch result should equal {"foo":1}

Scenario: Undescribed scenario [8f3aafb2-2b8e-4304-9b2e-7a17519d5b48]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	When I build the patch [{"op":"move","from":"/foo","path":"/bar"}]
	Then the patch result should equal {"baz":[{"qux":"hello"}],"bar":1}

Scenario: Undescribed scenario [588904b5-4d28-4c73-a03a-272bdeeee95b]
	Given the document {"baz":[{"qux":"hello"}],"bar":1}
	When I build the patch [{"op":"move","from":"/baz/0/qux","path":"/baz/1"}]
	Then the patch result should equal {"baz":[{},"hello"],"bar":1}

Scenario: Undescribed scenario [efbbd6d9-744a-4233-bdf4-843e5dabc51b]
	Given the document {"baz":[{"qux":"hello"}],"bar":1}
	When I build the patch [{"op":"copy","from":"/baz/0","path":"/boo"}]
	Then the patch result should equal {"baz":[{"qux":"hello"}],"bar":1,"boo":{"qux":"hello"}}

Scenario: replacing the root of the document is possible with add [d3c14d71-795d-49d2-94d9-ea8ca0b21ea6]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"add","path":"","value":{"baz":"qux"}}]
	Then the patch result should equal {"baz":"qux"}

Scenario: Adding to "/-" adds to the end of the array [cf9050a1-e8ee-4c40-9597-5cce581f0aef]
	Given the document [1,2]
	When I build the patch [{"op":"add","path":"/-","value":{"foo":["bar","baz"]}}]
	Then the patch result should equal [1,2,{"foo":["bar","baz"]}]

Scenario: Adding to "/-" adds to the end of the array, even n levels down [201cb75d-5b05-40f6-9550-1452a791c7d7]
	Given the document [1,2,[3,[4,5]]]
	When I build the patch [{"op":"add","path":"/2/1/-","value":{"foo":["bar","baz"]}}]
	Then the patch result should equal [1,2,[3,[4,5,{"foo":["bar","baz"]}]]]

Scenario: test remove with bad number should fail [e59edb4d-1b3e-4a3f-8270-5d0bbe21b47a]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	When I build the patch [{"op":"remove","path":"/baz/1e0/qux"}]
	Then a patch exception should be thrown

Scenario: test remove on array [0d6ea036-6b42-43ff-a791-fc0246206dec]
	Given the document [1,2,3,4]
	When I build the patch [{"op":"remove","path":"/0"}]
	Then the patch result should equal [2,3,4]

Scenario: test repeated removes [e97d5d7c-a164-4fe3-bb5b-9015a43e5cd8]
	Given the document [1,2,3,4]
	When I build the patch [{"op":"remove","path":"/1"},{"op":"remove","path":"/2"}]
	Then the patch result should equal [1,3]

Scenario: test remove with bad index should fail [e8b8021f-c567-4509-a0a4-bb44e123a133]
	Given the document [1,2,3,4]
	When I build the patch [{"op":"remove","path":"/1e0"}]
	Then a patch exception should be thrown

Scenario: test replace with bad number should fail [9e86143b-6125-43df-ac49-d2a59cdeb87a]
	Given the document [""]
	When I build the patch [{"op":"replace","path":"/1e0","value":false}]
	Then a patch exception should be thrown

Scenario: test copy with bad number should fail [6eb8ffad-7d1e-4bae-ac8c-6d5224f706ae]
	Given the document {"baz":[1,2,3],"bar":1}
	When I build the patch [{"op":"copy","from":"/baz/1e0","path":"/boo"}]
	Then a patch exception should be thrown

Scenario: test move with bad number should fail [08c7cbf7-e7b4-425a-b01a-2e0d2e8a16eb]
	Given the document {"foo":1,"baz":[1,2,3,4]}
	When I build the patch [{"op":"move","from":"/baz/1e0","path":"/foo"}]
	Then a patch exception should be thrown

Scenario: test add with bad number should fail [fa81b59d-7484-4f0b-850c-3f4a34eeaf7e]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"add","path":"/1e0","value":"bar"}]
	Then a patch exception should be thrown

Scenario: missing 'path' parameter [95d68f2e-3c9c-49b4-9594-9088b3df06f2]
	Given the document {}
	When I build the patch [{"op":"add","value":"bar"}]
	Then a patch exception should be thrown

Scenario: 'path' parameter with null value [64bbe02a-0211-46fb-bfec-718ca0f2be28]
	Given the document {}
	When I build the patch [{"op":"add","path":null,"value":"bar"}]
	Then a patch exception should be thrown

Scenario: invalid JSON Pointer token [bc52a5d8-f270-44ef-a227-47e89c85f1db]
	Given the document {}
	When I build the patch [{"op":"add","path":"foo","value":"bar"}]
	Then a patch exception should be thrown

Scenario: missing 'value' parameter to add [11337063-147c-464d-8b37-637e24c679d2]
	Given the document [1]
	When I build the patch [{"op":"add","path":"/-"}]
	Then a patch exception should be thrown

Scenario: missing 'value' parameter to replace [3f38acc4-8d06-4b14-a1db-a4245764affb]
	Given the document [1]
	When I build the patch [{"op":"replace","path":"/0"}]
	Then a patch exception should be thrown

Scenario: missing 'value' parameter to test [48fa718d-d981-46d2-9873-8856e08b6bf5]
	Given the document [null]
	When I build the patch [{"op":"test","path":"/0"}]
	Then a patch exception should be thrown

Scenario: missing value parameter to test - where undef is falsy [a64c8a4c-50b1-482a-aaae-112f8b2f0fc7]
	Given the document [false]
	When I build the patch [{"op":"test","path":"/0"}]
	Then a patch exception should be thrown

Scenario: missing from parameter to copy [d9b1db5d-b436-4b32-85e1-56794fd40d12]
	Given the document [1]
	When I build the patch [{"op":"copy","path":"/-"}]
	Then a patch exception should be thrown

Scenario: missing from location to copy [523f794a-903d-4333-aa8e-ba598b282753]
	Given the document {"foo":1}
	When I build the patch [{"op":"copy","from":"/bar","path":"/foo"}]
	Then a patch exception should be thrown

Scenario: missing from parameter to move [60531b97-fd21-4fec-8600-9a3b72f8bf8a]
	Given the document {"foo":1}
	When I build the patch [{"op":"move","path":""}]
	Then a patch exception should be thrown

Scenario: missing from location to move [f304a57b-a673-4e83-8647-e80194ad9742]
	Given the document {"foo":1}
	When I build the patch [{"op":"move","from":"/bar","path":"/foo"}]
	Then a patch exception should be thrown

Scenario: unrecognized op should fail [bfef6cf2-0a07-413d-b9e0-fdad17b9feb8]
	Given the document {"foo":1}
	When I build the patch [{"op":"spam","path":"/foo","value":1}]
	Then a patch exception should be thrown

Scenario: test with bad array number that has leading zeros [a45b9d74-44c1-440e-b953-afc41d861ce3]
	Given the document ["foo","bar"]
	When I build the patch [{"op":"test","path":"/00","value":"foo"}]
	Then a patch exception should be thrown

Scenario: test with bad array number that has leading zeros [97a2edd5-49e4-49b2-8ecf-0d57af64e474]
	Given the document ["foo","bar"]
	When I build the patch [{"op":"test","path":"/01","value":"bar"}]
	Then a patch exception should be thrown

Scenario: Removing nonexistent field [a5648d60-de1f-4a63-b2fc-c25d938e39fc]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"remove","path":"/baz"}]
	Then a patch exception should be thrown

Scenario: Removing deep nonexistent path [7e463a86-b46c-4e4b-ad65-0e1d5370d7a5]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"remove","path":"/missing1/missing2"}]
	Then a patch exception should be thrown

Scenario: Removing nonexistent index [d8b40880-0aa0-412d-b03d-0d0b2033a850]
	Given the document ["foo","bar"]
	When I build the patch [{"op":"remove","path":"/2"}]
	Then a patch exception should be thrown

Scenario: Patch with different capitalisation than doc [b093e984-b045-46c2-bde1-34537de463ff]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"add","path":"/FOO","value":"BAR"}]
	Then the patch result should equal {"foo":"bar","FOO":"BAR"}
