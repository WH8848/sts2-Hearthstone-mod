using System;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Cards;
using STS2RitsuLib.Scaffolding.Cards.HandOutline;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 条件触发卡"条件满足"手牌发光（深白描边）。
/// 像匣中古神/埃匹希斯冲击/不公平游戏/能量之泉这类需要条件解锁额外效果的卡，
/// 当触发条件满足（本回合抽牌堆中没有随从牌）时，手牌中的卡以<b>深白</b>描边发光，
/// 提示玩家现在打出可触发额外效果。
/// 规则注册到 CardModel 基类（EvaluateBest 沿基类链向上匹配 → 任意卡生效），
/// RefreshEveryFrame 每帧评估条件——抽牌堆随从情况变化即亮/灭。
/// 联机：条件评估为纯本地 UI 判断，两端各自评估（DrawPile 确定性同步），结果一致。
/// </summary>
public static class JainaConditionGlow
{
    /// <summary>
    /// 条件满足时的发光颜色（深白：偏冷色的白，区别于衍生卡金色/小玩物小屋深蓝）
    /// </summary>
    public static readonly Color GlowColor = new Color(0.85f, 0.9f, 1f);

    /// <summary>
    /// 必须在内容注册冻结前调用（ModCardHandOutlineRegistry 冻结后禁止注册）。
    /// </summary>
    public static void Register()
    {
        ModCardHandOutlineRegistry.Register(
            typeof(CardModel),
            ModCardHandOutlineRules.Switch(ResolveColor, priority: 100, refreshEveryFrame: true));
    }

    /// <summary>
    /// 条件满足则返回发光颜色，否则 null（不发光）。
    /// </summary>
    private static Color? ResolveColor(CardModel card)
    {
        try
        {
            if (card?.Owner?.PlayerCombatState is not { } pcs)
            {
                return null; // 非战斗/无主（图鉴/商店预览等）：不发光
            }
            // 条件：抽牌堆中没有随从牌（部分卡仅基础版有条件）
            bool noMinionInDrawPile = !pcs.DrawPile.Cards.Any(c => c != null && c.Type == JainaCardTypes.Minion);
            bool conditionMet = card switch
            {
                YoggBoxCard yogg => !yogg.IsUpgraded && noMinionInDrawPile,
                ApexisBlast a => !a.IsUpgraded && noMinionInDrawPile,
                UnfairGame u => !u.IsUpgraded && noMinionInDrawPile,
                FontOfPowerCard => noMinionInDrawPile,
                _ => false,
            };
            return conditionMet ? GlowColor : null;
        }
        catch (Exception)
        {
            // 求值异常不发光，不影响手牌
            return null;
        }
    }
}
