using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 异议 (Objection) - 1费：敌人的下一个攻击意图不会造成任何伤害。
/// 升级后变为法术反制 (Counterspell)：敌人的下一个非攻击意图不会触发任何效果。
/// 简化实现：异议拦截下一次敌人造成的攻击伤害；法术反制拦截下一次敌人造成的任何伤害。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class Objection : ModCardTemplate
{
    /// <summary>
    /// 法术牌 + 奥术派系（仅升级后：法术反制才有派系）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        IsUpgraded
            ? [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Arcane]
            : [jaina.Scripts.Character.Keywords.JainaKeywords.Spell];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override string CustomPortraitPath => "res://assets/card_art/objection.png";

    public Objection()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.None, true)
    {
    }

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

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 异议：拦截敌人下一次攻击意图的伤害（只拦攻击）；
        // 法术反制（升级）：拦截敌人下一次非攻击意图的效果（只拦非攻击）。
        // 两个独立 Power，同时在场时各自拦截一类意图。
        if (IsUpgraded)
        {
            await PowerCmd.Apply<jaina.Scripts.Character.Powers.CounterspellPower>(
                choiceContext, base.Owner.Creature, 1m, base.Owner.Creature, this);
        }
        else
        {
            await PowerCmd.Apply<jaina.Scripts.Character.Powers.ObjectionPower>(
                choiceContext, base.Owner.Creature, 1m, base.Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级为法术反制：加入奥术派系关键词（LocalKeywords 懒缓存可能已在未升级状态初始化）
        AddKeyword(jaina.Scripts.Character.Keywords.JainaKeywords.Arcane);
    }
}
