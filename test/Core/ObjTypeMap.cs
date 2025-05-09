using FlameCsv.Attributes;

namespace FlameCsv.Tests;

[CsvTypeMap<char, TestData.Obj>]
internal partial class ObjCharTypeMap;

[CsvTypeMap<byte, TestData.Obj>]
internal partial class ObjByteTypeMap;
