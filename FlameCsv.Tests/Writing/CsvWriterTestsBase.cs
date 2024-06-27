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
    public static TheoryData<string, bool, char?, CsvFieldEscaping, bool, int> ArgsWithBufferSize()
    {
        var values = from arr in Args()
        from bufferSize in (int[])[-1, 17, 128, 1024, 4096]
                     select new { arr, bufferSize };

        var data = new TheoryData<string, bool, char?, CsvFieldEscaping, bool, int>();

        foreach (var x in values)
        {
            data.Add((string)x.arr[0], (bool)x.arr[1], (char?)x.arr[2], (CsvFieldEscaping)x.arr[3], (bool)x.arr[4], x.bufferSize);
        }

        return data;
    }

    public static TheoryData<string, bool, char?, CsvFieldEscaping, bool> Args()
    {
        var values = from newline in (string[])["\r\n", "\n"]
                     from header in (bool[])[true, false]
                     from escape in (char?[])['^', null]
                     from quoting in (CsvFieldEscaping[])[CsvFieldEscaping.Never, CsvFieldEscaping.AlwaysQuote, CsvFieldEscaping.Auto]
                     from sourceGen in (bool[])[true, false]
                     select new { newline, header, escape, quoting, sourceGen};

        var data = new TheoryData<string, bool, char?, CsvFieldEscaping, bool>();

        foreach (var x in values)
        {
            data.Add(x.newline, x.header, x.escape, x.quoting, x.sourceGen);
        }

        return data;
    }
}
