// <copyright file="JsonPatchBuilderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using Corvus.Json.Patch;
using Corvus.Json.Patch.Model;
using Xunit;

namespace Corvus.Json.Specs.Tests.JsonPatch
{
    public class JsonPatchBuilderTests
    {
        [Fact]
        public void EmptyListEmptyDocs_65aaab02_aefb_4fde_87d6_a4cd639fd616()
        {
            JsonAny result = ApplyBuilder("""{}""", """[]""");
            Assert.Equal(JsonAny.Parse("""{}"""), result);
        }

        [Fact]
        public void EmptyPatchList_8cb8e43e_1adb_4610_9414_54125b728e86()
        {
            JsonAny result = ApplyBuilder("""{"foo":1}""", """[]""");
            Assert.Equal(JsonAny.Parse("""{"foo":1}"""), result);
        }

        [Fact]
        public void RearrangementsOK_fd727ff0_11f7_48d1_b897_bd628fcc89e0()
        {
            JsonAny result = ApplyBuilder("""{"foo":1,"bar":2}""", """[]""");
            Assert.Equal(JsonAny.Parse("""{"bar":2,"foo":1}"""), result);
        }

        [Fact]
        public void RearrangementsOKHowAboutOneLevelDownArray_c9871db4_19dc_4df5_a993_ecbf75864759()
        {
            JsonAny result = ApplyBuilder("""[{"foo":1,"bar":2}]""", """[]""");
            Assert.Equal(JsonAny.Parse("""[{"bar":2,"foo":1}]"""), result);
        }

        [Fact]
        public void RearrangementsOKHowAboutOneLevelDown_190c047d_22d2_4b79_aa21_29a91d1a824a()
        {
            JsonAny result = ApplyBuilder("""{"foo":{"foo":1,"bar":2}}""", """[]""");
            Assert.Equal(JsonAny.Parse("""{"foo":{"bar":2,"foo":1}}"""), result);
        }

        [Fact]
        public void AddReplacesAnyExistingField_1f3cbf58_186c_4367_bede_41152713b8a7()
        {
            JsonAny result = ApplyBuilder("""{"foo":null}""", """[{"op":"add","path":"/foo","value":1}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":1}"""), result);
        }

        [Fact]
        public void ToplevelArray_8a8beb5b_c40c_4d69_8634_9e4299c0f87d()
        {
            JsonAny result = ApplyBuilder("""[]""", """[{"op":"add","path":"/0","value":"foo"}]""");
            Assert.Equal(JsonAny.Parse("""["foo"]"""), result);
        }

        [Fact]
        public void ToplevelArrayNoChange_24579b57_6a5b_43f8_b06d_5aa461c16cec()
        {
            JsonAny result = ApplyBuilder("""["foo"]""", """[]""");
            Assert.Equal(JsonAny.Parse("""["foo"]"""), result);
        }

        [Fact]
        public void ToplevelObjectNumericString_2c9fc267_3c8f_49f7_b45d_3d1193e3b663()
        {
            JsonAny result = ApplyBuilder("""{}""", """[{"op":"add","path":"/foo","value":"1"}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":"1"}"""), result);
        }

        [Fact]
        public void ToplevelObjectInteger_bdc22e11_5f6b_4da2_b5c9_c52bdee0b9e9()
        {
            JsonAny result = ApplyBuilder("""{}""", """[{"op":"add","path":"/foo","value":1}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":1}"""), result);
        }

        [Fact]
        public void ReplaceObjectDocumentWithArrayDocument_8c7c0603_f1c3_46dd_aa6a_04e3942dd6dd()
        {
            JsonAny result = ApplyBuilder("""{}""", """[{"op":"add","path":"","value":[]}]""");
            Assert.Equal(JsonAny.Parse("""[]"""), result);
        }

        [Fact]
        public void ReplaceArrayDocumentWithObjectDocument_f9ce0936_0d8f_4a4a_9156_5b09f93acbce()
        {
            JsonAny result = ApplyBuilder("""[]""", """[{"op":"add","path":"","value":{}}]""");
            Assert.Equal(JsonAny.Parse("""{}"""), result);
        }

        [Fact]
        public void AppendToRootArrayDocument_c4c190da_d30d_4013_8579_12b3bf241004()
        {
            JsonAny result = ApplyBuilder("""[]""", """[{"op":"add","path":"/-","value":"hi"}]""");
            Assert.Equal(JsonAny.Parse("""["hi"]"""), result);
        }

        [Fact]
        public void AddTarget_8784babb_7e20_499d_a908_91821811a119()
        {
            JsonAny result = ApplyBuilder("""{}""", """[{"op":"add","path":"/","value":1}]""");
            Assert.Equal(JsonAny.Parse("""{"":1}"""), result);
        }

        [Fact]
        public void AddFooDeepTargetTrailingSlash_7bc4e13d_350a_468d_aabf_95cb9c159f02()
        {
            JsonAny result = ApplyBuilder("""{"foo":{}}""", """[{"op":"add","path":"/foo/","value":1}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":{"":1}}"""), result);
        }

        [Fact]
        public void AddCompositeValueAtTopLevel_274582b1_e86f_4210_95d2_03f322c46edf()
        {
            JsonAny result = ApplyBuilder("""{"foo":1}""", """[{"op":"add","path":"/bar","value":[1,2]}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":1,"bar":[1,2]}"""), result);
        }

        [Fact]
        public void AddIntoCompositeValue_417d3604_1750_48fa_9975_1e8b3a8a9969()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":1,"baz":[{"qux":"hello"}]}""",
                """[{"op":"add","path":"/baz/0/foo","value":"world"}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":1,"baz":[{"qux":"hello","foo":"world"}]}"""), result);
        }

        [Fact]
        public void Scenario_08e94755_5da6_4dd3_99ce_ef24e5263180()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """{"bar":[1,2]}""",
                """[{"op":"add","path":"/bar/8","value":"5"}]"""));
        }

        [Fact]
        public void Scenario_ddc8463d_ac32_415c_9327_dab095ac6c67()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """{"bar":[1,2]}""",
                """[{"op":"add","path":"/bar/-1","value":"5"}]"""));
        }

        [Fact]
        public void Scenario_37ad638a_cced_4eae_b456_fac64e5bf805()
        {
            JsonAny result = ApplyBuilder("""{"foo":1}""", """[{"op":"add","path":"/bar","value":true}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":1,"bar":true}"""), result);
        }

        [Fact]
        public void Scenario_037ebfd9_24e0_4f8c_b5ae_53ad67fadc46()
        {
            JsonAny result = ApplyBuilder("""{"foo":1}""", """[{"op":"add","path":"/bar","value":false}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":1,"bar":false}"""), result);
        }

        [Fact]
        public void Scenario_bf0dd719_0516_4516_819f_9d09bf074719()
        {
            JsonAny result = ApplyBuilder("""{"foo":1}""", """[{"op":"add","path":"/bar","value":null}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":1,"bar":null}"""), result);
        }

        [Fact]
        public void _0CanBeAnArrayIndexOrObjectElementName_be4c77a4_f6be_43f4_a049_bf0a92cda610()
        {
            JsonAny result = ApplyBuilder("""{"foo":1}""", """[{"op":"add","path":"/0","value":"bar"}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":1,"0":"bar"}"""), result);
        }

        [Fact]
        public void Scenario_6b22435d_bb8b_4230_b591_4ea7b6375020()
        {
            JsonAny result = ApplyBuilder("""["foo"]""", """[{"op":"add","path":"/1","value":"bar"}]""");
            Assert.Equal(JsonAny.Parse("""["foo","bar"]"""), result);
        }

        [Fact]
        public void Scenario_02c5cdb2_5557_47ac_a3f0_0c56d8509bd3()
        {
            JsonAny result = ApplyBuilder("""["foo","sil"]""", """[{"op":"add","path":"/1","value":"bar"}]""");
            Assert.Equal(JsonAny.Parse("""["foo","bar","sil"]"""), result);
        }

        [Fact]
        public void Scenario_9a5e6ea3_9684_4bb9_8ee8_dc9ef0553f3f()
        {
            JsonAny result = ApplyBuilder("""["foo","sil"]""", """[{"op":"add","path":"/0","value":"bar"}]""");
            Assert.Equal(JsonAny.Parse("""["bar","foo","sil"]"""), result);
        }

        [Fact]
        public void PushItemToArrayViaLastIndexPlus1_e3226b41_8eda_449e_89b8_3ac904a3644e()
        {
            JsonAny result = ApplyBuilder("""["foo","sil"]""", """[{"op":"add","path":"/2","value":"bar"}]""");
            Assert.Equal(JsonAny.Parse("""["foo","sil","bar"]"""), result);
        }

        [Fact]
        public void AddItemToArrayAtIndexLengthShouldFail_c75ae750_5e27_473a_aa82_f1110e0bbcf8()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """["foo","sil"]""",
                """[{"op":"add","path":"/3","value":"bar"}]"""));
        }

        [Fact]
        public void TestAgainstImplementationSpecificNumericParsing_4784abe9_aa06_49ed_a09d_fb51b67b4cfd()
        {
            JsonAny result = ApplyBuilder(
                """{"1e0":"foo"}""",
                """[{"op":"test","path":"/1e0","value":"foo"}]""");
            Assert.Equal(JsonAny.Parse("""{"1e0":"foo"}"""), result);
        }

        [Fact]
        public void TestWithBadNumberShouldFail_68bf187a_87b1_4926_9062_60c87fc9c4bd()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """["foo","bar"]""",
                """[{"op":"test","path":"/1e0","value":"bar"}]"""));
        }

        [Fact]
        public void Scenario_f220a130_b30f_401b_b7fa_228bf926e17c()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """["foo","sil"]""",
                """[{"op":"add","path":"/bar","value":42}]"""));
        }

        [Fact]
        public void ValueInArrayAddNotFlattened_1dfdb9b2_9752_470d_92bf_eae8679e3bbf()
        {
            JsonAny result = ApplyBuilder(
                """["foo","sil"]""",
                """[{"op":"add","path":"/1","value":["bar","baz"]}]""");
            Assert.Equal(JsonAny.Parse("""["foo",["bar","baz"],"sil"]"""), result);
        }

        [Fact]
        public void Scenario_43e8870f_53a7_439f_9253_b3646e0eed16()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":1,"bar":[1,2,3,4]}""",
                """[{"op":"remove","path":"/bar"}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":1}"""), result);
        }

        [Fact]
        public void Scenario_48c3940a_3ddd_4a33_9e22_412afbae822e()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":1,"baz":[{"qux":"hello"}]}""",
                """[{"op":"remove","path":"/baz/0/qux"}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":1,"baz":[{}]}"""), result);
        }

        [Fact]
        public void Scenario_b6c4985e_9c17_459d_a27d_ec4c3fb809a8()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":1,"baz":[{"qux":"hello"}]}""",
                """[{"op":"replace","path":"/foo","value":[1,2,3,4]}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":[1,2,3,4],"baz":[{"qux":"hello"}]}"""), result);
        }

        [Fact]
        public void Scenario_61ce2c72_e500_4541_b231_5bdd4d3034ae()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":[1,2,3,4],"baz":[{"qux":"hello"}]}""",
                """[{"op":"replace","path":"/baz/0/qux","value":"world"}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":[1,2,3,4],"baz":[{"qux":"world"}]}"""), result);
        }

        [Fact]
        public void Scenario_93aec3b7_67a6_44a6_af04_b9afd8c044fb()
        {
            JsonAny result = ApplyBuilder("""["foo"]""", """[{"op":"replace","path":"/0","value":"bar"}]""");
            Assert.Equal(JsonAny.Parse("""["bar"]"""), result);
        }

        [Fact]
        public void Scenario_685dbb91_8b95_4521_a74c_ec28953950ea()
        {
            JsonAny result = ApplyBuilder("""[""]""", """[{"op":"replace","path":"/0","value":0}]""");
            Assert.Equal(JsonAny.Parse("""[0]"""), result);
        }

        [Fact]
        public void Scenario_af78f25e_80cc_4d51_b529_d0ad39d2d861()
        {
            JsonAny result = ApplyBuilder("""[""]""", """[{"op":"replace","path":"/0","value":true}]""");
            Assert.Equal(JsonAny.Parse("""[true]"""), result);
        }

        [Fact]
        public void Scenario_5e8a4a20_c55b_483b_86fa_4e83276f1c1e()
        {
            JsonAny result = ApplyBuilder("""[""]""", """[{"op":"replace","path":"/0","value":false}]""");
            Assert.Equal(JsonAny.Parse("""[false]"""), result);
        }

        [Fact]
        public void Scenario_76ff6468_12ed_44ba_957b_2c8666982c0f()
        {
            JsonAny result = ApplyBuilder("""[""]""", """[{"op":"replace","path":"/0","value":null}]""");
            Assert.Equal(JsonAny.Parse("""[null]"""), result);
        }

        [Fact]
        public void ValueInArrayReplaceNotFlattened_00c4473a_b473_4905_8a7e_a6995eca1f3d()
        {
            JsonAny result = ApplyBuilder(
                """["foo","sil"]""",
                """[{"op":"replace","path":"/1","value":["bar","baz"]}]""");
            Assert.Equal(JsonAny.Parse("""["foo",["bar","baz"]]"""), result);
        }

        [Fact]
        public void ReplaceWholeDocument_f83d6995_5fe1_4885_badd_005d88b9ab04()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":"bar"}""",
                """[{"op":"replace","path":"","value":{"baz":"qux"}}]""");
            Assert.Equal(JsonAny.Parse("""{"baz":"qux"}"""), result);
        }

        [Fact]
        public void TestReplaceWithMissingParentKeyShouldFail_a193fb0b_20d4_4bde_9cc7_0cedf6e5b448()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """{"bar":"baz"}""",
                """[{"op":"replace","path":"/foo/bar","value":false}]"""));
        }

        [Fact]
        public void SpuriousPatchProperties_32a8af79_0a70_41cd_8f8c_3db00f4f16c1()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":1}""",
                """[{"op":"test","path":"/foo","value":1,"spurious":1}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":1}"""), result);
        }

        [Fact]
        public void NullValueShouldBeValidObjProperty_1ff7f875_8b7f_44e7_8df3_7cfa6d7f5373()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":null}""",
                """[{"op":"test","path":"/foo","value":null}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":null}"""), result);
        }

        [Fact]
        public void NullValueShouldBeValidObjPropertyToBeReplacedWithSomethingTruthy_b38afd08_8077_4473_ae1e_57ea1541025f()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":null}""",
                """[{"op":"replace","path":"/foo","value":"truthy"}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":"truthy"}"""), result);
        }

        [Fact]
        public void NullValueShouldBeValidObjPropertyToBeMoved_c5878c94_f6d5_455f_aec7_b1d655437f32()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":null}""",
                """[{"op":"move","from":"/foo","path":"/bar"}]""");
            Assert.Equal(JsonAny.Parse("""{"bar":null}"""), result);
        }

        [Fact]
        public void NullValueShouldBeValidObjPropertyToBeCopied_7c10c0d3_105b_412a_8591_5f33526bbe67()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":null}""",
                """[{"op":"copy","from":"/foo","path":"/bar"}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":null,"bar":null}"""), result);
        }

        [Fact]
        public void NullValueShouldBeValidObjPropertyToBeRemoved_a01de2cd_2fd1_4b47_8f72_c91be7f0352a()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":null}""",
                """[{"op":"remove","path":"/foo"}]""");
            Assert.Equal(JsonAny.Parse("""{}"""), result);
        }

        [Fact]
        public void NullValueShouldStillBeValidObjPropertyReplaceOtherValue_4064068a_f1ef_428a_bc4c_528b1c3217d7()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":"bar"}""",
                """[{"op":"replace","path":"/foo","value":null}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":null}"""), result);
        }

        [Fact]
        public void TestShouldPassDespiteRearrangement_0810c115_5617_474b_8fd8_9989ed1fd392()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":{"foo":1,"bar":2}}""",
                """[{"op":"test","path":"/foo","value":{"bar":2,"foo":1}}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":{"foo":1,"bar":2}}"""), result);
        }

        [Fact]
        public void TestShouldPassDespiteNestedRearrangement_c60ee4cc_7c3e_409b_8611_b446b22d9f95()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":[{"foo":1,"bar":2}]}""",
                """[{"op":"test","path":"/foo","value":[{"bar":2,"foo":1}]}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":[{"foo":1,"bar":2}]}"""), result);
        }

        [Fact]
        public void TestShouldPassNoError_9377b712_efb9_45e0_befb_70b51d4aa266()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":{"bar":[1,2,5,4]}}""",
                """[{"op":"test","path":"/foo","value":{"bar":[1,2,5,4]}}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":{"bar":[1,2,5,4]}}"""), result);
        }

        [Fact]
        public void Scenario_62e8a073_547d_4d13_869e_6a593bdd9f10()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """{"foo":{"bar":[1,2,5,4]}}""",
                """[{"op":"test","path":"/foo","value":[1,2]}]"""));
        }

        [Fact]
        public void EmptyStringElement_83011935_b8be_41c8_93b5_e1ebff0df279()
        {
            JsonAny result = ApplyBuilder(
                """{"":1}""",
                """[{"op":"test","path":"/","value":1}]""");
            Assert.Equal(JsonAny.Parse("""{"":1}"""), result);
        }

        [Fact]
        public void Scenario_7eaab010_5498_4e7f_af97_f15c23b6e5df()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":["bar","baz"],"":0,"a/b":1,"c%d":2,"e^f":3,"g|h":4,"i\\j":5,"k\u0022l":6," ":7,"m~n":8}""",
                """[{"op":"test","path":"/foo","value":["bar","baz"]},{"op":"test","path":"/foo/0","value":"bar"},{"op":"test","path":"/","value":0},{"op":"test","path":"/a~1b","value":1},{"op":"test","path":"/c%d","value":2},{"op":"test","path":"/e^f","value":3},{"op":"test","path":"/g|h","value":4},{"op":"test","path":"/i\\j","value":5},{"op":"test","path":"/k\u0022l","value":6},{"op":"test","path":"/ ","value":7},{"op":"test","path":"/m~0n","value":8}]""");
            Assert.Equal(JsonAny.Parse("""{"":0," ":7,"a/b":1,"c%d":2,"e^f":3,"foo":["bar","baz"],"g|h":4,"i\\j":5,"k\u0022l":6,"m~n":8}"""), result);
        }

        [Fact]
        public void MoveToSameLocationHasNoEffect_e4271114_a290_4900_913d_04726dfaa660()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":1}""",
                """[{"op":"move","from":"/foo","path":"/foo"}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":1}"""), result);
        }

        [Fact]
        public void Scenario_d9eab1d9_da1a_4cba_9e82_30d48a18319c()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":1,"baz":[{"qux":"hello"}]}""",
                """[{"op":"move","from":"/foo","path":"/bar"}]""");
            Assert.Equal(JsonAny.Parse("""{"baz":[{"qux":"hello"}],"bar":1}"""), result);
        }

        [Fact]
        public void Scenario_1cef0973_6b06_41c5_b755_fb0cc6243451()
        {
            JsonAny result = ApplyBuilder(
                """{"baz":[{"qux":"hello"}],"bar":1}""",
                """[{"op":"move","from":"/baz/0/qux","path":"/baz/1"}]""");
            Assert.Equal(JsonAny.Parse("""{"baz":[{},"hello"],"bar":1}"""), result);
        }

        [Fact]
        public void Scenario_4cb50dcc_95d7_4783_be4f_9b25c9fa20c3()
        {
            JsonAny result = ApplyBuilder(
                """{"baz":[{"qux":"hello"}],"bar":1}""",
                """[{"op":"copy","from":"/baz/0","path":"/boo"}]""");
            Assert.Equal(JsonAny.Parse("""{"baz":[{"qux":"hello"}],"bar":1,"boo":{"qux":"hello"}}"""), result);
        }

        [Fact]
        public void ReplacingTheRootOfTheDocumentIsPossibleWithAdd_49b6d9d7_bf21_4c65_8b71_99e192ccd28c()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":"bar"}""",
                """[{"op":"add","path":"","value":{"baz":"qux"}}]""");
            Assert.Equal(JsonAny.Parse("""{"baz":"qux"}"""), result);
        }

        [Fact]
        public void AddingToAddsToTheEndOfTheArray_befe892b_d41d_4b23_940c_dd7aa80adb44()
        {
            JsonAny result = ApplyBuilder(
                """[1,2]""",
                """[{"op":"add","path":"/-","value":{"foo":["bar","baz"]}}]""");
            Assert.Equal(JsonAny.Parse("""[1,2,{"foo":["bar","baz"]}]"""), result);
        }

        [Fact]
        public void AddingToAddsToTheEndOfTheArrayEvenNLevelsDown_d083e522_2bd6_4685_8b53_09334bf178b8()
        {
            JsonAny result = ApplyBuilder(
                """[1,2,[3,[4,5]]]""",
                """[{"op":"add","path":"/2/1/-","value":{"foo":["bar","baz"]}}]""");
            Assert.Equal(JsonAny.Parse("""[1,2,[3,[4,5,{"foo":["bar","baz"]}]]]"""), result);
        }

        [Fact]
        public void TestRemoveWithBadNumberShouldFail_d2656873_5c60_490e_8922_538baa7b5072()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """{"foo":1,"baz":[{"qux":"hello"}]}""",
                """[{"op":"remove","path":"/baz/1e0/qux"}]"""));
        }

        [Fact]
        public void TestRemoveOnArray_78a297dd_d225_4d49_be4b_9a0038064efc()
        {
            JsonAny result = ApplyBuilder("""[1,2,3,4]""", """[{"op":"remove","path":"/0"}]""");
            Assert.Equal(JsonAny.Parse("""[2,3,4]"""), result);
        }

        [Fact]
        public void TestRepeatedRemoves_f79646e7_a591_43d5_bcbb_0fb05c90e7bc()
        {
            JsonAny result = ApplyBuilder(
                """[1,2,3,4]""",
                """[{"op":"remove","path":"/1"},{"op":"remove","path":"/2"}]""");
            Assert.Equal(JsonAny.Parse("""[1,3]"""), result);
        }

        [Fact]
        public void TestRemoveWithBadIndexShouldFail_34d59d1d_7bc9_404c_97f7_8ae3f72dbd5a()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """[1,2,3,4]""",
                """[{"op":"remove","path":"/1e0"}]"""));
        }

        [Fact]
        public void TestReplaceWithBadNumberShouldFail_3dccba9b_5602_4a60_9e87_da7e1c19bff5()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """[""]""",
                """[{"op":"replace","path":"/1e0","value":false}]"""));
        }

        [Fact]
        public void TestCopyWithBadNumberShouldFail_1d9c5583_2623_486d_99a2_d7322a6211b7()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """{"baz":[1,2,3],"bar":1}""",
                """[{"op":"copy","from":"/baz/1e0","path":"/boo"}]"""));
        }

        [Fact]
        public void TestMoveWithBadNumberShouldFail_df5b67d4_a6a5_4569_9dd0_fc572f3cbdac()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """{"foo":1,"baz":[1,2,3,4]}""",
                """[{"op":"move","from":"/baz/1e0","path":"/foo"}]"""));
        }

        [Fact]
        public void TestAddWithBadNumberShouldFail_4dabce6f_1256_4c1b_ae8d_2641fc60edbe()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """["foo","sil"]""",
                """[{"op":"add","path":"/1e0","value":"bar"}]"""));
        }

        [Fact]
        public void MissingPathParameter_6b00ae33_7f59_4172_869f_6a9dea76319f()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """{}""",
                """[{"op":"add","value":"bar"}]"""));
        }

        [Fact]
        public void PathParameterWithNullValue_447e3338_0952_4295_bb14_240e95c09f6d()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """{}""",
                """[{"op":"add","path":null,"value":"bar"}]"""));
        }

        [Fact]
        public void InvalidJSONPointerToken_daf56299_c10d_4568_8d10_4d78e7b5281a()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """{}""",
                """[{"op":"add","path":"foo","value":"bar"}]"""));
        }

        [Fact]
        public void MissingValueParameterToAdd_f9876c70_3514_49cb_a4b7_f5421a3202c6()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """[1]""",
                """[{"op":"add","path":"/-"}]"""));
        }

        [Fact]
        public void MissingValueParameterToReplace_2f13da60_0c23_4d01_aae0_b5977ef6823b()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """[1]""",
                """[{"op":"replace","path":"/0"}]"""));
        }

        [Fact]
        public void MissingValueParameterToTest_49ea2fb8_0307_4c68_add5_77a15c8947df()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """[null]""",
                """[{"op":"test","path":"/0"}]"""));
        }

        [Fact]
        public void MissingValueParameterToTestWhereUndefIsFalsy_6f6675f2_e8cf_4f95_8c45_5d08418c476e()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """[false]""",
                """[{"op":"test","path":"/0"}]"""));
        }

        [Fact]
        public void MissingFromParameterToCopy_c72e1a45_9622_4382_9f96_2a45510a5d90()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """[1]""",
                """[{"op":"copy","path":"/-"}]"""));
        }

        [Fact]
        public void MissingFromLocationToCopy_febefd40_b430_4f87_9b25_54e1dbb01525()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """{"foo":1}""",
                """[{"op":"copy","from":"/bar","path":"/foo"}]"""));
        }

        [Fact]
        public void MissingFromParameterToMove_c51e7387_94c6_48a3_91b3_6499bf463d10()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """{"foo":1}""",
                """[{"op":"move","path":""}]"""));
        }

        [Fact]
        public void MissingFromLocationToMove_94cc8d0f_3d1d_4135_beaf_98baaadd080f()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """{"foo":1}""",
                """[{"op":"move","from":"/bar","path":"/foo"}]"""));
        }

        [Fact]
        public void UnrecognizedOpShouldFail_beb508c6_5fe8_455f_84e8_579f303f35b2()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """{"foo":1}""",
                """[{"op":"spam","path":"/foo","value":1}]"""));
        }

        [Fact]
        public void TestWithBadArrayNumberThatHasLeadingZeros_3f310d3b_f144_483d_b0d4_d924f2df0a73()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """["foo","bar"]""",
                """[{"op":"test","path":"/00","value":"foo"}]"""));
        }

        [Fact]
        public void TestWithBadArrayNumberThatHasLeadingZeros_59de35f8_0f04_4eb6_a4be_d341855e8257()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """["foo","bar"]""",
                """[{"op":"test","path":"/01","value":"bar"}]"""));
        }

        [Fact]
        public void RemovingNonexistentField_ab020c89_a5ac_44ea_b2e2_ac9769f9129a()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """{"foo":"bar"}""",
                """[{"op":"remove","path":"/baz"}]"""));
        }

        [Fact]
        public void RemovingDeepNonexistentPath_d1df167b_9135_49a4_91da_e7054c9bf070()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """{"foo":"bar"}""",
                """[{"op":"remove","path":"/missing1/missing2"}]"""));
        }

        [Fact]
        public void RemovingNonexistentIndex_6d62d9f9_184e_44c9_914e_29264ea9873b()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """["foo","bar"]""",
                """[{"op":"remove","path":"/2"}]"""));
        }

        [Fact]
        public void PatchWithDifferentCapitalisationThanDoc_5252afb2_09e0_4726_bc4d_b178be18e3a4()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":"bar"}""",
                """[{"op":"add","path":"/FOO","value":"BAR"}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":"bar","FOO":"BAR"}"""), result);
        }

        [Fact]
        public void _41AddWithMissingObject_e9311282_4ed5_4580_85bc_e9b3e3d09814()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """{"q":{"bar":2}}""",
                """[{"op":"add","path":"/a/b","value":1}]"""));
        }

        [Fact]
        public void A1AddingAnObjectMember_c048fa20_b3d8_4aa3_bd44_077db15733e4()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":"bar"}""",
                """[{"op":"add","path":"/baz","value":"qux"}]""");
            Assert.Equal(JsonAny.Parse("""{"baz":"qux","foo":"bar"}"""), result);
        }

        [Fact]
        public void A2AddingAnArrayElement_c4026a32_8b55_42b4_a466_81719b076125()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":["bar","baz"]}""",
                """[{"op":"add","path":"/foo/1","value":"qux"}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":["bar","qux","baz"]}"""), result);
        }

        [Fact]
        public void A3RemovingAnObjectMember_61493012_a08e_4dab_9831_8cc64e37e571()
        {
            JsonAny result = ApplyBuilder(
                """{"baz":"qux","foo":"bar"}""",
                """[{"op":"remove","path":"/baz"}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":"bar"}"""), result);
        }

        [Fact]
        public void A4RemovingAnArrayElement_ab4e5a07_0a4c_4027_ad49_71513a886102()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":["bar","qux","baz"]}""",
                """[{"op":"remove","path":"/foo/1"}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":["bar","baz"]}"""), result);
        }

        [Fact]
        public void A5ReplacingAValue_32231887_dcc1_4ea3_9fbf_90e2801b6c71()
        {
            JsonAny result = ApplyBuilder(
                """{"baz":"qux","foo":"bar"}""",
                """[{"op":"replace","path":"/baz","value":"boo"}]""");
            Assert.Equal(JsonAny.Parse("""{"baz":"boo","foo":"bar"}"""), result);
        }

        [Fact]
        public void A6MovingAValue_5a770e0f_1221_4d08_af07_94f57ae60e31()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":{"bar":"baz","waldo":"fred"},"qux":{"corge":"grault"}}""",
                """[{"op":"move","from":"/foo/waldo","path":"/qux/thud"}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":{"bar":"baz"},"qux":{"corge":"grault","thud":"fred"}}"""), result);
        }

        [Fact]
        public void A7MovingAnArrayElement_e905c322_0663_4d90_a304_d7304fa542c3()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":["all","grass","cows","eat"]}""",
                """[{"op":"move","from":"/foo/1","path":"/foo/3"}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":["all","cows","eat","grass"]}"""), result);
        }

        [Fact]
        public void A8TestingAValueSuccess_bf9eb3c7_a173_450f_ae90_fec054e12672()
        {
            JsonAny result = ApplyBuilder(
                """{"baz":"qux","foo":["a",2,"c"]}""",
                """[{"op":"test","path":"/baz","value":"qux"},{"op":"test","path":"/foo/1","value":2}]""");
            Assert.Equal(JsonAny.Parse("""{"baz":"qux","foo":["a",2,"c"]}"""), result);
        }

        [Fact]
        public void A9TestingAValueError_618332cc_204d_4be4_8aef_4bf953f163b0()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """{"baz":"qux"}""",
                """[{"op":"test","path":"/baz","value":"bar"}]"""));
        }

        [Fact]
        public void A10AddingANestedMemberObject_79442047_997b_4072_934e_170210f4562d()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":"bar"}""",
                """[{"op":"add","path":"/child","value":{"grandchild":{}}}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":"bar","child":{"grandchild":{}}}"""), result);
        }

        [Fact]
        public void A11IgnoringUnrecognizedElements_436ca681_15f4_4dec_b15e_410f257d33c9()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":"bar"}""",
                """[{"op":"add","path":"/baz","value":"qux","xyz":123}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":"bar","baz":"qux"}"""), result);
        }

        [Fact]
        public void A12AddingToANonExistentTarget_22a24524_0c28_4c67_a7dd_70867650b80c()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """{"foo":"bar"}""",
                """[{"op":"add","path":"/baz/bat","value":"qux"}]"""));
        }

        [Fact]
        public void A14EscapeOrdering_7f165fed_3833_4adb_9377_2edfd0ac462c()
        {
            JsonAny result = ApplyBuilder(
                """{"/":9,"~1":10}""",
                """[{"op":"test","path":"/~01","value":10}]""");
            Assert.Equal(JsonAny.Parse("""{"/":9,"~1":10}"""), result);
        }

        [Fact]
        public void A15ComparingStringsAndNumbers_17a617c3_c274_415a_842f_f56de1641bf1()
        {
            Assert.Throws<JsonPatchException>(() => ApplyBuilder(
                """{"/":9,"~1":10}""",
                """[{"op":"test","path":"/~01","value":"10"}]"""));
        }

        [Fact]
        public void A16AddingAnArrayValue_1b31dcd6_0b18_427e_8aa3_d56a503ff01a()
        {
            JsonAny result = ApplyBuilder(
                """{"foo":["bar"]}""",
                """[{"op":"add","path":"/foo/-","value":["abc","def"]}]""");
            Assert.Equal(JsonAny.Parse("""{"foo":["bar",["abc","def"]]}"""), result);
        }

        private static JsonAny ApplyBuilder(string docJson, string patchJson)
        {
            JsonAny doc = JsonAny.Parse(docJson);
            var patchOps = JsonPatchDocument.Parse(patchJson);
            PatchBuilder builder = doc.BeginPatch();
            foreach (JsonPatchDocument.PatchOperation operation in patchOps.EnumerateArray())
            {
                string op = (string)operation.Op;
                switch (op)
                {
                    case "add":
                        JsonPatchDocument.AddOperation add = operation.AsAddOperation;
                        if (!add.IsValid()) { throw new JsonPatchException("Invalid add operation."); }
                        builder = builder.Add(add.Value, operation.Path);
                        break;
                    case "copy":
                        JsonPatchDocument.CopyOperation copy = operation.AsCopyOperation;
                        if (!copy.IsValid()) { throw new JsonPatchException("Invalid copy operation."); }
                        builder = builder.Copy(copy.From, operation.Path);
                        break;
                    case "move":
                        JsonPatchDocument.MoveOperation move = operation.AsMoveOperation;
                        if (!move.IsValid()) { throw new JsonPatchException("Invalid move operation."); }
                        builder = builder.Move(move.From, operation.Path);
                        break;
                    case "remove":
                        builder = builder.Remove(operation.Path);
                        break;
                    case "replace":
                        JsonPatchDocument.ReplaceOperation replace = operation.AsReplaceOperation;
                        if (!replace.IsValid()) { throw new JsonPatchException("Invalid replace operation."); }
                        builder = builder.Replace(operation.Path, replace.Value);
                        break;
                    case "test":
                        JsonPatchDocument.TestOperation test = operation.AsTestOperation;
                        if (!test.IsValid()) { throw new JsonPatchException("Invalid test operation."); }
                        builder = builder.Test(operation.Path, test.Value);
                        break;
                    default:
                        throw new JsonPatchException("Unrecognized operation.");
                }
            }

            return builder.Value;
        }
    }
}