using System;
using System.Collections.Generic;
using jaina.Scripts.Character.Cards;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 随从生物类型 → 随从卡牌类型的映射（悬停显示卡牌时获取本地化文本用）。
/// </summary>
public static class JainaMinionCardMap
{
    private static readonly Dictionary<Type, Type> CardByMinionType = new()
    {
        [typeof(Zealot)] = typeof(ZealotCard),
        [typeof(VolatileSkeleton)] = typeof(VolatileSkeletonCard),
        [typeof(ImpMinion)] = typeof(ImpCard),
        [typeof(SpiritCollectorMinion)] = typeof(SpiritCollectorCard),
        [typeof(RenathalMinion)] = typeof(RenathalCard),
        [typeof(SorcererApprenticeMinion)] = typeof(SorcererApprenticeCard),
        [typeof(ArcaneArtificerMinion)] = typeof(ArcaneArtificerCard),
        [typeof(AntonidasMinion)] = typeof(AntonidasCard),
        [typeof(VardenMinion)] = typeof(VardenCard),
        [typeof(RommathMinion)] = typeof(RommathCard),
        [typeof(MozakiMinion)] = typeof(MozakiCard),
        [typeof(LunaMinion)] = typeof(LunaCard),
        [typeof(KalecgosMinion)] = typeof(KalecgosCard),
        [typeof(AegwynnMinion)] = typeof(AegwynnCard),
        [typeof(DawngraspMinion)] = typeof(DawngraspCard),
        [typeof(WaterElementalMinion)] = typeof(WaterElementalCard),
        [typeof(GreySageParrotMinion)] = typeof(GreySageParrotCard),
        [typeof(FlamewakerMinion)] = typeof(FlamewakerCard),
        [typeof(SanctumCandlesellerMinion)] = typeof(SanctumCandlesellerCard),
        [typeof(EnergyShaperMinion)] = typeof(EnergyShaperCard),
        [typeof(KhadgarMinion)] = typeof(KhadgarCard),
        [typeof(CommanderSivaraMinion)] = typeof(CommanderSivaraCard),
        [typeof(MordreshFireEyeMinion)] = typeof(MordreshFireEyeCard),
        [typeof(AstromancerSolarianMinion)] = typeof(AstromancerSolarianCard),
        [typeof(SolarianPrimeMinion)] = typeof(SolarianPrimeCard),
        [typeof(ImpWranglerMinion)] = typeof(ImpWranglerCard),
        [typeof(RecklessApprenticeMinion)] = typeof(RecklessApprenticeCard),
        [typeof(IceWalkerMinion)] = typeof(IceWalkerCard),
        [typeof(ScrappyScavengerMinion)] = typeof(ScrappyScavengerCard),
        [typeof(KelThuzadMinion)] = typeof(KelThuzadCard),
        [typeof(RobocallerMinion)] = typeof(RobocallerCard),
        [typeof(ConnivingConmanMinion)] = typeof(ConnivingConmanCard),
        [typeof(VexallusMinion)] = typeof(VexallusCard),
        [typeof(MaroonedArchmageMinion)] = typeof(MaroonedArchmageCard),
        // 地标（悬停显示地标卡；不进入 MinionTypes 随机召唤池）
        [typeof(NightcloakSanctumLandmark)] = typeof(NightcloakSanctumCard),
        [typeof(TrinketShopLandmark)] = typeof(TrinketShopCard),
        [typeof(TidePoolLandmark)] = typeof(TidePoolCard)
    };

    /// <summary>
    /// 取随从生物对应的卡牌类型（未映射返回 null）
    /// </summary>
    public static Type? GetCardType(Type minionType)
    {
        return CardByMinionType.TryGetValue(minionType, out var cardType) ? cardType : null;
    }

    /// <summary>
    /// 全部随从生物类型（含地标；随机召唤池如需排除地标请过滤 <see cref="IsLandmarkType"/>）
    /// </summary>
    public static IEnumerable<Type> MinionTypes => CardByMinionType.Keys;

    /// <summary>
    /// 该随从生物类型是否为地标（地标不进入随机召唤池）
    /// </summary>
    public static bool IsLandmarkType(Type minionType)
    {
        return typeof(JainaLandmarkBase).IsAssignableFrom(minionType);
    }
}
