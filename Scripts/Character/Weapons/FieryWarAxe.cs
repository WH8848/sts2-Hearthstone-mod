using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Cards;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Weapons;

/// <summary>
/// 炽炎战斧 (Fiery War Axe) - 吉安娜武器能力卡（普通，2 费）。
/// 攻击力 3，耐久度 2。打出后装备：赋予角色 3 点攻击次数（每回合一次），
/// 角色每攻击一次耐久 -1，归零时武器能力消失。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class FieryWarAxe : JainaWeaponCardTemplate
{
    public override int WeaponAttack => 3;

    public override int WeaponDurability => 2;

    public override string CustomPortraitPath => "res://assets/card_art/fiery_war_axe.png";

    public FieryWarAxe()
        : base(2, CardRarity.Common)
    {
    }
}
