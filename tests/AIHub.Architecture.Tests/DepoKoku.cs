namespace AIHub.Architecture.Tests;

/// <summary>
/// Depo kökünü test çıktısının bulunduğu dizinden yukarı yürüyerek bulur. Sabit bir göreli yol
/// ("../../../..") kullanmak, çıktı yolu değiştiğinde (farklı TFM, farklı konfigürasyon) sessizce
/// kırılır; `AIHub.sln` işaretçisi bu kırılganlığı ortadan kaldırır.
/// </summary>
internal static class DepoKoku
{
    private const string Isaretci = "AIHub.sln";

    public static string Bul()
    {
        var dizin = new DirectoryInfo(AppContext.BaseDirectory);

        while (dizin is not null)
        {
            if (File.Exists(Path.Combine(dizin.FullName, Isaretci)))
            {
                return dizin.FullName;
            }

            dizin = dizin.Parent;
        }

        throw new InvalidOperationException(
            $"Depo kökü bulunamadı: '{Isaretci}' dosyası {AppContext.BaseDirectory} " +
            "dizininden yukarı doğru hiçbir üst dizinde yok.");
    }
}
