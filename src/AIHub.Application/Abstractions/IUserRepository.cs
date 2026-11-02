using AIHub.Domain.Users;

namespace AIHub.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> FindByNormalizedNameAsync(string normalizedUserName, CancellationToken cancellationToken);

    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> AnyAsync(CancellationToken cancellationToken);

    Task<int> CountActiveAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Kullanıcıyı ekler. Aynı normalize ad o arada eklenmişse <c>false</c> döner.</summary>
    Task<bool> AddAsync(User user, CancellationToken cancellationToken);

    /// <summary>Bu depodan okunan kullanıcılardaki değişiklikleri kalıcılaştırır.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Kullanıcı yönetimindeki yazmaları birbirine karşı sıraya sokar: "son aktif kullanıcı" gibi
    /// birden çok satıra bakan kurallar yarışta bozulmaz (Spec 0004, R-8).
    /// </summary>
    Task<T> RunSerializedAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken);
}
