using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 非公平游戏 (Rigged Faire Game) - 0费技能牌（普通）。
/// 没有受到攻击时，下回合抽3张牌。
/// 升级后变为巫卜 (Divination)：消灭1个小精灵（选择自己场上的一个小精灵），抽3张牌。奥术派系。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class UnfairGame : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌：攻击牌和技能牌都视为法术牌。
    /// 升级后（巫卜）：奥术派系（炉石原卡巫卜为奥术法术）。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded
        ? [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Arcane]
        : [jaina.Scripts.Character.Keywords.JainaKeywords.Spell];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override string CustomPortraitPath =>
        IsUpgraded ? "res://assets/card_art/volume_up.png" : "res://assets/card_art/manufacturing_error.png";

    /// <summary>
    /// 升级后（巫卜）需要选择目标（自己场上的一个小精灵）；基础版不需要目标。
    /// </summary>
    public override TargetType TargetType => IsUpgraded ? JainaTargetTypes.AnyOwnImp : TargetType.None;

    public UnfairGame()
        : base(0, CardType.Skill, CardRarity.Common, TargetType.None, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"巫卜 (Divination)"
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

    protected override void OnUpgrade()
    {
        // 升级为巫卜：奥术派系（基础版无派系）。
        // 需显式加入：LocalKeywords 懒初始化只算一次，升级前缓存的 Keywords
        // 不含 Arcane，悬停提示（原版 HoverTips 遍历 Keywords）不会出现奥术解释。
        AddKeyword(jaina.Scripts.Character.Keywords.JainaKeywords.Arcane);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        if (IsUpgraded)
        {
            // 巫卜：消灭1个小精灵（选择自己场上的一个小精灵），抽3张牌。
            // 目标合法性由 AnyOwnImp 目标类型保证（手打只能选自己的小精灵；
            // 随机释放经 PickRandomTarget 按 IsValidTarget 过滤，兜底选错时防御跳过）。
            if (cardPlay.Target is not { IsAlive: true } imp ||
                imp.Monster is not ImpMinion ||
                imp.PetOwner != base.Owner)
            {
                return;
            }
            await CreatureCmd.Kill(imp);
            // 抽3张牌（满手自动改道弃牌堆，原版语义）
            await CardPileCmd.Draw(choiceContext, 3, base.Owner);
            return;
        }

        // 非公平游戏：没有受到攻击时，下回合抽3张牌。
        // "受到攻击" = 上一玩家回合（含其后的敌人行动轮）内，英雄被敌人造成的
        // 未格挡伤害打中（UnblockedDamage > 0；被格挡全挡/自己扣血不算）。
        // 判定走战斗历史（原版 Spite/Flatten 同机制，两端模拟一致，联机确定性）。
        if (!WasAttackedByEnemyLastPlayerTurn())
        {
            await PowerCmd.Apply<DrawCardsNextTurnPower>(
                choiceContext, base.Owner.Creature, 3m, base.Owner.Creature, this);
        }
    }

    /// <summary>
    /// 玩家英雄在"上一个玩家回合"期间是否被敌人攻击（受到未格挡伤害）。
    /// CombatHistory 在每场战斗开始时清空（CombatManager.History.Clear），跨战斗无残留。
    /// </summary>
    private bool WasAttackedByEnemyLastPlayerTurn()
    {
        var combatState = base.Owner.Creature.CombatState;
        if (combatState == null)
        {
            return false;
        }
        return CombatManager.Instance.History.Entries
            .OfType<DamageReceivedEntry>()
            .Any(e => e.Receiver == base.Owner.Creature
                      && e.Result.UnblockedDamage > 0
                      && e.Dealer != null
                      && e.Dealer.IsEnemy
                      && e.HappenedLastPlayerTurn(base.Owner));
    }
}
