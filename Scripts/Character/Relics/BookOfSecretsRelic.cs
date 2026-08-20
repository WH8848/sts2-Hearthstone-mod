using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Relics;

/// <summary>
/// 秘法宝典 (Book of Secrets) - 吉安娜罕见遗物。
/// 每场战斗开始时，随机获取3张法师法术牌（入手牌；从吉安娜全法术池随机
/// 抽取 3 张，含升级形态，排除英雄技能/任务线卡，与本局已生成的卡不重复）。
/// </summary>
[RegisterRelic(typeof(JainaRelicPool))]
public sealed class BookOfSecretsRelic : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    /// <summary>
    /// 遗物图标：小图 85x85（程序绘制占位，卡图待 wiki 原画）
    /// </summary>
    public override string? CustomIconPath => "res://assets/relic_icons/book_of_secrets_icon.png";

    /// <summary>
    /// 遗物轮廓图标：85x85
    /// </summary>
    public override string? CustomIconOutlinePath => "res://assets/relic_icons/book_of_secrets_outline.png";

    /// <summary>
    /// 遗物大图：256x256
    /// </summary>
    public override string? CustomBigIconPath => "res://assets/relic_icons/book_of_secrets_big.png";

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override async Task BeforeCombatStart()
    {
        if (Owner.Creature.IsDead || Owner.Creature.CombatState == null)
        {
            return;
        }

        Flash();

        // 每场战斗开始时，随机获取3张法师法术牌（入手牌）
        var combatState = Owner.Creature.CombatState;
        var rng = Owner.RunState.Rng.CombatCardSelection;

        // 吉安娜全法术池（含升级形态，排除英雄技能/任务线卡）
        var pool = jaina.Scripts.Character.JainaCastTracker.BuildAllSpellPool();
        if (pool.Count == 0)
        {
            return;
        }

        // 随机取 3 张（允许重复类型，符合"随机获取3张"字面语义）
        for (int i = 0; i < 3; i++)
        {
            var (type, upgradeLevel) = rng.NextItem(pool);
            var card = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
                combatState, Owner, type, upgradeLevel);
            if (card == null)
            {
                continue;
            }
            // 标记衍生（蓝光 + 牌库外计数；随机获取的法术按衍生处理）
            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(card);
            // 手牌满时 AddGeneratedCardToCombat 自动改道弃牌堆（原版满手语义）
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
        }
    }
}
