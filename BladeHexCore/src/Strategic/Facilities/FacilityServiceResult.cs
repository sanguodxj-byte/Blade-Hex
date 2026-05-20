// FacilityServiceResult.cs
// 城镇设施服务的统一结果对象。
namespace BladeHex.Strategic.Facilities;

/// <summary>
/// 设施服务执行结果。Core 层只返回结构化结果，不直接操作 UI。
/// </summary>
public sealed class FacilityServiceResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public int GoldSpent { get; init; }
    public int AffectedMembers { get; init; }
    public int AffectedItems { get; init; }
    public int AmountChanged { get; init; }

    public static FacilityServiceResult Ok(
        string message,
        int goldSpent = 0,
        int affectedMembers = 0,
        int affectedItems = 0,
        int amountChanged = 0) => new()
        {
            Success = true,
            Message = message,
            GoldSpent = goldSpent,
            AffectedMembers = affectedMembers,
            AffectedItems = affectedItems,
            AmountChanged = amountChanged,
        };

    public static FacilityServiceResult Fail(string message) => new()
    {
        Success = false,
        Message = message,
    };
}
