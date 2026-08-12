using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// SorcererApprenticeCard - 吉安娜随从卡。召唤 3/2 的 SorcererApprenticeMinion。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class SorcererApprenticeCard : JainaMinionCardTemplate
{
    protected override Type MinionType => typeof(SorcererApprenticeMinion);

    protected override int MinionAttack => 3;

    protected override int MinionHealth => 2;

    public SorcererApprenticeCard()
        : base(0, CardRarity.Uncommon)
    {
    }
}
