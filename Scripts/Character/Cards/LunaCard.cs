using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// LunaCard - 吉安娜随从卡。召唤 2/4 的 LunaMinion。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class LunaCard : JainaMinionCardTemplate
{
    public override string CustomPortraitPath => "res://assets/card_art/stargazer_luna.png";
    protected override Type MinionType => typeof(LunaMinion);

    protected override int MinionAttack => 2;

    protected override int MinionHealth => 4;

    public LunaCard()
        : base(1, CardRarity.Rare)
    {
    }
}
