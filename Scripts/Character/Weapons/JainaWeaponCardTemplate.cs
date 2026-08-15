using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Keywords;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Weapons;

/// <summary>
/// 吉安娜武器能力卡基类（炉石传说式武器位）。
/// 武器 = 能力卡（Power 类型），带"武器"与"耐久度"关键词。
/// 打出后装备武器：挂 <see cref="JainaWeaponPower"/> 到玩家角色（耐久度 = Amount），
/// 并赋予角色一次攻击次数（数值等同于武器攻击力）；角色每攻击一次耐久 -1，
/// 归零时能力消失；打出第二张武器时旧武器能力被顶替。
/// </summary>
public abstract class JainaWeaponCardTemplate : ModCardTemplate
{
    /// <summary>
    /// 武器攻击力
    /// </summary>
    public abstract int WeaponAttack { get; }

    /// <summary>
    /// 武器初始耐久度
    /// </summary>
    public abstract int WeaponDurability { get; }

    /// <summary>
    /// 关键词：武器（只可装备1把）+ 耐久度（归零时卡牌被消耗）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [JainaKeywords.Weapon, JainaKeywords.Durability];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Attack", WeaponAttack),
        new IntVar("Durability", WeaponDurability)
    ];

    protected JainaWeaponCardTemplate(int cost, CardRarity rarity)
        : base(cost, CardType.Power, rarity, TargetType.Self, true)
    {
    }

    /// <summary>
    /// 打出：装备武器（顶替旧武器，赋予攻击次数）
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await JainaWeaponSlot.Equip(choiceContext, base.Owner, WeaponAttack, WeaponDurability, this);
    }
}
