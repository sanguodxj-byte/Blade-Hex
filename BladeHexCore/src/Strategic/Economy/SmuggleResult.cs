// SmuggleResult.cs
// 走私交易结果数据结构
namespace BladeHex.Strategic.Economy;

public record SmuggleResult(
    bool Success,
    int GoldDelta,
    int ItemQtyDelta,
    string FailReason,
    int ReputationPenalty,
    int InfluencePenalty
);
