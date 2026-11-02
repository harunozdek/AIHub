namespace AIHub.Application.Abstractions;

/// <summary>Şifre yalnızca yavaş ve tuzlu bir hash olarak saklanır (Spec 0004, R-4).</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>Sabit zamanlı karşılaştırır. Tanınmayan biçimdeki hash için <c>false</c>.</summary>
    bool Verify(string password, string passwordHash);
}
