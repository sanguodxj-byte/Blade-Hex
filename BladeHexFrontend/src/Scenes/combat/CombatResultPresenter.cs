// CombatResultPresenter.cs
// 从 CombatSceneBase 提取的战斗结果呈现器。
// 负责：结算面板、BGM切换、音效播放、战斗结束处理。
using Godot;
using System;
using System.Linq;
using BladeHex.Combat;
using BladeHex.Strategic;
using BladeHex.UI.Combat;
using BladeHex.Audio;

namespace BladeHex.Scenes;

/// <summary>战斗场景结果呈现器。</summary>
[GlobalClass]
public partial class CombatResultPresenter : Node
{
	// ===== 小 ports 依赖 =====
	private ICombatHighlightPort? _highlight;
	private ICombatFeedbackPort? _feedback;
	private Node? _parentScene;

	private ICombatHighlightPort Highlight => _highlight ?? throw new InvalidOperationException("CombatResultPresenter not initialized.");
	private ICombatFeedbackPort Feedback => _feedback ?? throw new InvalidOperationException("CombatResultPresenter not initialized.");
	public Node ParentScene => _parentScene ?? throw new InvalidOperationException("CombatResultPresenter not initialized.");
	public AudioManager? AudioManager { get; set; }

	/// <summary>注入必要依赖。</summary>
	public void Initialize(ICombatHighlightPort highlight, ICombatFeedbackPort feedback, Node parentScene, AudioManager? audioManager = null)
	{
		_highlight = highlight ?? throw new ArgumentNullException(nameof(highlight));
		_feedback = feedback ?? throw new ArgumentNullException(nameof(feedback));
		_parentScene = parentScene ?? throw new ArgumentNullException(nameof(parentScene));
		AudioManager = audioManager;
	}

	// 战斗结束状态防重
	private bool _combatEnded;

	/// <summary>战斗结束处理</summary>
	public async void OnCombatEnded(bool victory, BattleOutcome? outcome, Action<bool>? finishCallback = null)
	{
		if (_combatEnded) return;
		_combatEnded = true;

		try
		{
			Highlight.ClearHighlights();
			Feedback.SetTurnText(victory ? "战斗胜利！" : "战斗失败！", victory ? Colors.Green : Colors.Red);
			Feedback.SetActionBarVisible(false);

			// 播放胜利/失败音效与音乐
			if (AudioManager != null)
			{
				AudioManager.PlaySfxName(victory ? "combat_victory" : "combat_defeat");
				AudioManager.PlayScenarioBgm(
					victory ? AudioManager.Scenario.Victory : AudioManager.Scenario.Defeat,
					"default", 2.0f);
			}

			// 等待 1 秒
			await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);

			// 从 outcome 读取奖励数据，无 outcome 时显示 0
			int xp = outcome?.XpGranted ?? 0;
			int gold = outcome?.GoldGranted ?? 0;
			var lootNames = outcome?.LootEntries?.Select(l => l.ItemName).ToArray()
				?? Array.Empty<string>();

			var resultPanel = new BattleResultPanel();
			ParentScene.AddChild(resultPanel);
			resultPanel.Show(victory, xp, gold, lootNames);
			resultPanel.ContinueClicked += () =>
			{
				finishCallback?.Invoke(victory);
			};
		}
		catch (Exception ex)
		{
			GD.PushError($"[CombatResultPresenter] OnCombatEnded: {ex.Message}");
		}
	}
}
