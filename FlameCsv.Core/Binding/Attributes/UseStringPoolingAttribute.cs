using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Parsers;
using FlameCsv.Parsers.Text;

namespace FlameCsv.Binding.Attributes;

public sealed class UseStringPoolingAttribute : CsvParserOverrideAttribute
{
    public override ICsvParser<T> CreateParser<T>(Type targetType, CsvReaderOptions<T> options)
    {
        if (targetType != typeof(string))
        {
            throw new InvalidOperationException("This attribute can only be used on string properties.");
        }

        object parser;

        if (typeof(T) == typeof(char))
        {
            if (options is CsvTextReaderOptions { StringPool: { } pool } &&
                pool != StringPool.Shared)
            {
                parser = new PoolingStringTextParser(pool);
            }
            else
            {
                parser = PoolingStringTextParser.Instance;
            }
        }
        //else if (typeof(T) == typeof(byte))
        //{
        //    if (options is CsvUtf8ReaderOptions { StringPool: { } pool } &&
        //        pool != StringPool.Shared)
        //    {
        //        parser = new PoolingStringTextParser(pool);
        //    }
        //    else
        //    {
        //        parser = PoolingStringUtf8Parser.Instance;
        //    }
        //}
        else
        {
            Token<T>.ThrowNotSupportedException();
            return default!;
        }

        return (ICsvParser<T>)parser;
    }
}
