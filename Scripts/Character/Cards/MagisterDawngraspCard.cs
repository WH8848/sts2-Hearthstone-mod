using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Powers;
using MinionLib.Targeting;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 魔导师晨拥 (Magister Dawngrasp) - 2费英雄卡（稀有）。
/// 战吼：再次施放你在本局对战中施放过的每个法术派系的一个法术（随机目标，免费自动打出）。
/// 获得 5 点格挡。替换英雄技能为"奥术爆裂"。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class MagisterDawngraspCard : JainaHeroCardTemplate
{
    /// <summary>
    /// 获得 5 点格挡
    /// </summary>
    protected override int HeroArmor => 5;

    /// <summary>
    /// 悬停提示：先显示基类的英雄技能卡提示（替换后的英雄技能"奥术爆裂"），
    /// 再显示格挡关键词注释（描述中"获得5点格挡"）。
    /// 注意：必须链式调用 base.AdditionalHoverTips —— 基类负责 CardHoverTip(英雄技能卡)，
    /// 直接覆写而不接 base 会导致悬停时左侧不显示英雄技能。
    /// </summary>
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            foreach (var tip in base.AdditionalHoverTips)
            {
                yield return tip;
            }
            yield return HoverTipFactory.Static(StaticHoverTip.Block);
        }
    }

    /// <summary>
    /// 替换英雄技能为奥术爆裂
    /// </summary>
    protected override System.Type? HeroPowerType => typeof(ArcaneBurstCard);

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override string CustomPortraitPath => "res://assets/card_art/magister_dawngrasp.png";

    public MagisterDawngraspCard()
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
    /// 战吼：再次施放你在本局对战中施放过的每个法术派系的一个法术
    /// （每个派系取其最近施放的法术，按升级级别/衍生状态恢复副本，随机目标自动打出）
    /// </summary>
    protected override async Task OnHeroBattlecry(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var combatState = base.Owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
        // 再次施放"我"施放过的每个法术派系的一个法术（按玩家区分，联机不混入队友的）
        var mySchools = rec.LastCastBySchoolByPlayer.TryGetValue(base.Owner.NetId, out var schools)
            ? schools
            : new Dictionary<JainaSpellSchool, (Type Type, int UpgradeLevel, bool IsGenerated)>();
        foreach (var (school, last) in mySchools)
        {
            var card = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
                combatState, base.Owner, last.Type, last.UpgradeLevel);
            if (card == null)
            {
                continue;
            }
            if (last.IsGenerated)
            {
                jaina.Scripts.Character.JainaCastTracker.MarkGenerated(card);
            }
            // 被再次释放的卡自动带消耗词条：打出后进消耗堆（不进弃牌堆）
            card.AddKeyword(CardKeyword.Exhaust);

            // 单目标法术：从场上所有活物（含队友/自己）随机选合法目标
            Creature? target = null;
            if (card.TargetType == TargetType.AnyEnemy || card.TargetType == TargetType.AnyPlayer ||
                card.TargetType == TargetType.AnyAlly ||
                (CustomTargetTypeManager.TryGetCustomTargetType(card.TargetType, out var customType) &&
                 customType.IsSingleTarget))
            {
                var pool = combatState.Creatures
                    .Where(c => c != null && c.IsAlive && card.IsValidTarget(c))
                    .ToList();
                target = pool.Count > 0 ? base.Owner.RunState.Rng.CombatTargets.NextItem(pool) : null;
                if (target == null)
                {
                    continue;
                }
            }
            jaina.Scripts.Character.Powers.RommathReplayTracker.Mark(card);
            await CardCmd.AutoPlay(choiceContext, card, target);
        }
    }
}
