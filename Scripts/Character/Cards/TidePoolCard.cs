using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 潮汐之池 (Tide Pools) - 1费地标卡（普通）。
/// 占随从槽，每两个回合可点击使用一次：
/// 发现一张费用消耗小于或等于1点的法术牌。在你施放一个法术后，重新开启本地标。耐久度3。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class TidePoolCard : JainaLandmarkCardTemplate
{
    /// <summary>
    /// 卡牌原画：炉石传说"潮汐之池"（Tide Pools, PIP_058）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/tide_pools.png";

    /// <summary>
    /// 耐久度 3
    /// </summary>
    public override int LandmarkDurability => 3;

    protected override Type LandmarkType => typeof(TidePoolLandmark);

    public TidePoolCard()
        : base(1, CardRarity.Common)
    {
    }
}
