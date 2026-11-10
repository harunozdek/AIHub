namespace AIHub.Domain.Monitoring;

/// <summary>
/// AIHub'ın kendi yaptığı işlemlerin "ekleyeni" (Spec 0008, R-6). Gerçek bir kullanıcı değildir; kimliği hiçbir
/// kullanıcıyla çakışmaz (kullanıcı kimlikleri GUID v7'dir) ve listede <see cref="DisplayName"/> olarak görünür.
/// </summary>
public static class SystemActor
{
    public static readonly Guid Id = Guid.Empty;

    public const string DisplayName = "Sistem";

    /// <summary>AIHub'ın kendi izlenen uygulama kayıtlarının ServiceName'leri (Spec 0008).</summary>
    public const string ApiServiceName = "AIHub.Api";

    public const string PanelServiceName = "AIHub.Panel";
}
