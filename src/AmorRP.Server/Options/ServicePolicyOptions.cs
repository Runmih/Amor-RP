using System.ComponentModel.DataAnnotations;
using AmorRP.Contracts.Common;
using AmorRP.Core.Groups;

namespace AmorRP.Server.Options;

public sealed class ServicePolicyOptions : IValidatableObject
{
    [Range(1, 100)] public int OwnedGroups { get; set; } = 3;
    [Range(1, 100)] public int JoinedGroups { get; set; } = 6;
    [Range(0, 10000)] public int DefaultLettersPerWeek { get; set; } = 5;
    [Range(0, 1000000)] public int DefaultPotionPoints { get; set; }
    [Range(1, 500)] public int ChatMessageTextElements { get; set; } = 50;
    [Range(1, 1000)] public int LetterTitleTextElements { get; set; } = 80;
    [Range(1, 100000)] public int LetterBodyTextElements { get; set; } = 5000;
    [Range(1, 1000)] public int MaxPageSize { get; set; } = 100;
    [Range(1, 1000000)] public int MaxHoldingsPerCharacterGroup { get; set; } = 10000;
    [Range(1, 1000)] public int MaxTradeLinesPerSide { get; set; } = 20;
    [Range(1, 10000)] public int MaxCreationQuantity { get; set; } = 100;
    [Range(1, 86400)] public int TradeLifetimeSeconds { get; set; } = 600;
    [Range(1024, 1048576)] public int MaxRequestBytes { get; set; } = 65536;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (JoinedGroups < OwnedGroups)
            yield return new ValidationResult("JoinedGroups must include owned groups.", [nameof(JoinedGroups)]);
    }

    public PublicLimits ToContract()
    {
        var groups = new GroupCapacityPolicy(OwnedGroups, JoinedGroups);
        return new PublicLimits(groups.OwnedGroups, groups.JoinedGroups,
            DefaultLettersPerWeek, DefaultPotionPoints, ChatMessageTextElements,
            LetterTitleTextElements, LetterBodyTextElements, MaxPageSize,
            MaxHoldingsPerCharacterGroup, MaxTradeLinesPerSide, MaxCreationQuantity,
            TradeLifetimeSeconds, "Monday", "00:00:00", MaxRequestBytes);
    }
}
