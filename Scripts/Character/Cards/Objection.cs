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

        // 异议：拦截下一次敌人攻击伤害；法术反制（升级）：拦截下一次敌人减益
        var secret = await PowerCmd.Apply<SecretPower>(choiceContext, base.Owner.Creature, 1m, base.Owner.Creature, this);
        if (secret != null)
        {
            secret.IsCounterspell = IsUpgraded;
        }
    }
}
