using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 禁忌烈焰 (Forbidden Flame) - X费攻击牌（罕见，火焰派系）。
/// 对一个角色造成 10x 点伤害（x = 消耗的能量）。
/// 升级后（禁忌烈焰+）：造成 12x 点伤害。
/// 卡面伤害数字动态显示：= 倍率 × 当前能量（与 X 费用层一致，打出时 x = 实际消耗）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class ForbiddenFlameCard : JainaSpellCardTemplate
{
    /// <summary>
    /// X 费用：自动消耗玩家全部剩余能量
    /// </summary>
    protected override bool HasEnergyCostX => true;

    /// <summary>
    /// 法术牌 + 火焰派系
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Fire];

    /// <summary>
    /// 动态伤害：倍率（10/12）× 当前能量（与 X 费用层同源显示；能量变化即跟随）。
    /// 用项目 ComputedDamageVar（DamageVar 子类，强转安全——ComputedDynamicVar 放
    /// "Damage" 槽会导致打出/附魔/牌库网格 InvalidCastException）。
    /// canonical（图鉴）不可变实例：显示静态基础倍率（10/12）。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        IsUpgraded
            ? [new ComputedDamageVar(12m, ComputeDisplayDamage)]
            : [new ComputedDamageVar(10m, ComputeDisplayDamage)];

    /// <summary>
    /// 动态伤害委托：canonical 不可变实例返回基础倍率；战斗实例返回 倍率 × 当前能量。
    /// </summary>
    private static decimal ComputeDisplayDamage(CardModel card)
    {
        if (card == null || !card.IsMutable)
        {
            return card?.CurrentUpgradeLevel >= 1 ? 12m : 10m;
        }
        var multiplier = card.CurrentUpgradeLevel >= 1 ? 12m : 10m;
        return multiplier * (card.Owner?.PlayerCombatState?.Energy ?? 0);
    }

    public override string CustomPortraitPath => "res://assets/card_art/forbidden_flame.png";

    public ForbiddenFlameCard()
        : base(0, CardType.Attack, CardRarity.Uncommon, JainaTargetTypes.AnyTargetable, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"禁忌烈焰+"
    /// </summary>
    

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // x = 消耗的能量；伤害 = (10 或 12) × x
        int x = ResolveEnergyXValue();
        int damage = (IsUpgraded ? 12 : 10) * x;
        if (cardPlay.Target is { IsAlive: true } target)
        {
            // 攻击伤害：吃力量加成（与原版多次攻击牌一致）
            await DamageCmd.Attack(damage)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(choiceContext);
        }
    }
}
