using AIHub.Domain.Common;

namespace AIHub.Api.Auth;

/// <summary>
/// İki yüzey, iki ayrı kimlik (docs/security.md): servis API anahtarı yalnızca ingestion'a, panel
/// kullanıcısının OIDC token'ı yalnızca panel okuma yüzeyine geçer. Biri ötekinin yüzeyinde 401 alır
/// (Spec 0002, R-1).
/// </summary>
internal static class AuthSchemes
{
    public const string Panel = "Panel";

    public const string IngestionPolicy = "Ingestion";
    public const string PanelPolicy = "Panel";

    public static readonly Error PanelUnauthenticated = new("panel.unauthenticated", "Bu işlem için oturum açmanız gerekiyor.");
}
