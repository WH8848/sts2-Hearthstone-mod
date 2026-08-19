using System;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Cards;
using STS2RitsuLib.Scaffolding.Cards.HandOutline;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 条件触发卡标记接口：实现此接口的吉安娜卡，在"本回合抽牌堆中没有随从牌"
/// 时手牌深白描边发光（提示玩家现在打出可触发额外效果）。
/// <b>新增条件触发卡实现此接口即可自动生效</b>（JainaConditionGlow 反射收集，
/// 无需手动维护 switch 分支）。
/// </summary>
public interface IJainaConditionGlowCard
{
    /// <summary>
    /// 升级后是否仍发光（默认 false = 仅未升级形态发光，如匣中古神/埃匹希斯冲击/
    /// 加工失误——升级后条件效果关闭；能量之泉等升级后仍可触发额外效果的卡覆写为 true）。
    /// </summary>
    bool GlowsWhenUpgraded => false;
}

/// <summary>
/// 条件触发卡"条件满足"手牌发光（深白描边）。
/// 像匣中古神/埃匹希斯冲击/加工失误/能量之泉这类需要条件解锁额外效果的卡
/// （实现 <see cref="IJainaConditionGlowCard"/>），
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
        // 【诊断】统计实现接口的条件触发卡数量（新增条件触发卡实现接口即自动注册，
        // 无需手动维护分支——日志出现即可确认注册生效）
        var count = typeof(JainaConditionGlow).Assembly.GetTypes()
            .Count(t => !t.IsAbstract && !t.IsInterface && !t.ContainsGenericParameters &&
                        typeof(IJainaConditionGlowCard).IsAssignableFrom(t));
        MegaCrit.Sts2.Core.Logging.Log.Info($"[JainaDiag] condition-glow cards registered: {count} types");

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
            // 仅条件触发卡（实现 IJainaConditionGlowCard）发光
            if (card is not IJainaConditionGlowCard glowCard)
            {
                return null;
            }
            // 部分卡仅基础版有条件（匣中古神/埃匹希斯冲击/加工失误）：升级后关闭发光
            if (!glowCard.GlowsWhenUpgraded && card.IsUpgraded)
            {
                return null;
            }
            // 条件：抽牌堆中没有随从牌
            bool noMinionInDrawPile = !pcs.DrawPile.Cards.Any(c => c != null && c.Type == JainaCardTypes.Minion);
            return noMinionInDrawPile ? GlowColor : null;
        }
        catch (Exception)
        {
            // 求值异常不发光，不影响手牌
            return null;
        }
    }
}
