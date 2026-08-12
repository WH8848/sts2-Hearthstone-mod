using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// MozakiCard - 吉安娜随从卡。召唤 3/8 的 MozakiMinion。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class MozakiCard : JainaMinionCardTemplate
{
    public override string CustomPortraitPath => "res://assets/card_art/mozaki.png";
{
    protected override Type MinionType => typeof(MozakiMinion);

    protected override int MinionAttack => 3;

    protected override int MinionHealth => 8;

    public MozakiCard()
        : base(1, CardRarity.Rare)
    {
    }
}
