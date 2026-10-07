using System.ComponentModel.DataAnnotations;
namespace AmorRP.Server.Options;

public sealed class SessionPolicyOptions
{
    [Range(5, 60)] public int AccessMinutes { get; set; } = 30;
    [Range(1, 90)] public int RefreshDays { get; set; } = 30;
    [Range(2, 20)] public int LoginMinutes { get; set; } = 10;
    [Range(1, 100)] public int MaxSessionsPerCharacter { get; set; } = 20;
    [Range(1, 1024)] public int MaxPendingLogins { get; set; } = 256;
}
