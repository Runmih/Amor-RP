namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class GroupRow
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public Guid OwnerCharacterId { get; set; }
    public int Version { get; set; } = 1;
    public bool Deleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid CurrencyId { get; set; } = Guid.CreateVersion7();
    public string CurrencyName { get; set; } = "";
    public string CurrencySymbol { get; set; } = "";
    public int CurrencyVersion { get; set; } = 1;
    public int PolicyVersion { get; set; } = 1;
    public int CurrentPotionPoints { get; set; }
    public int CurrentLetters { get; set; }
    public int NextPotionPoints { get; set; }
    public int NextLetters { get; set; }
    public DateTimeOffset PolicyPeriod { get; set; }
}
