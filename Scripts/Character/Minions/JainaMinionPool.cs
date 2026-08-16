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
    /// <param name="source">召唤来源卡（手牌打出随从卡时传随从卡实例，触发战吼；其余不传）</param>
    public static async Task<Creature> SummonMinion<T>(
        PlayerChoiceContext choiceContext,
        Player player,
        decimal? maxHp = null,
        decimal? attack = null,
        MinionPosition position = MinionPosition.FrontUpper,
        MegaCrit.Sts2.Core.Models.CardModel? source = null) where T : JainaMinionBase
    {
        // 随从上限：最多 7 个，超过则不召唤
        if (GetCurrentMinionCount(player) >= MaxMinions)
        {
            return null!;
        }
        return await MinionCmd.AddMinion<T>(choiceContext, player, new MinionSummonOptions(
            MaxHp: maxHp,
            PrimaryStatAmount: attack,
            Source: source,
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
        MinionPosition position = MinionPosition.FrontUpper,
        MegaCrit.Sts2.Core.Models.CardModel? source = null)
    {
        if (minionType == null || !typeof(JainaMinionBase).IsAssignableFrom(minionType))
        {
            return null!;
        }
        return minionType.Name switch
        {
            nameof(Zealot) => await SummonMinion<Zealot>(choiceContext, player, maxHp, attack, position, source),
            nameof(VolatileSkeleton) => await SummonMinion<VolatileSkeleton>(choiceContext, player, maxHp, attack, position, source),
            nameof(ImpMinion) => await SummonMinion<ImpMinion>(choiceContext, player, maxHp, attack, position, source),
            nameof(SpiritCollectorMinion) => await SummonMinion<SpiritCollectorMinion>(choiceContext, player, maxHp, attack, position, source),
            nameof(RenathalMinion) => await SummonMinion<RenathalMinion>(choiceContext, player, maxHp, attack, position, source),
            nameof(SorcererApprenticeMinion) => await SummonMinion<SorcererApprenticeMinion>(choiceContext, player, maxHp, attack, position, source),
            nameof(ArcaneArtificerMinion) => await SummonMinion<ArcaneArtificerMinion>(choiceContext, player, maxHp, attack, position, source),
            nameof(AntonidasMinion) => await SummonMinion<AntonidasMinion>(choiceContext, player, maxHp, attack, position, source),
            nameof(VardenMinion) => await SummonMinion<VardenMinion>(choiceContext, player, maxHp, attack, position, source),
            nameof(RommathMinion) => await SummonMinion<RommathMinion>(choiceContext, player, maxHp, attack, position, source),
            nameof(MozakiMinion) => await SummonMinion<MozakiMinion>(choiceContext, player, maxHp, attack, position, source),
            nameof(LunaMinion) => await SummonMinion<LunaMinion>(choiceContext, player, maxHp, attack, position, source),
            nameof(KalecgosMinion) => await SummonMinion<KalecgosMinion>(choiceContext, player, maxHp, attack, position, source),
            nameof(AegwynnMinion) => await SummonMinion<AegwynnMinion>(choiceContext, player, maxHp, attack, position, source),
            nameof(DawngraspMinion) => await SummonMinion<DawngraspMinion>(choiceContext, player, maxHp, attack, position, source),
            nameof(WaterElementalMinion) => await SummonMinion<WaterElementalMinion>(choiceContext, player, maxHp, attack, position, source),
            nameof(GreySageParrotMinion) => await SummonMinion<GreySageParrotMinion>(choiceContext, player, maxHp, attack, position, source),
            nameof(FlamewakerMinion) => await SummonMinion<FlamewakerMinion>(choiceContext, player, maxHp, attack, position, source),
            nameof(SanctumCandlesellerMinion) => await SummonMinion<SanctumCandlesellerMinion>(choiceContext, player, maxHp, attack, position, source),
            nameof(EnergyShaperMinion) => await SummonMinion<EnergyShaperMinion>(choiceContext, player, maxHp, attack, position, source),
            nameof(KhadgarMinion) => await SummonMinion<KhadgarMinion>(choiceContext, player, maxHp, attack, position, source),
            nameof(CommanderSivaraMinion) => await SummonMinion<CommanderSivaraMinion>(choiceContext, player, maxHp, attack, position, source),
            nameof(MordreshFireEyeMinion) => await SummonMinion<MordreshFireEyeMinion>(choiceContext, player, maxHp, attack, position, source),
            nameof(AstromancerSolarianMinion) => await SummonMinion<AstromancerSolarianMinion>(choiceContext, player, maxHp, attack, position, source),
            nameof(SolarianPrimeMinion) => await SummonMinion<SolarianPrimeMinion>(choiceContext, player, maxHp, attack, position, source),
            nameof(ImpWranglerMinion) => await SummonMinion<ImpWranglerMinion>(choiceContext, player, maxHp, attack, position, source),
            nameof(RecklessApprenticeMinion) => await SummonMinion<RecklessApprenticeMinion>(choiceContext, player, maxHp, attack, position, source),
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

    /// <summary>
    /// 随机召唤一个指定费用消耗的随从（如埃匹希斯冲击/火焰之地传送门）。
    /// 按随从卡牌模型的费用筛选，属性取卡面标准值；无匹配时返回 null。
    /// </summary>
    public static async Task<Creature> SummonRandomMinionOfCost(
        PlayerChoiceContext choiceContext,
        Player player,
        int cost,
        MinionPosition position = MinionPosition.FrontUpper)
    {
        // 收集费用匹配的随从类型
        var candidates = new List<Type>();
        foreach (var minionType in JainaMinionCardMap.MinionTypes)
        {
            var cardType = JainaMinionCardMap.GetCardType(minionType);
            if (cardType == null)
            {
                continue;
            }
            var cardModel = ModelDb.GetByIdOrNull<MegaCrit.Sts2.Core.Models.CardModel>(ModelDb.GetId(cardType));
            if (cardModel != null && cardModel.EnergyCost.Canonical == cost)
            {
                candidates.Add(minionType);
            }
        }
        if (candidates.Count == 0)
        {
            return null!;
        }

        var combatState = player.Creature.CombatState;
        var chosen = combatState.RunState.Rng.CombatTargets.NextItem(candidates) ?? candidates[0];

        // 属性取对应随从卡的标准值
        var chosenCard = ModelDb.GetByIdOrNull<MegaCrit.Sts2.Core.Models.CardModel>(ModelDb.GetId(JainaMinionCardMap.GetCardType(chosen)))
            as jaina.Scripts.Character.Cards.JainaMinionCardTemplate;
        return await SummonMinionByType(
            choiceContext, player, chosen,
            maxHp: chosenCard?.StandardMinionHealth,
            attack: chosenCard?.StandardMinionAttack,
            position: position);
    }
}
