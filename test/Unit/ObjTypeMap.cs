using FlameCsv.Attributes;

namespace FlameCsv.Tests;

[CsvTypeMap<char, TestData.Obj>]
public partial class ObjCharTypeMap;

[CsvTypeMap<byte, TestData.Obj>]
public partial class ObjByteTypeMap;
