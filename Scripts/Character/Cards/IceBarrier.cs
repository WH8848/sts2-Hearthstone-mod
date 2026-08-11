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
/// 不稳定的骷髅是冰冷案例的衍生物：悬停冰冷案例卡牌时，会显示不稳定的骷髅衍生物卡。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
[RegisterCharacterStarterCard(typeof(Jaina), 4)]
public sealed class IceBarrier : ModCardTemplate
{
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
        : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self, true)
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
    /// 悬停提示：升级后的冰冷案例显示不稳定的骷髅衍生物卡。
    /// </summary>
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
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
    /// 未升级（寒冰护体）：获得 8 点护甲。
    /// 已升级（冰冷案例）：召唤 2 个 2/2 不稳定的骷髅 + 获得 4 点护甲。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (IsUpgraded)
        {
            // 冰冷案例：召唤 2 个 2/2 不稳定的骷髅（衍生物）
            _ = JainaMinionPool.SummonMinion<VolatileSkeleton>(choiceContext, base.Owner, maxHp: 2m, attack: 2m);
            _ = JainaMinionPool.SummonMinion<VolatileSkeleton>(choiceContext, base.Owner, maxHp: 2m, attack: 2m);
            // 获得 4 点护甲
            await CreatureCmd.GainBlock(base.Owner.Creature, new BlockVar(4m, ValueProp.Move), cardPlay);
            return;
        }

        // 寒冰护体：获得 8 点护甲
        await CreatureCmd.GainBlock(base.Owner.Creature, new BlockVar(8m, ValueProp.Move), cardPlay);
    }

    protected override void OnUpgrade()
    {
        // 升级后 API 不变，效果与悬停预览在 IsUpgraded 分支中实现
    }
}