using FlameCsv.Attributes;

namespace FlameCsv.Tests.Writing;

[CsvTypeMap<char, TestData.Obj>]
internal partial class ObjCharTypeMap;

[CsvTypeMap<byte, TestData.Obj>]
internal partial class ObjByteTypeMap;
