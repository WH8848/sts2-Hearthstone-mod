using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Cards;
using jaina.Scripts.Character.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Weapons;

/// <summary>
/// 金属探测器 (Metal Detector) - 1费武器能力卡（罕见）。
/// 亡语：获取一张幸运币。武器：攻击力 3 / 耐久度 2。
/// 升级后：攻击1次，获取一张幸运币（亡语改为攻击后效果）。
/// </summary>
[RegisterCard(typeof(jaina.Scripts.Character.JainaCardPool))]
public sealed class MetalDetectorCard : JainaWeaponCardTemplate
{
    public override int WeaponAttack => 3;

    public override int WeaponDurability => 2;

    /// <summary>
    /// 可升级（升级后亡语改为攻击后效果）
    /// </summary>
    public override int MaxUpgradeLevel => 1;

    public override string CustomPortraitPath => "res://assets/card_art/metal_detector.png";

    /// <summary>
    /// 关键词：武器 + 耐久度；基础版额外带亡语（升级后为攻击后效果，无亡语词条）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded
        ? [JainaKeywords.Weapon, JainaKeywords.Durability]
        : [JainaKeywords.Weapon, JainaKeywords.Durability, JainaKeywords.Deathrattle];

    public MetalDetectorCard()
        : base(1, CardRarity.Uncommon)
    {
    }

    /// <summary>
    /// 打出：装备武器（顶替旧武器，旧武器亡语会先触发），并按升级状态挂载效果回调：
    /// 基础版 → 武器摧毁时亡语获取幸运币；升级版 → 每次武器攻击后获取幸运币。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 装备武器（顶替旧武器；若旧武器带亡语会先触发其亡语）
        await JainaWeaponSlot.Equip(choiceContext, base.Owner, WeaponAttack, WeaponDurability, this);

        // 给新武器挂载效果回调
        var weapon = base.Owner.Creature.Powers.OfType<JainaWeaponPower>().FirstOrDefault();
        if (weapon == null)
        {
            return;
        }
        if (IsUpgraded)
        {
            weapon.OnAttack = ctx => AddCoin(ctx);
        }
        else
        {
            weapon.OnDestroyed = ctx => AddCoin(ctx);
        }
    }

    /// <summary>
    /// 获取一张幸运币（0费：获得 1 点能量，保留）加入手牌；
    /// 手牌满时正确塞入弃牌堆（不再直接消失）。
    /// </summary>
    private async Task AddCoin(PlayerChoiceContext choiceContext)
    {
        var owner = base.Owner;
        if (owner == null || owner.Creature == null || owner.Creature.CombatState == null)
        {
            return;
        }
        var combatState = owner.Creature.CombatState;
        var canonical = ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId(typeof(LuckyCoin)));
        if (canonical == null)
        {
            return;
        }
        var coin = combatState.CreateCard(canonical, owner);
        jaina.Scripts.Character.JainaCastTracker.MarkGenerated(coin);
        if (jaina.Scripts.Character.JainaHandHelper.IsHandFull(owner))
        {
            // 手牌满：幸运币正确塞入弃牌堆
            await CardPileCmd.AddGeneratedCardToCombat(coin, PileType.Discard, owner);
            return;
        }
        await CardPileCmd.AddGeneratedCardToCombat(coin, PileType.Hand, owner);
    }
}
