using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// VardenCard - 吉安娜随从卡。召唤 3/3 的 VardenMinion。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class VardenCard : JainaMinionCardTemplate
{
    protected override Type MinionType => typeof(VardenMinion);

    protected override int MinionAttack => 3;

    protected override int MinionHealth => 3;

    public VardenCard()
        : base(1, CardRarity.Rare)
    {
    }
}
