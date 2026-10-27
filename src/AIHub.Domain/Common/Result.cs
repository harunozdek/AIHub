namespace AIHub.Domain.Common;

/// <summary>Beklenen bir hata. <paramref name="Field"/> doğrulama hatalarında hangi alanın reddedildiğini taşır.</summary>
public sealed record Error(string Code, string Message, string? Field = null);

/// <summary>Beklenen hatalar için kontrol akışı (docs/conventions.md, Error handling) — exception değil.</summary>
public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T value)
    {
        _value = value;
        Errors = [];
    }

    private Result(IReadOnlyList<Error> errors)
    {
        if (errors.Count == 0)
        {
            throw new ArgumentException("Başarısız sonuç en az bir hata taşımalıdır.", nameof(errors));
        }

        Errors = errors;
    }

    public bool IsSuccess => Errors.Count == 0;

    public IReadOnlyList<Error> Errors { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Başarısız bir sonucun değeri okunamaz.");

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(Error error) => new([error]);

    public static Result<T> Failure(IReadOnlyList<Error> errors) => new(errors);
}
