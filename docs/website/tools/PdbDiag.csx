using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

string pdbPath = args[0];
string dllPath = args[1];
string typeFilter = args.Length > 2 ? args[2] : "ArrayEnumerator";

using var pdbStream = File.OpenRead(pdbPath);
var pdbProvider = MetadataReaderProvider.FromPortablePdbStream(pdbStream, MetadataStreamOptions.PrefetchMetadata);
var pdbReader = pdbProvider.GetMetadataReader();

using var peStream = File.OpenRead(dllPath);
var peReader = new PEReader(peStream);
var peMetadata = peReader.GetMetadataReader();

// Build document paths
var docPaths = new Dictionary<DocumentHandle, string>();
foreach (var dh in pdbReader.Documents)
{
    var doc = pdbReader.GetDocument(dh);
    if (!doc.Name.IsNil) docPaths[dh] = pdbReader.GetString(doc.Name);
}

// Build type names
var typeNames = new Dictionary<int, string>();
foreach (var th in peMetadata.TypeDefinitions)
{
    var td = peMetadata.GetTypeDefinition(th);
    string ns = td.Namespace.IsNil ? "" : peMetadata.GetString(td.Namespace);
    string name = peMetadata.GetString(td.Name);
    string full = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    typeNames[MetadataTokens.GetToken(th)] = full;
}

foreach (var mdiHandle in pdbReader.MethodDebugInformation)
{
    var mdi = pdbReader.GetMethodDebugInformation(mdiHandle);
    if (mdi.Document.IsNil) continue;
    if (!docPaths.TryGetValue(mdi.Document, out var filePath)) continue;

    int firstLine = int.MaxValue;
    foreach (var sp in mdi.GetSequencePoints())
    {
        if (!sp.IsHidden && sp.StartLine < firstLine) firstLine = sp.StartLine;
    }
    if (firstLine == int.MaxValue) continue;

    var methodHandle = MetadataTokens.MethodDefinitionHandle(MetadataTokens.GetRowNumber(mdiHandle));
    if (methodHandle.IsNil) continue;
    var methodDef = peMetadata.GetMethodDefinition(methodHandle);
    var declType = methodDef.GetDeclaringType();
    int typeToken = MetadataTokens.GetToken(declType);
    if (!typeNames.TryGetValue(typeToken, out var typeName)) continue;

    if (!typeName.Contains(typeFilter)) continue;

    string methodName = peMetadata.GetString(methodDef.Name);
    string fileName = Path.GetFileName(filePath);
    Console.WriteLine($"{typeName}.{methodName} -> {fileName}#L{firstLine}");
}
