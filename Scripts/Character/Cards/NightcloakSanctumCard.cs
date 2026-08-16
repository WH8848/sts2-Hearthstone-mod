using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 夜隐者圣所 (Nightcloak Sanctum) - 1费地标卡（普通）。
/// 占随从槽，每两个回合可点击使用一次：
/// 给予一名角色1层冻结，召唤一个2/2的不稳定的骷髅。耐久度3。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class NightcloakSanctumCard : JainaLandmarkCardTemplate
{
    /// <summary>
    /// 卡牌原画：炉石传说"夜隐者圣所"（Nightcloak Sanctum, ALT_108）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/nightcloak_sanctum.png";

    /// <summary>
    /// 耐久度 3
    /// </summary>
    public override int LandmarkDurability => 3;

    protected override Type LandmarkType => typeof(NightcloakSanctumLandmark);

    public NightcloakSanctumCard()
        : base(1, CardRarity.Common)
    {
    }
}
