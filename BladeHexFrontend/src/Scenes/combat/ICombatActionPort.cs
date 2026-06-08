// ICombatActionPort.cs
// 战斗场景行动端口接口 — 提供攻击/施法等行动处理入口。
using System.Threading.Tasks;
using BladeHex.Map;

namespace BladeHex.Scenes;

/// <summary>
/// 战斗场景行动端口。提供攻击和施法的处理入口。
/// </summary>
public interface ICombatActionPort
{
	/// <summary>处理攻击动作。</summary>
	Task HandleAttack(HexCell targetCell);

	/// <summary>处理施法动作。</summary>
	Task HandleSpell(HexCell targetCell);
}
