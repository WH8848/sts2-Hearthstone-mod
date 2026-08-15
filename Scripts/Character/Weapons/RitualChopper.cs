using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Cards;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Weapons;

/// <summary>
/// 仪式斩斧 (Ritual Chopper) - 吉安娜武器能力卡（稀有，2 费）。
/// 攻击力 2，耐久度 3。打出后装备：赋予角色 2 点攻击次数（每回合一次），
/// 角色每攻击一次耐久 -1，归零时武器能力消失。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class RitualChopper : JainaWeaponCardTemplate
{
    public override int WeaponAttack => 2;

    public override int WeaponDurability => 3;

    public override string CustomPortraitPath => "res://assets/card_art/ritual_chopper.png";

    public RitualChopper()
        : base(2, CardRarity.Rare)
    {
    }
}
