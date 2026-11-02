using AIHub.Domain.Common;

namespace AIHub.Application.Users;

public static class UserErrors
{
    /// <summary>
    /// Yanlış şifre, var olmayan kullanıcı, pasif ve kilitli hesap için TEK hata: kullanıcı adının
    /// varlığı sızmaz (Spec 0004, R-2).
    /// </summary>
    public static readonly Error InvalidCredentials = new("auth.invalidCredentials", "Kullanıcı adı veya şifre hatalı.");

    public static readonly Error UserNameTaken = new("userName.taken", "Bu kullanıcı adı zaten kullanılıyor.", "userName");

    public static readonly Error NotFound = new("user.notFound", "Kullanıcı bulunamadı.");

    public static readonly Error CannotDeactivateSelf = new("user.cannotDeactivateSelf", "Kendi hesabınızı pasifleştiremezsiniz.");

    public static readonly Error CannotDeactivateLastActive = new("user.cannotDeactivateLastActive", "Son aktif kullanıcı pasifleştirilemez.");

    public static readonly Error CurrentPasswordInvalid = new("currentPassword.invalid", "Mevcut şifre hatalı.", "currentPassword");
}
