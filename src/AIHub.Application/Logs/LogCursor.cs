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
/// </summary>
public sealed record LogCursor(DateTimeOffset ReceivedAt, Guid Id, LogPageDirection Direction);
