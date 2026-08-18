using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Weapons;

/// <summary>
/// 艾露尼斯 (Aluneth) - 2费武器能力卡（稀有）。
/// 每回合开始时抽三张牌。武器：攻击力 0 / 耐久度 3。
/// 武器能力牌不可升级。
/// </summary>
[RegisterCard(typeof(jaina.Scripts.Character.JainaCardPool))]
public sealed class AlunethCard : JainaWeaponCardTemplate
{
    public override int WeaponAttack => 0;

    public override int WeaponDurability => 3;

    public override string CustomPortraitPath => "res://assets/card_art/aluneth.png";

    public AlunethCard()
        : base(2, CardRarity.Rare)
    {
    }

    /// <summary>
    /// 卡名不变
    /// </summary>
    public override string Title
    {
        get
        {
            var title = new LocString("cards", base.Id.Entry + ".title");
            return title.GetFormattedText();
        }
    }

    /// <summary>
    /// 打出：装备武器（顶替旧武器），并挂载/顶替"每回合抽3张"效果。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 顶替旧的艾露尼斯抽牌效果（换武器后不再生效）
        var oldAluneth = base.Owner.Creature.Powers.OfType<AlunethPower>().FirstOrDefault();
        if (oldAluneth != null)
        {
            await PowerCmd.Remove(oldAluneth);
        }

        // 装备武器（顶替旧武器能力）
        await JainaWeaponSlot.Equip(choiceContext, base.Owner, WeaponAttack, WeaponDurability, this);

        // 武器能力栏只显示特殊效果（攻击力在角色攻击意图、耐久度在图标右下角标）
        var weapon = base.Owner.Creature.Powers.OfType<JainaWeaponPower>().FirstOrDefault();
        if (weapon != null)
        {
            weapon.EffectLocKey = "JAINA_POWER_ALUNETH_EFFECT.description";
        }

        // 挂载"每回合开始抽3张"效果
        await PowerCmd.Apply<AlunethPower>(choiceContext, [base.Owner.Creature], 1m, base.Owner.Creature, this);
    }
}
