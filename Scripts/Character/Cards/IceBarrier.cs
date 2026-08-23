using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 寒冰护体 (Ice Barrier) - 吉安娜的基础技能牌。
/// 1费：获得 8 点护甲值。
/// 升级后变为"冰冷案例 (Cold Case)"：召唤 2 个 2/2 的不稳定的骷髅，并获得 4 点护甲值。
/// 战场上放不下的骷髅会立即爆炸（对随机敌人造成 2 点伤害）。
/// 不稳定的骷髅是冰冷案例的衍生物：悬停冰冷案例卡牌时，会显示不稳定的骷髅衍生物卡。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
[RegisterCharacterStarterCard(typeof(Jaina), 4)]
public sealed class IceBarrier : JainaSpellCardTemplate
{
    /// <summary>
    /// "防御"标签已移至昔时古树（新初始防御卡）/古拉巴什贡品（防御+）——
    /// 寒冰护盾不再视为 Defend（升级为冰冷案例后同样不是）。
    /// </summary>
    protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag>();

    /// <summary>
    /// 法术牌：攻击牌和技能牌都视为法术牌
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Frost];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(8m, ValueProp.Move)
    ];

    /// <summary>
    /// 卡牌原画：炉石传说"寒冰屏障"官方高清原画。
    /// 升级后变为"冰冷案例 (Cold Case)"，卡图同步切换为 Cold Case 官方原画。
    /// </summary>
    public override string CustomPortraitPath =>
        IsUpgraded
            ? "res://assets/card_art/cold_case.png"
            : "res://assets/card_art/ice_barrier.png";

    public IceBarrier()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"冰冷案例 (Cold Case)"
    /// </summary>
    public override string Title
    {
        get
        {
            var title = new LocString("cards", base.Id.Entry + ".title");
            if (!IsUpgraded)
            {
                return title.GetFormattedText();
            }
            LocString? upgraded = LocString.GetIfExists("cards", base.Id.Entry + ".titleUpgraded");
            return upgraded?.GetFormattedText() ?? title.GetFormattedText() + "+";
        }
    }

    /// <summary>
    /// 悬停提示：格挡关键词注释（右侧显示）；升级后的冰冷案例显示不稳定的骷髅衍生物卡。
    /// </summary>
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            yield return HoverTipFactory.Static(StaticHoverTip.Block);
            if (!IsUpgraded)
            {
                yield break;
            }
            // 不稳定的骷髅是冰冷案例的衍生物（通过 ModelDb 获取已注册的 canonical 卡实例）
            yield return new CardHoverTip(ModelDb.Card<VolatileSkeletonCard>());
        }
    }

    /// <summary>
    /// 打出效果：
    /// 未升级（寒冰护盾）：本回合内受到攻击时，获得 8 点护甲。
    /// 已升级（冰冷案例）：召唤 2 个 2/2 不稳定的骷髅 + 获得 4 点护甲；
    /// 战场上放不下的骷髅会立即爆炸（对随机敌人造成 2 点伤害）。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        if (IsUpgraded)
        {
            // 冰冷案例：召唤 2 个 2/2 不稳定的骷髅（衍生物）
            // 注意：必须 await —— 召唤走随从动作同步流，fire-and-forget
            // 会导致两端执行时序错位，联机状态分歧断联。
            // 战场上放不下的骷髅会立即爆炸（对随机敌人造成 2 点伤害，亡语伤害）。
            for (int i = 0; i < 2; i++)
            {
                await JainaMinionPool.SummonVolatileSkeletonOrExplode(
                    choiceContext, base.Owner, maxHp: 2m, attack: 2m);
            }
            // 获得 4 点护甲
            await CreatureCmd.GainBlock(base.Owner.Creature, new BlockVar(4m, ValueProp.Move), cardPlay);
            return;
        }

        // 寒冰护盾：本回合内受到攻击时，获得 8 点护甲（受击触发，回合结束失效）
        await PowerCmd.Apply<jaina.Scripts.Character.Powers.IceBarrierPower>(
            choiceContext, [base.Owner.Creature], 8m, base.Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        // 升级为冰冷案例：护甲从 8 降为 4（动态 BlockVar 同步 BaseValue，
        // 描述 {Block:diff()} 与结算一致；召唤骷髅由 IsUpgraded 分支处理）
        base.DynamicVars.Block.UpgradeValueBy(-4m);
    }
}