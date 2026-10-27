namespace AIHub.Infrastructure.Queue;

public sealed class IngestionOptions
{
    public const string SectionName = "Ingestion";

    public string StreamKey { get; set; } = "aihub:logs";

    public string DeadLetterStreamKey { get; set; } = "aihub:logs:dead";

    public string ConsumerGroup { get; set; } = "aihub-persist";

    /// <summary>Yeniden başlatmada kendi bekleyen kayıtlarını bulabilmesi için sabit olmalıdır.</summary>
    public string ConsumerName { get; set; } = Environment.MachineName;

    public bool ConsumerEnabled { get; set; } = true;

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(200);

    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Başka (artık yaşamayan) bir tüketicide bu süreden uzun bekleyen kayıtlar devralınır.</summary>
    public TimeSpan ClaimIdleAfter { get; set; } = TimeSpan.FromMinutes(1);

    public int BatchSize { get; set; } = 50;
}
