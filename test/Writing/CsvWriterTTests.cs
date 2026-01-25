using System.Globalization;
using System.Text;
using FlameCsv.Attributes;
using FlameCsv.Converters.Formattable;
using FlameCsv.Extensions;

namespace FlameCsv.Tests.Writing;

public partial class CsvWriterTTests
{
    [CsvTypeMap<char, Testable>]
    private partial class TestableTypeMapChar;

    [CsvTypeMap<byte, Testable>]
    private partial class TestableTypeMapByte;

    private class Testable
    {
        public int A { get; set; }
        public int B { get; set; }
        public int C { get; set; }
    }

    [Fact]
    public static void Should_Cache_Materializer()
    {
        Assert.Equal(
            "0,0,0\r\n0,0,0\r\n0,0,0\r\n0,0,0\r\n0,0,0\r\n",
            Impl<char>(writer =>
            {
                writer.WriteRecord(new Testable());
                writer.NextRecord();
                writer.WriteRecord(new Testable());
                writer.NextRecord();

                writer.WriteRecord(TestableTypeMapChar.Default, new Testable());
                writer.NextRecord();
                writer.WriteRecord(TestableTypeMapChar.Default, new Testable());
                writer.NextRecord();

                writer.WriteRecord(new Testable());
            })
        );
    }

    [Fact]
    public static void Should_Write_Record()
    {
        Assert.Equal(
            "1,2,3\r\n",
            Impl<char>(writer =>
            {
                writer.WriteRecord(
                    new Testable
                    {
                        A = 1,
                        B = 2,
                        C = 3,
                    }
                );
            })
        );

        Assert.Equal(
            "1,2,3\r\n",
            Impl<byte>(writer =>
            {
                writer.WriteRecord(
                    new Testable
                    {
                        A = 1,
                        B = 2,
                        C = 3,
                    }
                );
            })
        );

        Assert.Equal(
            "1,2,3\r\n",
            Impl<char>(writer =>
            {
                writer.WriteRecord(
                    TestableTypeMapChar.Default,
                    new Testable
                    {
                        A = 1,
                        B = 2,
                        C = 3,
                    }
                );
            })
        );

        Assert.Equal(
            "1,2,3\r\n",
            Impl<byte>(writer =>
            {
                writer.WriteRecord(
                    TestableTypeMapByte.Default,
                    new Testable
                    {
                        A = 1,
                        B = 2,
                        C = 3,
                    }
                );
            })
        );
    }

    [Fact]
    public static async Task Should_Advance_To_Next_Record()
    {
        Assert.Equal(
            "xyz\r\n",
            Impl<char>(writer =>
            {
                writer.WriteField("xyz");
                writer.NextRecord();
            })
        );

        Assert.Equal(
            "xyz\r\n",
            Impl<byte>(writer =>
            {
                writer.WriteField("xyz"u8);
                writer.NextRecord();
            })
        );

        Assert.Equal(
            "xyz\r\n",
            await Impl<char>(async writer =>
            {
                writer.WriteField("xyz");
                await writer.NextRecordAsync(TestContext.Current.CancellationToken);
            })
        );

        Assert.Equal(
            "xyz\r\n",
            await Impl<byte>(async writer =>
            {
                writer.WriteField("xyz"u8);
                await writer.NextRecordAsync(TestContext.Current.CancellationToken);
            })
        );

        using (var w = Csv.To(Stream.Null).ToWriter())
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await w.NextRecordAsync(new CancellationToken(true))
            );

            Assert.False(w.IsCompleted);
            w.Complete();
            Assert.True(w.IsCompleted);

            Assert.Throws<ObjectDisposedException>(() => w.NextRecord());
            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await w.NextRecordAsync(TestContext.Current.CancellationToken)
            );
        }
    }

    [Fact]
    public static void Should_Write_Field()
    {
        Assert.Equal("1\r\n", Impl<char>(writer => writer.WriteField(1)));
        Assert.Equal("1\r\n", Impl<byte>(writer => writer.WriteField(1)));

        Assert.Equal(
            "1\r\n",
            Impl<char>(writer =>
                writer.WriteField(new NumberTextConverter<int>(CsvOptions<char>.Default, NumberStyles.Integer), 1)
            )
        );

        Assert.Equal(
            "1\r\n",
            Impl<byte>(writer =>
                writer.WriteField(new NumberUtf8Converter<int>(CsvOptions<byte>.Default, NumberStyles.Integer), 1)
            )
        );

        Assert.Equal("1,2,3\r\n", Impl<char>(writer => writer.WriteFields(["1", "2", "3"])));
        Assert.Equal("1,2,3\r\n", Impl<byte>(writer => writer.WriteFields(["1", "2", "3"])));

        Assert.Equal("\"te,st\",\"te,st\"\r\n", Impl<char>(writer => writer.WriteFields(["te,st", "te,st"])));
        Assert.Equal("\"te,st\",\"te,st\"\r\n", Impl<byte>(writer => writer.WriteFields(["te,st", "te,st"])));

        Assert.Equal(
            "te,st,te,st\r\n",
            Impl<char>(writer => writer.WriteFields(["te,st", "te,st"], skipEscaping: true))
        );
        Assert.Equal(
            "te,st,te,st\r\n",
            Impl<byte>(writer => writer.WriteFields(["te,st", "te,st"], skipEscaping: true))
        );
    }

    [Fact]
    public static void Should_Write_Text()
    {
        Assert.Equal("\"te,st\"\r\n", Impl<char>(writer => writer.WriteField(value: "te,st")));
        Assert.Equal("\"te,st\"\r\n", Impl<byte>(writer => writer.WriteField(value: "te,st"u8)));
        Assert.Equal("\"te,st\"\r\n", Impl<char>(writer => writer.WriteField(chars: "te,st")));
        Assert.Equal("\"te,st\"\r\n", Impl<byte>(writer => writer.WriteField(chars: "te,st")));

        Assert.Equal("te,st\r\n", Impl<char>(writer => writer.WriteField(value: "te,st", skipEscaping: true)));
        Assert.Equal("te,st\r\n", Impl<byte>(writer => writer.WriteField(value: "te,st"u8, skipEscaping: true)));
        Assert.Equal("te,st\r\n", Impl<char>(writer => writer.WriteField(chars: "te,st", skipEscaping: true)));
        Assert.Equal("te,st\r\n", Impl<byte>(writer => writer.WriteField(chars: "te,st", skipEscaping: true)));
    }

    [Fact]
    public static void Should_Write_Final_Newline()
    {
        Assert.Equal(
            "xyz\r\n",
            Impl<char>(writer =>
            {
                writer.WriteField("xyz");
            })
        );

        Assert.Equal(
            "xyz\r\n",
            Impl<byte>(writer =>
            {
                writer.WriteField("xyz"u8);
            })
        );

        Assert.Equal(
            "xyz",
            Impl<char>(writer =>
            {
                writer.EnsureTrailingNewline = false;
                writer.WriteField("xyz");
            })
        );

        Assert.Equal(
            "xyz",
            Impl<byte>(writer =>
            {
                writer.EnsureTrailingNewline = false;
                writer.WriteField("xyz"u8);
            })
        );
    }

    [Fact]
    public static void Should_Write_Header()
    {
        Assert.Equal(
            "A,B,C\r\n",
            Impl<char>(writer =>
            {
                writer.WriteHeader("A", "B", "C");
            })
        );

        Assert.Equal(
            "A,B,C\r\n",
            Impl<byte>(writer =>
            {
                writer.WriteHeader("A", "B", "C");
            })
        );

        Assert.Equal(
            "A,B,C\r\n",
            Impl<char>(writer =>
            {
                writer.WriteHeader<Testable>();
            })
        );

        Assert.Equal(
            "A,B,C\r\n",
            Impl<byte>(writer =>
            {
                writer.WriteHeader<Testable>();
            })
        );

        Assert.Equal(
            "A,B,C\r\n",
            Impl<char>(writer =>
            {
                writer.WriteHeader(TestableTypeMapChar.Default);
            })
        );

        Assert.Equal(
            "A,B,C\r\n",
            Impl<byte>(writer =>
            {
                writer.WriteHeader(TestableTypeMapByte.Default);
            })
        );
    }

    [Fact]
    public static void Should_Write_Records()
    {
        const string chars = "A,B,C\r\n1,2,3\r\n4,5,6\r\n7,8,9\r\n";
        byte[] bytes = Encoding.UTF8.GetBytes(chars);

        Assert.Equal(
            chars,
            Impl<char>(writer =>
            {
                foreach (var record in Csv.From(chars).Enumerate())
                {
                    if (!writer.HeaderWritten)
                    {
                        writer.WriteHeader(in record);
                        writer.NextRecord();
                    }

                    writer.WriteRecord(in record);
                    Assert.Equal(3, writer.FieldIndex);
                    writer.NextRecord();
                }
            })
        );

        Assert.Equal(
            chars,
            Impl<byte>(writer =>
            {
                foreach (var record in Csv.From(bytes).Enumerate())
                {
                    if (!writer.HeaderWritten)
                    {
                        writer.WriteHeader(in record);
                        writer.NextRecord();
                    }

                    writer.WriteRecord(in record);
                    Assert.Equal(3, writer.FieldIndex);
                    writer.NextRecord();
                }
            })
        );

        const string trimmedChars = "A,C\r\n1,3\r\n4,6\r\n7,9\r\n";

        Assert.Equal(
            trimmedChars,
            Impl<char>(writer =>
            {
                foreach (var record in Csv.From(chars).Enumerate())
                {
                    if (!writer.HeaderWritten)
                    {
                        writer.WriteHeader("A", "C");
                        writer.NextRecord();
                    }

                    writer.WriteRecord(in record, [0, 2]);
                    Assert.Equal(2, writer.FieldIndex);
                    writer.NextRecord();

                    // nothing written for empty fields
                    writer.WriteRecord(in record, []);
                    Assert.Equal(0, writer.FieldIndex);
                }
            })
        );

        Assert.Equal(
            trimmedChars,
            Impl<byte>(writer =>
            {
                foreach (var record in Csv.From(bytes).Enumerate())
                {
                    if (!writer.HeaderWritten)
                    {
                        writer.WriteHeader("A", "C");
                        writer.NextRecord();
                    }

                    writer.WriteRecord(in record, [0, 2]);
                    Assert.Equal(2, writer.FieldIndex);
                    writer.NextRecord();

                    // nothing written for empty fields
                    writer.WriteRecord(in record, []);
                    Assert.Equal(0, writer.FieldIndex);
                }
            })
        );

        Assert.Equal(
            trimmedChars,
            Impl<char>(writer =>
            {
                foreach (var record in Csv.From(chars).Enumerate())
                {
                    if (!writer.HeaderWritten)
                    {
                        writer.WriteHeader("A", "C");
                        writer.NextRecord();
                    }

                    writer.WriteRecord(in record, ["A", "C"]);
                    Assert.Equal(2, writer.FieldIndex);
                    writer.NextRecord();
                }
            })
        );

        Assert.Equal(
            trimmedChars,
            Impl<byte>(writer =>
            {
                foreach (var record in Csv.From(bytes).Enumerate())
                {
                    if (!writer.HeaderWritten)
                    {
                        writer.WriteHeader("A", "C");
                        writer.NextRecord();
                    }

                    writer.WriteRecord(in record, ["A", "C"]);
                    Assert.Equal(2, writer.FieldIndex);
                    writer.NextRecord();
                }
            })
        );

        // different dialect
        Assert.Equal(
            chars,
            Impl<char>(writer =>
            {
                foreach (
                    var record in Csv.From(chars)
                        .Enumerate(new CsvOptions<char> { Trimming = CsvFieldTrimming.Leading })
                )
                {
                    if (!writer.HeaderWritten)
                    {
                        writer.WriteHeader(in record);
                        writer.NextRecord();
                    }

                    writer.WriteRecord(in record);
                    Assert.Equal(3, writer.FieldIndex);
                    writer.NextRecord();
                }
            })
        );

        // different dialect
        Assert.Equal(
            trimmedChars,
            Impl<char>(writer =>
            {
                foreach (
                    var record in Csv.From(chars)
                        .Enumerate(new CsvOptions<char> { Trimming = CsvFieldTrimming.Leading })
                )
                {
                    if (!writer.HeaderWritten)
                    {
                        writer.WriteHeader("A", "C");
                        writer.NextRecord();
                    }

                    writer.WriteRecord(in record, ["A", "C"]);
                    Assert.Equal(2, writer.FieldIndex);
                    writer.NextRecord();
                }
            })
        );
    }

    [Fact]
    public static void Should_Validate_Header_Options()
    {
        var options = new CsvOptions<char> { HasHeader = false };

        Assert.Throws<NotSupportedException>(() =>
            Impl<char>(writer =>
            {
                foreach (ref readonly var record in Csv.From("1,2,3").Enumerate(options))
                {
                    writer.WriteHeader(in record);
                }
            })
        );
    }

    [Fact]
    public static async Task Should_Not_Flush_On_Canceled_Or_Errored()
    {
        await using var tw = new StringWriter();

        await using (var writer = Csv.To(tw).ToWriter())
        {
            writer.WriteField("test");
            writer.Complete(new Exception());
        }

        Assert.Equal(string.Empty, tw.ToString());

        await using (var writer = Csv.To(tw).ToWriter())
        {
            writer.WriteField("test");
            await writer.CompleteAsync(new Exception(), CancellationToken.None);
        }

        Assert.Equal(string.Empty, tw.ToString());

        await using (var writer = Csv.To(tw).ToWriter())
        {
            writer.WriteField("test");
            await writer.CompleteAsync(null, new CancellationToken(true));
        }

        Assert.Equal(string.Empty, tw.ToString());
    }

    [Fact]
    public static async Task Should_Flush()
    {
        await using var tw = new StringWriter();
        await using var writer = Csv.To(tw).ToWriter();

        writer.WriteField("test");
        writer.NextRecord();
        writer.Flush();

        Assert.Equal("test\r\n", tw.ToString());

        writer.WriteField("test2");
        writer.NextRecord();
        await writer.FlushAsync(TestContext.Current.CancellationToken);

        Assert.Equal("test\r\ntest2\r\n", tw.ToString());

        writer.Complete();

        Assert.Throws<ObjectDisposedException>(() => writer.Flush());
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await writer.FlushAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public static void Should_Validate_CsvRecord_Fields()
    {
        const string data = "A,B,C\r\n1,2,3\r\n4,5,6\r\n7,8,9\r\n";

        Impl<char>(writer =>
        {
            foreach (var record in Csv.From(data).Enumerate(new CsvOptions<char> { Trimming = CsvFieldTrimming.Both }))
            {
                // "D" field does not exist
                Assert.Throws<ArgumentException>(() => writer.WriteRecord(in record, ["A", "D"]));
            }
        });

        Impl<char>(writer =>
        {
            foreach (
                var record in Csv.From(data)
                    .Enumerate(new CsvOptions<char> { Trimming = CsvFieldTrimming.Both, HasHeader = false })
            )
            {
                // no header configured
                Assert.Throws<NotSupportedException>(() => writer.WriteRecord(in record, ["A", "B"]));

                // out of range index
                Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteRecord(in record, [0, 4]));
            }
        });
    }

    private static string Impl<T>(Action<CsvWriter<T>> action, CsvOptions<T>? options = null)
        where T : unmanaged, IBinaryInteger<T>
    {
        var writer = GetWriter(out Func<string> getString, options);
        Exception? ex = null;
        try
        {
            action(writer);
        }
        catch (Exception e)
        {
            ex = e;
        }
        writer.Complete(ex);
        ex?.Rethrow();
        return getString();
    }

    private static async Task<string> Impl<T>(Func<CsvWriter<T>, Task> action, CsvOptions<T>? options = null)
        where T : unmanaged, IBinaryInteger<T>
    {
        var writer = GetWriter(out Func<string> getString, options);
        Exception? ex = null;
        try
        {
            await action(writer);
        }
        catch (Exception e)
        {
            ex = e;
        }

        await writer.CompleteAsync(ex, TestContext.Current.CancellationToken);
        return getString();
    }

    private static CsvWriter<T> GetWriter<T>(out Func<string> getString, CsvOptions<T>? options)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (typeof(T) == typeof(char))
        {
            var stringWriter = new StringWriter();
            getString = () => stringWriter.ToString();
            return (CsvWriter<T>)(object)Csv.To(stringWriter).ToWriter((CsvOptions<char>?)(object?)options);
        }

        if (typeof(T) == typeof(byte))
        {
            var stream = new MemoryStream();
            getString = () => Encoding.UTF8.GetString(stream.ToArray());
            return (CsvWriter<T>)(object)Csv.To(stream).ToWriter((CsvOptions<byte>?)(object?)options);
        }

        throw Token<T>.NotSupported;
    }
}
