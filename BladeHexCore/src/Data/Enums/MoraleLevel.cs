// MoraleLevel.cs
// 士气等级枚举

namespace BladeHex.Data;

public enum MoraleLevel
{
    High,    // 高昂 (+20~+40)
    Normal,  // 正常 (-19~+19)
    Low,     // 低落 (-39~-20)
    Broken,  // 崩溃 (-59~-40)
    Routing, // 溃逃 (-60)
}