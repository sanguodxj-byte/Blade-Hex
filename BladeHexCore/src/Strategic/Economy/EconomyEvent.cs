// EconomyEvent.cs
// 市场价格波动事件数据结构
namespace BladeHex.Strategic.Economy;

public enum EconomyEventType 
{ 
    War, 
    Siege, 
    LordCapturedInflation 
}

public record EconomyEvent(
    EconomyEventType Type,
    string TargetPoiName,
    string FactionId,
    string ItemCategory, // "weapon", "food", "horse", "all"
    float Multiplier,
    int ExpiresDay
);
