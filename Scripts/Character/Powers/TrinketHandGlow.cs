using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Scaffolding.Cards.HandOutline;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 小玩物小屋"抽到的牌"手牌发光。
/// 使用小玩物小屋抽上来的牌（TrinketTrackerPower.DrawnCard）在手牌中
/// 以<b>亮蓝色</b>描边发光，提示"本回合打出这张牌可重新开启小屋"；
/// 与"牌库外衍生卡"的<b>金色</b>发光（RegisterCardHandGlow 的 Gold 通道）颜色区分。
/// 规则注册到 CardModel 基类（EvaluateBest 沿基类链向上匹配）→ 抽到的任意卡
/// （含中立/无色卡）都生效；RefreshEveryFrame 每帧评估，抽出即亮、打出/回合结束即灭。
/// 联机：发光为纯本地 UI 判断，两端各自评估 DrawnCard（命令确定性同步），结果一致。
/// </summary>
public static class TrinketHandGlow
{
    /// <summary>
    /// 小玩物小屋抽到的牌发光颜色（深蓝色，与衍生卡金色区分）
    /// </summary>
    public static readonly Color GlowColor = new Color(0.1f, 0.25f, 0.85f);

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
    /// 该卡是否小玩物小屋本回合抽到的牌：是则返回发光颜色，否则 null（不发光）。
    /// </summary>
    private static Color? ResolveColor(CardModel card)
    {
        try
        {
            if (card?.Owner?.PlayerCombatState is not { } pcs)
            {
                return null;
            }
            foreach (Creature pet in pcs.Pets)
            {
                if (pet?.Monster is TrinketShopLandmark && pet.GetPower<TrinketTrackerPower>()?.DrawnCard == card)
                {
                    return GlowColor;
                }
            }
        }
        catch (Exception)
        {
            // 求值异常不发光，不影响手牌
        }
        return null;
    }
}
