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
using MegaCrit.Sts2.Core.ValueProps;
using jaina.Scripts.Character.Minions;
using jaina.Scripts.Character.Powers;
using MinionLib.Minion;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 深度冻结 (Deep Freeze) - 2费技能牌（罕见，冰霜派系）。
/// 给一个敌人 1 层冻结，召唤两个 3/6 的水元素。
/// 升级后变为海啸 (Tsunami)：召唤三个 3/6 的水元素（冻结攻击目标），并使其随机攻击敌人。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class DeepFreezeCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌 + 冰霜派系
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Frost];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override string CustomPortraitPath =>
        IsUpgraded ? "res://assets/card_art/tsunami.png" : "res://assets/card_art/deep_freeze.png";

    public DeepFreezeCard()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy, true)
    {
    }

    /// <summary>
    /// 目标：深度冻结指向一名敌人（冻结目标）；升级后的海啸不指向目标
    /// （召唤的水元素随机攻击敌人）。
    /// </summary>
    public override TargetType TargetType =>
        IsUpgraded ? TargetType.None : TargetType.AnyEnemy;

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
    /// 悬停提示：显示召唤的衍生物"水元素"卡（深度冻结召唤 2 个 / 海啸召唤 3 个；
    /// 参考灵体采集者显示小精灵/死神之躯显示骷髅的做法）
    /// </summary>
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            yield return new CardHoverTip(ModelDb.Card<WaterElementalCard>());
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        if (IsUpgraded)
        {
            // 海啸：召唤三个 3/6 水元素（冻结攻击目标），并使其随机攻击敌人
            var minions = new List<Creature>();
            for (int i = 0; i < 3; i++)
            {
                var minion = await JainaMinionPool.SummonMinionByType(
                    choiceContext, base.Owner, typeof(WaterElementalMinion),
                    maxHp: 6m, attack: 3m, position: MinionPosition.FrontUpper);
                if (minion != null)
                {
                    minions.Add(minion);
                }
            }
            foreach (var minion in minions)
            {
                if (!minion.IsAlive)
                {
                    continue;
                }
                // 随机攻击一个敌人（水元素 AfterDamageGiven 会自动给受伤角色挂冻结）
                var enemies = base.Owner.Creature.CombatState
                    .GetOpponentsOf(minion)
                    .Where(e => e != null && e.IsAlive && e.IsHittable)
                    .ToList();
                if (enemies.Count == 0)
                {
                    continue;
                }
                var target = base.Owner.RunState.Rng.CombatTargets.NextItem(enemies);
                if (target == null)
                {
                    continue;
                }
                await CreatureCmd.Damage(choiceContext, [target], 3m, ValueProp.Move, minion);
            }
        }
        else
        {
            // 深度冻结：给目标 1 层冻结，召唤两个 3/6 水元素
            if (cardPlay.Target is { IsAlive: true } target)
            {
                await PowerCmd.Apply<FreezePower>(
                    choiceContext, [target], 1m, base.Owner.Creature, this);
            }
            for (int i = 0; i < 2; i++)
            {
                await JainaMinionPool.SummonMinionByType(
                    choiceContext, base.Owner, typeof(WaterElementalMinion),
                    maxHp: 6m, attack: 3m, position: MinionPosition.FrontUpper);
            }
        }
    }
}
