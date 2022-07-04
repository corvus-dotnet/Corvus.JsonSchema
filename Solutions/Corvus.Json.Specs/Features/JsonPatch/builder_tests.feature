
Feature: builder_tests

Scenario: empty list, empty docs [c36f774f-b828-487f-ac5f-5df3855c8673]
	Given the document {}
	When I build the patch []
	Then the patch result should equal {}

Scenario: empty patch list [c6afc980-0af2-4bb2-a1bf-507d81e60c35]
	Given the document {"foo":1}
	When I build the patch []
	Then the patch result should equal {"foo":1}

Scenario: rearrangements OK? [c46b1f3d-cafa-4f03-b160-b7ae508d0372]
	Given the document {"foo":1,"bar":2}
	When I build the patch []
	Then the patch result should equal {"bar":2,"foo":1}

Scenario: rearrangements OK?  How about one level down ... array [cc2185e8-d721-4ec8-951b-363dfe68b631]
	Given the document [{"foo":1,"bar":2}]
	When I build the patch []
	Then the patch result should equal [{"bar":2,"foo":1}]

Scenario: rearrangements OK?  How about one level down... [54a36702-d7c7-47e8-a9c3-5bab2adee7df]
	Given the document {"foo":{"foo":1,"bar":2}}
	When I build the patch []
	Then the patch result should equal {"foo":{"bar":2,"foo":1}}

Scenario: add replaces any existing field [baa9c739-90be-46f1-98e6-9dba5ba4d84d]
	Given the document {"foo":null}
	When I build the patch [{"op":"add","path":"/foo","value":1}]
	Then the patch result should equal {"foo":1}

Scenario: toplevel array [42a80b00-a20c-48e9-8daf-5cc9721fa21b]
	Given the document []
	When I build the patch [{"op":"add","path":"/0","value":"foo"}]
	Then the patch result should equal ["foo"]

Scenario: toplevel array, no change [7d1d40e9-ba35-4e38-91db-452e9077eadb]
	Given the document ["foo"]
	When I build the patch []
	Then the patch result should equal ["foo"]

Scenario: toplevel object, numeric string [1b2cea70-87fc-4508-a62f-ad2fd95b4abc]
	Given the document {}
	When I build the patch [{"op":"add","path":"/foo","value":"1"}]
	Then the patch result should equal {"foo":"1"}

Scenario: toplevel object, integer [305c2622-6af0-44cc-b4b1-ac1859767d46]
	Given the document {}
	When I build the patch [{"op":"add","path":"/foo","value":1}]
	Then the patch result should equal {"foo":1}

Scenario: replace object document with array document? [49a5c306-7192-4801-9eca-af457f6a899b]
	Given the document {}
	When I build the patch [{"op":"add","path":"","value":[]}]
	Then the patch result should equal []

Scenario: replace array document with object document? [414590b9-eae8-48d2-a17f-455320d95c13]
	Given the document []
	When I build the patch [{"op":"add","path":"","value":{}}]
	Then the patch result should equal {}

Scenario: append to root array document? [28ecd851-903b-4b84-ba49-86482600f914]
	Given the document []
	When I build the patch [{"op":"add","path":"/-","value":"hi"}]
	Then the patch result should equal ["hi"]

Scenario: Add, / target [c3eed083-dab5-478d-a924-ca458840a7c7]
	Given the document {}
	When I build the patch [{"op":"add","path":"/","value":1}]
	Then the patch result should equal {"":1}

Scenario: Add, /foo/ deep target (trailing slash) [81c61a49-7c2c-4c34-abe9-e7197809865b]
	Given the document {"foo":{}}
	When I build the patch [{"op":"add","path":"/foo/","value":1}]
	Then the patch result should equal {"foo":{"":1}}

Scenario: Add composite value at top level [c66ac73c-1a7e-44ac-9b36-3aae5c7178c9]
	Given the document {"foo":1}
	When I build the patch [{"op":"add","path":"/bar","value":[1,2]}]
	Then the patch result should equal {"foo":1,"bar":[1,2]}

Scenario: Add into composite value [2f68330a-86ef-4b33-9516-311e0a81ea27]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	When I build the patch [{"op":"add","path":"/baz/0/foo","value":"world"}]
	Then the patch result should equal {"foo":1,"baz":[{"qux":"hello","foo":"world"}]}

Scenario: Undescribed scenario [450b0465-ed49-4c25-9f24-d968a8fb9d92]
	Given the document {"bar":[1,2]}
	When I build the patch [{"op":"add","path":"/bar/8","value":"5"}]
	Then a patch exception should be thrown

Scenario: Undescribed scenario [18106116-1746-441e-9d23-3c8fa3116c4c]
	Given the document {"bar":[1,2]}
	When I build the patch [{"op":"add","path":"/bar/-1","value":"5"}]
	Then a patch exception should be thrown

Scenario: Undescribed scenario [1267d0f9-391d-4e12-9564-e79aa2518fe7]
	Given the document {"foo":1}
	When I build the patch [{"op":"add","path":"/bar","value":true}]
	Then the patch result should equal {"foo":1,"bar":true}

Scenario: Undescribed scenario [4efd615d-6650-4080-8538-93a7195fe07c]
	Given the document {"foo":1}
	When I build the patch [{"op":"add","path":"/bar","value":false}]
	Then the patch result should equal {"foo":1,"bar":false}

Scenario: Undescribed scenario [86d1e9d6-ab35-4b7f-bffa-ed420cf3181f]
	Given the document {"foo":1}
	When I build the patch [{"op":"add","path":"/bar","value":null}]
	Then the patch result should equal {"foo":1,"bar":null}

Scenario: 0 can be an array index or object element name [7b1440c5-559b-436f-8184-125cc667dc11]
	Given the document {"foo":1}
	When I build the patch [{"op":"add","path":"/0","value":"bar"}]
	Then the patch result should equal {"foo":1,"0":"bar"}

Scenario: Undescribed scenario [f8d5ef95-e425-487e-86b5-835e4749ecac]
	Given the document ["foo"]
	When I build the patch [{"op":"add","path":"/1","value":"bar"}]
	Then the patch result should equal ["foo","bar"]

Scenario: Undescribed scenario [1dd15b10-d07f-4e26-83a4-9675d9060f19]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"add","path":"/1","value":"bar"}]
	Then the patch result should equal ["foo","bar","sil"]

Scenario: Undescribed scenario [15d2171a-90f9-4fdd-a0f7-9332dc180d84]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"add","path":"/0","value":"bar"}]
	Then the patch result should equal ["bar","foo","sil"]

Scenario: push item to array via last index + 1 [5e35510c-3ad6-41cd-a025-16ae5eb0588a]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"add","path":"/2","value":"bar"}]
	Then the patch result should equal ["foo","sil","bar"]

Scenario: add item to array at index > length should fail [04057505-333e-4f0e-abc4-5ce218c326b1]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"add","path":"/3","value":"bar"}]
	Then a patch exception should be thrown

Scenario: test against implementation-specific numeric parsing [590e6db6-fb8d-4d89-9b63-744bf5d1c010]
	Given the document {"1e0":"foo"}
	When I build the patch [{"op":"test","path":"/1e0","value":"foo"}]
	Then the patch result should equal {"1e0":"foo"}

Scenario: test with bad number should fail [7efdb4f3-56b6-484d-95a3-ac9ec8c9a985]
	Given the document ["foo","bar"]
	When I build the patch [{"op":"test","path":"/1e0","value":"bar"}]
	Then a patch exception should be thrown

Scenario: Undescribed scenario [519b3f54-fa3b-4985-9621-d5ad369ca526]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"add","path":"/bar","value":42}]
	Then a patch exception should be thrown

Scenario: value in array add not flattened [84a5bfc6-645c-4329-81b9-f0ee66bbe6d3]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"add","path":"/1","value":["bar","baz"]}]
	Then the patch result should equal ["foo",["bar","baz"],"sil"]

Scenario: Undescribed scenario [8507b5c3-c344-49ed-9716-b0539f41566f]
	Given the document {"foo":1,"bar":[1,2,3,4]}
	When I build the patch [{"op":"remove","path":"/bar"}]
	Then the patch result should equal {"foo":1}

Scenario: Undescribed scenario [aea73a41-22e9-41db-803f-f48ec68f2ef8]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	When I build the patch [{"op":"remove","path":"/baz/0/qux"}]
	Then the patch result should equal {"foo":1,"baz":[{}]}

Scenario: Undescribed scenario [a34429cc-2167-481a-9cb8-c5909b7e4e46]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	When I build the patch [{"op":"replace","path":"/foo","value":[1,2,3,4]}]
	Then the patch result should equal {"foo":[1,2,3,4],"baz":[{"qux":"hello"}]}

Scenario: Undescribed scenario [4ff38d7e-3214-43f4-8228-f3ff8fb8c487]
	Given the document {"foo":[1,2,3,4],"baz":[{"qux":"hello"}]}
	When I build the patch [{"op":"replace","path":"/baz/0/qux","value":"world"}]
	Then the patch result should equal {"foo":[1,2,3,4],"baz":[{"qux":"world"}]}

Scenario: Undescribed scenario [84b42860-33b0-4f91-ac6d-db312635fdd2]
	Given the document ["foo"]
	When I build the patch [{"op":"replace","path":"/0","value":"bar"}]
	Then the patch result should equal ["bar"]

Scenario: Undescribed scenario [eee2a057-b875-4d74-81dd-044f88af8df2]
	Given the document [""]
	When I build the patch [{"op":"replace","path":"/0","value":0}]
	Then the patch result should equal [0]

Scenario: Undescribed scenario [e84227af-d08e-4079-b3a2-9ad4a0ae9ba6]
	Given the document [""]
	When I build the patch [{"op":"replace","path":"/0","value":true}]
	Then the patch result should equal [true]

Scenario: Undescribed scenario [968c37c7-7131-4a70-add1-5911fc3a942a]
	Given the document [""]
	When I build the patch [{"op":"replace","path":"/0","value":false}]
	Then the patch result should equal [false]

Scenario: Undescribed scenario [3806b1c1-4858-4859-9db4-1c1b47803cad]
	Given the document [""]
	When I build the patch [{"op":"replace","path":"/0","value":null}]
	Then the patch result should equal [null]

Scenario: value in array replace not flattened [fa2a912e-dc71-4385-9e42-8c5bc670003d]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"replace","path":"/1","value":["bar","baz"]}]
	Then the patch result should equal ["foo",["bar","baz"]]

Scenario: replace whole document [58e125ce-f15a-4d5f-9896-26b82410de41]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"replace","path":"","value":{"baz":"qux"}}]
	Then the patch result should equal {"baz":"qux"}

Scenario: test replace with missing parent key should fail [22548568-e0c2-4677-ac77-a52edad06667]
	Given the document {"bar":"baz"}
	When I build the patch [{"op":"replace","path":"/foo/bar","value":false}]
	Then a patch exception should be thrown

Scenario: spurious patch properties [7c4fb161-83cd-4f7f-ba01-b1f74e36e504]
	Given the document {"foo":1}
	When I build the patch [{"op":"test","path":"/foo","value":1,"spurious":1}]
	Then the patch result should equal {"foo":1}

Scenario: null value should be valid obj property [8c1516f4-bb49-4e1b-b3c4-ec60916ec3b4]
	Given the document {"foo":null}
	When I build the patch [{"op":"test","path":"/foo","value":null}]
	Then the patch result should equal {"foo":null}

Scenario: null value should be valid obj property to be replaced with something truthy [b5a91954-40b9-4474-a970-f266affee701]
	Given the document {"foo":null}
	When I build the patch [{"op":"replace","path":"/foo","value":"truthy"}]
	Then the patch result should equal {"foo":"truthy"}

Scenario: null value should be valid obj property to be moved [b028265c-8cdc-4875-bbc9-c854cd947602]
	Given the document {"foo":null}
	When I build the patch [{"op":"move","from":"/foo","path":"/bar"}]
	Then the patch result should equal {"bar":null}

Scenario: null value should be valid obj property to be copied [e70205ed-409e-41ff-88b3-a8b861b22161]
	Given the document {"foo":null}
	When I build the patch [{"op":"copy","from":"/foo","path":"/bar"}]
	Then the patch result should equal {"foo":null,"bar":null}

Scenario: null value should be valid obj property to be removed [04594be0-625a-45b0-91f9-e4fe8b83e9d5]
	Given the document {"foo":null}
	When I build the patch [{"op":"remove","path":"/foo"}]
	Then the patch result should equal {}

Scenario: null value should still be valid obj property replace other value [91cbcdb4-9998-49c8-a29c-fd9813e85aea]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"replace","path":"/foo","value":null}]
	Then the patch result should equal {"foo":null}

Scenario: test should pass despite rearrangement [7e5bc604-0bbd-4100-8ca8-0f9327abe0ad]
	Given the document {"foo":{"foo":1,"bar":2}}
	When I build the patch [{"op":"test","path":"/foo","value":{"bar":2,"foo":1}}]
	Then the patch result should equal {"foo":{"foo":1,"bar":2}}

Scenario: test should pass despite (nested) rearrangement [45afa31d-d7fa-447a-8f3e-72194702c947]
	Given the document {"foo":[{"foo":1,"bar":2}]}
	When I build the patch [{"op":"test","path":"/foo","value":[{"bar":2,"foo":1}]}]
	Then the patch result should equal {"foo":[{"foo":1,"bar":2}]}

Scenario: test should pass - no error [1512d0dc-40a2-4201-b3d5-eaae48572486]
	Given the document {"foo":{"bar":[1,2,5,4]}}
	When I build the patch [{"op":"test","path":"/foo","value":{"bar":[1,2,5,4]}}]
	Then the patch result should equal {"foo":{"bar":[1,2,5,4]}}

Scenario: Undescribed scenario [01cc9321-5037-45f4-b9f4-9598c3436dfa]
	Given the document {"foo":{"bar":[1,2,5,4]}}
	When I build the patch [{"op":"test","path":"/foo","value":[1,2]}]
	Then a patch exception should be thrown

Scenario: Empty-string element [2695727e-04b9-49f5-911b-bb6d6ebdd9e7]
	Given the document {"":1}
	When I build the patch [{"op":"test","path":"/","value":1}]
	Then the patch result should equal {"":1}

Scenario: Undescribed scenario [a4fa924e-badd-4cfe-abe0-0e441632da73]
	Given the document {"foo":["bar","baz"],"":0,"a/b":1,"c%d":2,"e^f":3,"g|h":4,"i\\j":5,"k\u0022l":6," ":7,"m~n":8}
	When I build the patch [{"op":"test","path":"/foo","value":["bar","baz"]},{"op":"test","path":"/foo/0","value":"bar"},{"op":"test","path":"/","value":0},{"op":"test","path":"/a~1b","value":1},{"op":"test","path":"/c%d","value":2},{"op":"test","path":"/e^f","value":3},{"op":"test","path":"/g|h","value":4},{"op":"test","path":"/i\\j","value":5},{"op":"test","path":"/k\u0022l","value":6},{"op":"test","path":"/ ","value":7},{"op":"test","path":"/m~0n","value":8}]
	Then the patch result should equal {"":0," ":7,"a/b":1,"c%d":2,"e^f":3,"foo":["bar","baz"],"g|h":4,"i\\j":5,"k\u0022l":6,"m~n":8}

Scenario: Move to same location has no effect [2fdddf85-5365-49ee-9a2f-190513bf3bf0]
	Given the document {"foo":1}
	When I build the patch [{"op":"move","from":"/foo","path":"/foo"}]
	Then the patch result should equal {"foo":1}

Scenario: Undescribed scenario [7ae5b1bb-c76f-45ea-98ef-4827042cee29]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	When I build the patch [{"op":"move","from":"/foo","path":"/bar"}]
	Then the patch result should equal {"baz":[{"qux":"hello"}],"bar":1}

Scenario: Undescribed scenario [0f2161b3-c3d0-41fb-880d-16263b25a935]
	Given the document {"baz":[{"qux":"hello"}],"bar":1}
	When I build the patch [{"op":"move","from":"/baz/0/qux","path":"/baz/1"}]
	Then the patch result should equal {"baz":[{},"hello"],"bar":1}

Scenario: Undescribed scenario [fc56315f-012b-49d7-933b-67199cf723f4]
	Given the document {"baz":[{"qux":"hello"}],"bar":1}
	When I build the patch [{"op":"copy","from":"/baz/0","path":"/boo"}]
	Then the patch result should equal {"baz":[{"qux":"hello"}],"bar":1,"boo":{"qux":"hello"}}

Scenario: replacing the root of the document is possible with add [e38109b9-7eca-4955-9b62-a2ab7480c3c8]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"add","path":"","value":{"baz":"qux"}}]
	Then the patch result should equal {"baz":"qux"}

Scenario: Adding to "/-" adds to the end of the array [6a7afd38-a8fe-4664-b6e6-b28e719da914]
	Given the document [1,2]
	When I build the patch [{"op":"add","path":"/-","value":{"foo":["bar","baz"]}}]
	Then the patch result should equal [1,2,{"foo":["bar","baz"]}]

Scenario: Adding to "/-" adds to the end of the array, even n levels down [01aecb28-d3d3-47a1-8ffd-95230a0ba5e6]
	Given the document [1,2,[3,[4,5]]]
	When I build the patch [{"op":"add","path":"/2/1/-","value":{"foo":["bar","baz"]}}]
	Then the patch result should equal [1,2,[3,[4,5,{"foo":["bar","baz"]}]]]

Scenario: test remove with bad number should fail [ae14dc6c-ecd3-4a59-96c9-109be64e3c4d]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	When I build the patch [{"op":"remove","path":"/baz/1e0/qux"}]
	Then a patch exception should be thrown

Scenario: test remove on array [5471142a-2d1f-4f56-a954-6fb0dc315d7d]
	Given the document [1,2,3,4]
	When I build the patch [{"op":"remove","path":"/0"}]
	Then the patch result should equal [2,3,4]

Scenario: test repeated removes [4b7605d5-45c1-4109-8ffe-67cba6ea4cf1]
	Given the document [1,2,3,4]
	When I build the patch [{"op":"remove","path":"/1"},{"op":"remove","path":"/2"}]
	Then the patch result should equal [1,3]

Scenario: test remove with bad index should fail [ebd7c5a3-a77f-4948-b61d-c222f0fb9b52]
	Given the document [1,2,3,4]
	When I build the patch [{"op":"remove","path":"/1e0"}]
	Then a patch exception should be thrown

Scenario: test replace with bad number should fail [5784b1f7-f1c5-4f08-a733-16086039d7bb]
	Given the document [""]
	When I build the patch [{"op":"replace","path":"/1e0","value":false}]
	Then a patch exception should be thrown

Scenario: test copy with bad number should fail [0ee7c13a-5fee-4ba1-a64b-f86d502839d4]
	Given the document {"baz":[1,2,3],"bar":1}
	When I build the patch [{"op":"copy","from":"/baz/1e0","path":"/boo"}]
	Then a patch exception should be thrown

Scenario: test move with bad number should fail [9563386e-10df-4694-8230-3e350cec6ded]
	Given the document {"foo":1,"baz":[1,2,3,4]}
	When I build the patch [{"op":"move","from":"/baz/1e0","path":"/foo"}]
	Then a patch exception should be thrown

Scenario: test add with bad number should fail [10da95af-526e-4bbd-9787-f39c5b71a6f5]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"add","path":"/1e0","value":"bar"}]
	Then a patch exception should be thrown

Scenario: missing 'path' parameter [7feecf83-6720-45eb-a6b9-358071847f0f]
	Given the document {}
	When I build the patch [{"op":"add","value":"bar"}]
	Then a patch exception should be thrown

Scenario: 'path' parameter with null value [dc9e3172-5173-43fc-a241-133050c98d26]
	Given the document {}
	When I build the patch [{"op":"add","path":null,"value":"bar"}]
	Then a patch exception should be thrown

Scenario: invalid JSON Pointer token [ea66cedc-5bb7-415a-bcc1-fba66005252c]
	Given the document {}
	When I build the patch [{"op":"add","path":"foo","value":"bar"}]
	Then a patch exception should be thrown

Scenario: missing 'value' parameter to add [8b171af9-4bd1-4f55-b86c-81818df015f5]
	Given the document [1]
	When I build the patch [{"op":"add","path":"/-"}]
	Then a patch exception should be thrown

Scenario: missing 'value' parameter to replace [6a6d30a6-1359-4b7d-86a3-98ab60405961]
	Given the document [1]
	When I build the patch [{"op":"replace","path":"/0"}]
	Then a patch exception should be thrown

Scenario: missing 'value' parameter to test [87cd39ae-ef2b-44b5-8fee-090b4c7cc0e6]
	Given the document [null]
	When I build the patch [{"op":"test","path":"/0"}]
	Then a patch exception should be thrown

Scenario: missing value parameter to test - where undef is falsy [8a46f2b4-158b-477f-b652-7bdf402a2bdd]
	Given the document [false]
	When I build the patch [{"op":"test","path":"/0"}]
	Then a patch exception should be thrown

Scenario: missing from parameter to copy [91862ed0-8988-4289-acfb-649eaa6e0620]
	Given the document [1]
	When I build the patch [{"op":"copy","path":"/-"}]
	Then a patch exception should be thrown

Scenario: missing from location to copy [c3d41da9-c1c0-430f-afcc-d12e07109fce]
	Given the document {"foo":1}
	When I build the patch [{"op":"copy","from":"/bar","path":"/foo"}]
	Then a patch exception should be thrown

Scenario: missing from parameter to move [1859ba64-ff90-4d54-a61c-7145e12674c8]
	Given the document {"foo":1}
	When I build the patch [{"op":"move","path":""}]
	Then a patch exception should be thrown

Scenario: missing from location to move [3ac773bc-6e48-45d0-8efb-43c84e38438f]
	Given the document {"foo":1}
	When I build the patch [{"op":"move","from":"/bar","path":"/foo"}]
	Then a patch exception should be thrown

Scenario: unrecognized op should fail [498c2d7b-d427-457f-88aa-7893e5f2cbc1]
	Given the document {"foo":1}
	When I build the patch [{"op":"spam","path":"/foo","value":1}]
	Then a patch exception should be thrown

Scenario: test with bad array number that has leading zeros [b1ce2625-cb87-437c-ae2a-fa22dc046bf9]
	Given the document ["foo","bar"]
	When I build the patch [{"op":"test","path":"/00","value":"foo"}]
	Then a patch exception should be thrown

Scenario: test with bad array number that has leading zeros [8efdf262-7952-4e55-a64c-0e0b89c3109f]
	Given the document ["foo","bar"]
	When I build the patch [{"op":"test","path":"/01","value":"bar"}]
	Then a patch exception should be thrown

Scenario: Removing nonexistent field [6c2a28b5-8fcf-45ca-8477-bae16cef54fc]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"remove","path":"/baz"}]
	Then a patch exception should be thrown

Scenario: Removing deep nonexistent path [07563f11-8121-4c43-a7fa-3ce29c5d389b]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"remove","path":"/missing1/missing2"}]
	Then a patch exception should be thrown

Scenario: Removing nonexistent index [d00ede8d-6c3a-44af-9fe3-d450aeb0ca56]
	Given the document ["foo","bar"]
	When I build the patch [{"op":"remove","path":"/2"}]
	Then a patch exception should be thrown

Scenario: Patch with different capitalisation than doc [a9f1da2a-37cf-49ae-9d4d-fe6cd0ef1c2a]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"add","path":"/FOO","value":"BAR"}]
	Then the patch result should equal {"foo":"bar","FOO":"BAR"}
