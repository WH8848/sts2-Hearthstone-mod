using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Minions;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 指挥官西瓦拉 (Commander Sivara) - 1费随从卡（稀有，纳迦种族）。
/// 战吼：如果你在此牌在你手中时施放过三个法术，则将那些法术的复制置回你的手牌。
/// 属性 3/5。
/// 追踪：本卡在手牌期间，玩家每施放一张法术牌（攻击/技能牌，不含英雄技能卡）
/// 记录其类型与升级级别（最多 3 个）；打出时若已满 3 个，将它们的复制置入手牌。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class CommanderSivaraCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 本卡在手牌期间施放过的法术（类型 + 施放时的升级级别，最多 3 个）
    /// </summary>
    private List<(Type Type, int UpgradeLevel)> _recordedSpells = [];

    /// <summary>
    /// 克隆时必须重置引用类型字段：MutableClone 是 MemberwiseClone 浅拷贝，
    /// 若共享 List，上一局记录的法术会污染 canonical 单例，
    /// 导致本局打出西瓦拉时直接复制到历史法术（跨局残留）。
    /// </summary>
    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _recordedSpells = [];
    }

    /// <summary>
    /// 卡牌原画：炉石传说"指挥官西瓦拉"（Commander Sivara, TSC_087）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/commander_sivara.png";

    protected override Type MinionType => typeof(CommanderSivaraMinion);

    protected override int MinionAttack => 3;

    protected override int MinionHealth => 5;

    /// <summary>
    /// 纳迦种族 + 战吼（悬停解释）+ 保留（回合结束留在手牌）+ 消耗（随从卡打出后消耗，模板默认）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Naga,
         jaina.Scripts.Character.Keywords.JainaKeywords.Battlecry, CardKeyword.Retain, CardKeyword.Exhaust];

    public CommanderSivaraCard()
        : base(1, CardRarity.Rare)
    {
    }

    /// <summary>
    /// 悬停提示（左侧悬浮）：显示本卡在手牌期间记录的、打出时会复制回手的法术牌
    /// （最多 3 个，按施放时的升级级别显示升级形态；未记录满则不显示）。
    /// 参考惊奇套牌/吉安娜的礼物显示候选卡的做法。
    /// </summary>
    protected override IEnumerable<IHoverTip> ExtraMinionHoverTips
    {
        get
        {
            foreach (var (type, upgradeLevel) in _recordedSpells)
            {
                var canonical = ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId(type));
                if (canonical == null)
                {
                    continue;
                }
                CardModel display = canonical;
                if (upgradeLevel > 0)
                {
                    // 显示施放时的升级形态（克隆后逐级升级；点燃等无限升级卡按实际级别）
                    var clone = (CardModel)canonical.MutableClone();
                    for (int i = 0; i < upgradeLevel && clone.CurrentUpgradeLevel < clone.MaxUpgradeLevel; i++)
                    {
                        clone.UpgradeInternal();
                    }
                    display = clone;
                }
                yield return new CardHoverTip(display);
            }
        }
    }

    /// <summary>
    /// 玩家打出任意卡后（本卡在手牌时收到该 hook）：
    /// 记录施放的法术牌（攻击/技能牌，不含英雄技能卡），最多 3 个。
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await Task.CompletedTask;
        if (Pile?.Type != PileType.Hand)
        {
            // 本卡不在手牌（未入手/已打出）：不记录
            return;
        }
        if (cardPlay.Card == null || cardPlay.Card.Owner != Owner)
        {
            return;
        }
        // 只记录玩家手打施放的法术——自动打出（AutoPlay：匣中古神/惊奇卡牌/戏法图腾/
        // 大法师的符文/罗曼斯/灰贤鹦鹉/诈骗犯重放等随机释放）不算"你施放的法术"，
        // 否则符文随机打出的法术会被西瓦拉错误复制回手牌
        if (cardPlay.IsAutoPlay)
        {
            return;
        }
        var played = cardPlay.Card;
        // 法术牌 = 攻击牌/技能牌，或挂"法术牌"关键词的卡（任务线卡等视为法术牌，可被复制）；
        // 英雄技能卡（火焰冲击等）不算法术
        if (played.Type != CardType.Attack && played.Type != CardType.Skill &&
            !played.Keywords.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Spell))
        {
            return;
        }
        if (HeroPowerHandHelper.IsHeroPowerCard(played))
        {
            return;
        }
        if (_recordedSpells.Count >= 3)
        {
            return;
        }
        _recordedSpells.Add((played.GetType(), played.CurrentUpgradeLevel));
    }

    /// <summary>
    /// 打出：召唤随从；战吼——若本卡在手牌期间已施放过 3 个法术，
    /// 将那些法术的复制（恢复施放时的升级级别）置回手牌，然后清空记录。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await base.OnPlay(choiceContext, cardPlay);

        // 战吼：满 3 个法术才触发
        if (_recordedSpells.Count < 3)
        {
            return;
        }
        var combatState = base.Owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        foreach (var (type, upgradeLevel) in _recordedSpells.ToList())
        {
            // 手牌满时 AddGeneratedCardToCombat 自动改道弃牌堆（原版满手语义，牌不消失不消耗）
            var copy = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
                combatState, base.Owner, type, upgradeLevel);
            if (copy == null)
            {
                continue;
            }
            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(copy);
            await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, base.Owner);
        }
        _recordedSpells.Clear();
    }
}
