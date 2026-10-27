namespace AIHub.Domain.Masking;

/// <summary>
/// T.C. kimlik numarası kontrol hanesi doğrulaması. Yalnızca geçerli numaralar maskelenir; böylece
/// rastgele 11 haneli sayılar (sipariş no vb.) korunur (Spec 0001, Constraints).
/// </summary>
public static class TcknValidator
{
    public static bool IsValid(ReadOnlySpan<char> text)
    {
        if (text.Length != 11 || text[0] == '0')
        {
            return false;
        }

        Span<int> d = stackalloc int[11];
        for (var i = 0; i < 11; i++)
        {
            if (!char.IsAsciiDigit(text[i]))
            {
                return false;
            }

            d[i] = text[i] - '0';
        }

        var oddSum = d[0] + d[2] + d[4] + d[6] + d[8];
        var evenSum = d[1] + d[3] + d[5] + d[7];
        var tenth = (((oddSum * 7) - evenSum) % 10 + 10) % 10;
        if (tenth != d[9])
        {
            return false;
        }

        var firstTenSum = 0;
        for (var i = 0; i < 10; i++)
        {
            firstTenSum += d[i];
        }

        return firstTenSum % 10 == d[10];
    }
}
