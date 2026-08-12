using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// AegwynnCard - 吉安娜随从卡。召唤 5/5 的 AegwynnMinion。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class AegwynnCard : JainaMinionCardTemplate
{
    public override string CustomPortraitPath => "res://assets/card_art/aegwynn.png";
    protected override Type MinionType => typeof(AegwynnMinion);

    protected override int MinionAttack => 5;

    protected override int MinionHealth => 5;

    public AegwynnCard()
        : base(1, CardRarity.Rare)
    {
    }
}
