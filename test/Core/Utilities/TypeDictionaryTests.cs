using FlameCsv.Utilities;

namespace FlameCsv.Tests.Utilities;

public class TypeDictionaryTests
{
    [Theory, InlineData(typeof(bool)), InlineData(typeof(bool?))]
    public void Should_Equate_Nullable_And_Underlying(Type type)
    {
        var options = new CsvOptions<char>();
        var dict = new TypeDictionary<int>(options);

        Assert.DoesNotContain(typeof(bool), dict);
        Assert.DoesNotContain(typeof(bool?), dict);

        dict.Add(type, 5);

        Assert.Equal(5, dict[typeof(bool)]);
        Assert.Equal(5, dict[typeof(bool?)]);
    }
}
