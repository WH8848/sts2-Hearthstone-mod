using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 泰瑟兰·血望者 (Tesslan Bloodwatcher) - 2 费先古随从卡。
/// 你的套牌之外的卡牌耗能减少 1 点。属性 2/5。
/// 升级后费用 2 -> 1。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
[RegisterDustyTomeCard(typeof(jaina.Scripts.Character.Jaina))]
public sealed class TesslanBloodwatcherCard : JainaMinionCardTemplate
{
    public override string CustomPortraitPath => "res://assets/card_art/iceblood_tower.png";

    protected override Type MinionType => typeof(TesslanBloodwatcherMinion);

    protected override int MinionAttack => 2;

    protected override int MinionHealth => 5;

    public TesslanBloodwatcherCard()
        : base(2, CardRarity.Ancient)
    {
    }

    protected override void OnUpgrade()
    {
        base.OnUpgrade();
        EnergyCost.UpgradeBy(-1);
    }
}
