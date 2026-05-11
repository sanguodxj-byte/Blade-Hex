// CharacterGeneratorProxy.cs
// 场景节点包装 — 挂载到场景树供 GDScript 调用
// 内部委托给静态工具类 CharacterGenerator
using Godot;

namespace BladeHex.Data;

/// <summary>
/// 角色生成器节点 — 挂载到场景树供 GDScript 通过 @onready 引用
/// </summary>
[GlobalClass]
public partial class CharacterGeneratorProxy : Node
{
    public UnitData GenerateCharacter(RaceData? race = null, int level = 1, long seedVal = -1)
    {
        return CharacterGenerator.GenerateCharacter(race, level, seedVal);
    }
}
