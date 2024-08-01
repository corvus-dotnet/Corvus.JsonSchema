string path = "./this/really/is/an/exceptionally/long/path/deep/down/in/the/mire/of/the/file/system/so/deep/in/fact/that/you/may/even/get/lost/tyring/to/find/yourself/and/you/will.definitely.find.that.it.will.require.at.least.a.very.little.bit.of.truncation.if.not.a.lot.of.truncation.you.just.see.if.it.doesnt.cs";

Console.WriteLine(path);
Console.WriteLine(PathTruncator.TruncatePath(path));