using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// RenathalCard - 吉安娜随从卡。召唤 3/4 的 RenathalMinion。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class RenathalCard : JainaMinionCardTemplate
{
    protected override Type MinionType => typeof(RenathalMinion);

    protected override int MinionAttack => 3;

    protected override int MinionHealth => 4;

    public RenathalCard()
        : base(1, CardRarity.Basic)
    {
    }
}
