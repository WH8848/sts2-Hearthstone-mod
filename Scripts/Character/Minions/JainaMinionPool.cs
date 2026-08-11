using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MinionLib.Commands;
using MinionLib.Minion;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 吉安娜的随从池 - 随从生物类型列表与召唤入口。
/// 随从按打出顺序标记 OrderIndex，用于挡伤判定（见 MinionSquadPower）。
/// </summary>
public static class JainaMinionPool
{
    /// <summary>
    /// 随从池中的随从生物类型列表
    /// </summary>
    private static readonly Type[] _minionTypes =
    [
        typeof(Zealot),
        typeof(VolatileSkeleton)
    ];

    /// <summary>
    /// 随从池中所有随从生物类型的只读列表
    /// </summary>
    public static IReadOnlyList<Type> MinionTypes => _minionTypes;

    /// <summary>
    /// 吉安娜最多可同时拥有的随从数量（对应 7 个充能球位）
    /// </summary>
    public const int MaxMinions = 7;

    /// <summary>
    /// 当前玩家已召唤的存活随从数量
    /// </summary>
    public static int GetCurrentMinionCount(Player player)
    {
        if (player?.PlayerCombatState?.Pets == null)
        {
            return 0;
        }
        int count = 0;
        foreach (var pet in player.PlayerCombatState.Pets)
        {
            if (pet != null && pet.IsAlive && pet.Monster is JainaMinionBase)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// 从随从池中随机选择一个随从生物类型
    /// </summary>
    public static Type GetRandomMinionType(Player player)
    {
        return player.RunState.Rng.CombatTargets.NextItem(_minionTypes) ?? _minionTypes[0];
    }

    /// <summary>
    /// 召唤指定类型的随从生物
    /// </summary>
    /// <param name="maxHp">随从生命值（null 使用模型默认值）</param>
    /// <param name="attack">随从攻击力（null 不设置）</param>
    public static async Task<Creature> SummonMinion<T>(
        PlayerChoiceContext choiceContext,
        Player player,
        decimal? maxHp = null,
        decimal? attack = null,
        MinionPosition position = MinionPosition.FrontUpper) where T : JainaMinionBase
    {
        // 随从上限：最多 7 个，超过则不召唤
        if (GetCurrentMinionCount(player) >= MaxMinions)
        {
            return null!;
        }
        return await MinionCmd.AddMinion<T>(choiceContext, player, new MinionSummonOptions(
            MaxHp: maxHp,
            PrimaryStatAmount: attack,
            Source: null,
            Position: position));
    }

    /// <summary>
    /// 按运行时类型召唤指定随从生物（供随从牌等通过 Type 引用调用）。
    /// </summary>
    public static async Task<Creature> SummonMinionByType(
        PlayerChoiceContext choiceContext,
        Player player,
        Type minionType,
        decimal? maxHp = null,
        decimal? attack = null,
        MinionPosition position = MinionPosition.FrontUpper)
    {
        if (minionType == null || !typeof(JainaMinionBase).IsAssignableFrom(minionType))
        {
            return null!;
        }
        return minionType.Name switch
        {
            nameof(Zealot) => await SummonMinion<Zealot>(choiceContext, player, maxHp, attack, position),
            nameof(VolatileSkeleton) => await SummonMinion<VolatileSkeleton>(choiceContext, player, maxHp, attack, position),
            _ => null!,
        };
    }

    /// <summary>
    /// 从随从池中随机召唤一个随从生物
    /// </summary>
    public static async Task<Creature> SummonRandomMinion(
        PlayerChoiceContext choiceContext,
        Player player,
        MinionPosition position = MinionPosition.FrontUpper)
    {
        Type type = GetRandomMinionType(player);
        return await SummonMinionByType(choiceContext, player, type, position: position);
    }
}
