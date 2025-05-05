using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using FlameCsv.Attributes;
using FlameCsv.Converters;
using FlameCsv.Exceptions;
using FlameCsv.IO;
using FlameCsv.Writing;

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
    public static void Should_Validate_Field_Count()
    {
        Assert.ThrowsAny<CsvWriteException>(() =>
        {
            Impl<char>(writer =>
            {
                writer.ExpectedFieldCount = 4;
                writer.WriteField("1");
                writer.WriteField("2");
                writer.WriteField("3");
                writer.NextRecord();
            });
        });
    }

    [Fact]
    public static async Task Should_Advance_To_Next_Record()
    {
        Assert.Equal(
            "xyz\r\n",
            Impl<char>(writer =>
            {
                writer.WriteRaw("xyz");
                writer.NextRecord();
            })
        );

        Assert.Equal(
            "xyz\r\n",
            Impl<byte>(writer =>
            {
                writer.WriteRaw("xyz"u8);
                writer.NextRecord();
            })
        );

        Assert.Equal(
            "xyz\r\n",
            await Impl<char>(async writer =>
            {
                writer.WriteRaw("xyz");
                await writer.NextRecordAsync(TestContext.Current.CancellationToken);
            })
        );

        Assert.Equal(
            "xyz\r\n",
            await Impl<byte>(async writer =>
            {
                writer.WriteRaw("xyz"u8);
                await writer.NextRecordAsync(TestContext.Current.CancellationToken);
            })
        );

        using (var w = CsvWriter.Create(Stream.Null))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await w.NextRecordAsync(new CancellationToken(true))
            );

            Assert.False(w.IsCompleted);
            w.Complete();
            Assert.True(w.IsCompleted);

            Assert.Throws<ObjectDisposedException>(() => w.NextRecord());
            await Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await w.NextRecordAsync(TestContext.Current.CancellationToken)
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

        Assert.Equal("te,st\r\n", Impl<char>(writer => writer.WriteRaw("te,st", fieldsWritten: 1)));
        Assert.Equal("te,st\r\n", Impl<byte>(writer => writer.WriteRaw("te,st"u8, fieldsWritten: 1)));
    }

    [Fact]
    public static void Should_Write_Final_Newline()
    {
        Assert.Equal(
            "xyz\r\n",
            Impl<char>(writer =>
            {
                writer.WriteRaw("xyz");
            })
        );

        Assert.Equal(
            "xyz\r\n",
            Impl<byte>(writer =>
            {
                writer.WriteRaw("xyz"u8);
            })
        );

        Assert.Equal(
            "xyz",
            Impl<char>(writer =>
            {
                writer.EnsureTrailingNewline = false;
                writer.WriteRaw("xyz");
            })
        );

        Assert.Equal(
            "xyz",
            Impl<byte>(writer =>
            {
                writer.EnsureTrailingNewline = false;
                writer.WriteRaw("xyz"u8);
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

    private static string Impl<T>(Action<CsvWriter<T>> action)
        where T : unmanaged, IBinaryInteger<T>
    {
        var writer = GetWriter<T>(out Func<string> getString);
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
        return getString();
    }

    private static async Task<string> Impl<T>(Func<CsvWriter<T>, Task> action)
        where T : unmanaged, IBinaryInteger<T>
    {
        var writer = GetWriter<T>(out Func<string> getString);
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

    private static CsvWriter<T> GetWriter<T>(out Func<string> getString)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (typeof(T) == typeof(char))
        {
            var stringWriter = new StringWriter();
            getString = () => stringWriter.ToString();
            return (CsvWriter<T>)(object)CsvWriter.Create(stringWriter);
        }

        if (typeof(T) == typeof(byte))
        {
            var stream = new MemoryStream();
            getString = () => Encoding.UTF8.GetString(stream.ToArray());
            return (CsvWriter<T>)(object)CsvWriter.Create(stream);
        }

        throw Token<T>.NotSupported;
    }
}
