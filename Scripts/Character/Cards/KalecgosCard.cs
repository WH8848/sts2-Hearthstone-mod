using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// KalecgosCard - 吉安娜随从卡。召唤 4/12 的 KalecgosMinion。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class KalecgosCard : JainaMinionCardTemplate
{
    protected override Type MinionType => typeof(KalecgosMinion);

    protected override int MinionAttack => 4;

    protected override int MinionHealth => 12;

    public KalecgosCard()
        : base(3, CardRarity.Rare)
    {
    }
}
