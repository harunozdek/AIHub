namespace AIHub.Domain.Masking;

/// <summary>Maskelenen hassas veri türleri (BR-3). Yer tutucu metni <see cref="MaskKinds.Placeholder"/> üretir.</summary>
public enum MaskKind
{
    Email,
    Tckn,
    BearerToken,
    Password,
    ApiKey,
}

public static class MaskKinds
{
    public static string Placeholder(MaskKind kind) => kind switch
    {
        MaskKind.Email => "[MASKED:email]",
        MaskKind.Tckn => "[MASKED:tckn]",
        MaskKind.BearerToken => "[MASKED:bearer-token]",
        MaskKind.Password => "[MASKED:password]",
        MaskKind.ApiKey => "[MASKED:api-key]",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
