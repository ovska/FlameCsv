using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FlameCsv.Binding;
using FlameCsv.Writing;
using static FastExpressionCompiler.ExpressionCompiler;

namespace FlameCsv.Tests.Writing;

public abstract class CsvWriterTestsBase
{
    public static IEnumerable<object[]> Args =>
        from newline in (string[])["\r\n", "\n"]
        from header in (bool[])[true, false]
        from escape in (char?[])['^', null]
        from quoting in (CsvFieldEscaping[])[CsvFieldEscaping.Never, CsvFieldEscaping.AlwaysQuote, CsvFieldEscaping.Auto]
        from sourceGen in (bool[])[true, false]
        select new object[] { newline, header, escape, quoting, sourceGen };

    public static IEnumerable<object[]> ArgsWithBufferSize =>
        from arr in Args
        from bufferSize in (int[])[-1, 17, 128, 1024, 4096]
        select (object[])[.. arr, bufferSize];
}
