namespace AIHub.Domain.Monitoring;

/// <summary>Başarısız denetimin nedeni (Spec 0005, R-6). Yanıt gövdesi ya da iç ayrıntı taşımaz.</summary>
public enum HealthFailureReason
{
    Timeout,
    ConnectionFailed,
    CertificateError,
    HttpStatus,
    Redirect,
    BlockedAddress,
}
