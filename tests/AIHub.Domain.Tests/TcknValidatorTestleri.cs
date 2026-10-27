using AIHub.Domain.Masking;

namespace AIHub.Domain.Tests;

public class TcknValidatorTestleri
{
    [Theory]
    [InlineData("10000000146")]
    [InlineData("11111111110")]
    public void Kontrol_haneleri_tutan_numara_gecerlidir(string numara)
    {
        Assert.True(TcknValidator.IsValid(numara));
    }

    [Theory]
    [InlineData("12345678901")] // 10. hane tutmuyor
    [InlineData("10000000147")] // 11. hane tutmuyor
    [InlineData("01234567890")] // sıfırla başlıyor
    [InlineData("1000000014")]  // 10 hane
    [InlineData("1000000014a")]
    public void Kurala_uymayan_numara_gecersizdir(string numara)
    {
        Assert.False(TcknValidator.IsValid(numara));
    }
}
