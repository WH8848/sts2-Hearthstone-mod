using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 小玩物小屋 (Trinket Tracker) - 1费地标卡（罕见）。
/// 占随从槽，每两个回合可点击使用一次：
/// 抽一张牌。如果你在本回合中使用抽到的这张牌，重新开启本地标。耐久度4。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class TrinketShopCard : JainaLandmarkCardTemplate
{
    /// <summary>
    /// 卡牌原画：炉石传说"小玩物小屋"（Trinket Tracker, PIP_059）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/trinket_tracker.png";

    /// <summary>
    /// 耐久度 4
    /// </summary>
    public override int LandmarkDurability => 4;

    protected override Type LandmarkType => typeof(TrinketShopLandmark);

    public TrinketShopCard()
        : base(1, CardRarity.Uncommon)
    {
    }
}
