namespace AIHub.Application.Logs;

/// <summary>Sayfanın hangi yöne açılacağı: <see cref="Older"/> sonraki sayfa, <see cref="Newer"/> önceki sayfa.</summary>
public enum LogPageDirection
{
    Older,
    Newer,
}

/// <summary>
/// Keyset sayfalama işareti: sayfa, bu kaydın (alınma zamanı, kimlik) çiftinden itibaren açılır.
/// Sıra (ReceivedAt, Id) üzerindedir; Id GUID v7 olduğu için aynı andaki kayıtlar da kararlı sıralanır.
/// Yeni gelen kayıtlar listenin başına eklenir ve sonraki sayfaları kaydırmaz (Spec 0002, R-4).
/// <see cref="From"/> ve <see cref="To"/> ilk sayfada çözülen pencereyi taşır: kullanıcı aralık seçmediyse
/// "son 24 saat" sayfalar arasında kaymaz ve adrese kullanıcının seçmediği bir aralık yazılmaz (review 2, R2-02).
/// </summary>
public sealed record LogCursor(DateTimeOffset ReceivedAt, Guid Id, LogPageDirection Direction, DateTimeOffset From, DateTimeOffset To);
