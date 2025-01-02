using FlameCsv.Utilities;

namespace FlameCsv.Tests.Utilities;

public class TypeDictionaryTests
{
    [Theory, InlineData(typeof(bool)), InlineData(typeof(bool?))]
    public void Should_Equate_Nullable_And_Underlying(Type type)
    {
        var options = new CsvOptions<char>();
        var dict = new TypeDictionary<int, string>(options, i => i.ToString());

        Assert.DoesNotContain(typeof(bool), dict);
        Assert.DoesNotContain(typeof(bool?), dict);

        dict.Add(type, 5);

        Assert.Equal(5, dict[typeof(bool)]);
        Assert.Equal(5, dict[typeof(bool?)]);

        Assert.True(dict.TryGetAlternate(typeof(bool), out string? alternate));
        Assert.Equal("5", alternate);
        Assert.True(dict.TryGetAlternate(typeof(bool?), out alternate));
        Assert.Equal("5", alternate);
    }
}
