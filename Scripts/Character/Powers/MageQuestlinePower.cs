using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Cards;
using jaina.Scripts.Character.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 法师任务线光环（风暴城任务线：巫师的计策 → 拖延时间 → 抵达传送大厅）。
/// 任务：施放火焰、冰霜和奥术法术各一个（三个派系各至少一个）。
/// 完成后按阶段发奖并升级下一阶段：
/// - 阶段 1（巫师的计策）：奖励抽一张法术牌 + 拖延时间入手；
/// - 阶段 2（拖延时间）：奖励发现一张火焰/冰霜/奥术派系法术牌 + 抵达传送大厅入手；
/// - 阶段 3（抵达传送大厅）：奖励奥术师晨拥入手。
/// 挂在玩家身上，打出任务卡时施加。可见（能力图标显示任务进度）。
/// </summary>
[RegisterPower]
public sealed class MageQuestlinePower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_mage_questline_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    /// <summary>任务阶段：1=巫师的计策，2=拖延时间，3=抵达传送大厅</summary>
    public int Stage { get; set; } = 1;

    /// <summary>
    /// 本任务卡是否升级（+）：升级后的任务卡完成任务时，
    /// 奖励的是下一阶段的升级版（拖延时间+ / 抵达传送大厅+ / 奥术师晨拥+）。
    /// </summary>
    public bool RewardUpgraded { get; set; }

    private readonly HashSet<JainaSpellSchool> _schools = [];

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => true;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner?.Player;
        if (player == null || cardPlay.Card.Owner != player)
        {
            return;
        }
        var card = cardPlay.Card;
        // 只统计法术牌：攻击/技能牌，或带"法术牌"关键词的能力牌
        // （寒冰屏障/冰血哨塔/任务线卡视为法术牌，施放计入任务进度）
        if (card.Type != CardType.Attack && card.Type != CardType.Skill &&
            !card.Keywords.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Spell))
        {
            return;
        }
        // 按卡关键词判断派系（各法术牌 CanonicalKeywords 带火焰/冰霜/奥术）
        var schools = card.Keywords;
        if (schools.Contains(JainaKeywords.Fire))
        {
            _schools.Add(JainaSpellSchool.Fire);
        }
        if (schools.Contains(JainaKeywords.Frost))
        {
            _schools.Add(JainaSpellSchool.Frost);
        }
        if (schools.Contains(JainaKeywords.Arcane))
        {
            _schools.Add(JainaSpellSchool.Arcane);
        }
        MegaCrit.Sts2.Core.Logging.Log.Info($"[JainaDebug] MageQuestline stage={Stage} upgraded={RewardUpgraded} schools={_schools.Count}/3 card={card.Id.Entry}");
        if (_schools.Count < 3)
        {
            return;
        }
        // 三派系集齐：完成任务
        int upgradeLevel = RewardUpgraded ? 1 : 0;
        switch (Stage)
        {
            case 1:
                // 奖励：抽一张法术牌（从抽牌堆中找一张攻击/技能牌入手；没有则普通抽一张）
                await GrantDrawSpell(choiceContext, player);
                await GrantCardToHand(choiceContext, player, typeof(StallingCard), upgradeLevel, markGenerated: true);
                break;
            case 2:
                // 奖励：发现上述派系中的一张法术牌
                await jaina.Scripts.Character.Cards.JainaDiscoverHelper.DiscoverAndAddToHand(choiceContext, player);
                await GrantCardToHand(choiceContext, player, typeof(ReachPortalChamberCard), upgradeLevel, markGenerated: true);
                break;
            case 3:
                // 奖励：奥术师晨拥（升级任务卡奖励晨拥+）
                await GrantCardToHand(choiceContext, player, typeof(DawngraspCard), upgradeLevel, markGenerated: true);
                break;
        }
        MegaCrit.Sts2.Core.Logging.Log.Info("[JainaDebug] MageQuestline reward granted");
        await PowerCmd.Remove(this);
    }

    /// <summary>
    /// 获取任务线悬停提示用卡：升级的任务卡（+）悬停时显示升级版衍生卡（+）。
    /// 未升级返回 canonical 实例；升级返回 MutableClone + UpgradeInternal 的克隆。
    /// </summary>
    public static CardModel GetQuestlineHoverCard<T>(bool upgraded) where T : CardModel
    {
        var canonical = ModelDb.Card<T>();
        if (!upgraded)
        {
            return canonical;
        }
        var clone = (CardModel)canonical.MutableClone();
        clone.UpgradeInternal();
        return clone;
    }

    /// <summary>
    /// 抽一张法术牌：从抽牌堆中找第一张攻击/技能牌（或带"法术牌"关键词的能力牌）
    /// 置入手牌；抽牌堆中没有则普通抽一张。
    /// </summary>
    private static async Task GrantDrawSpell(PlayerChoiceContext choiceContext, Player player)
    {
        var drawPile = player.PlayerCombatState?.DrawPile;
        var spell = drawPile?.Cards.FirstOrDefault(c =>
            c != null && (c.Type == CardType.Attack || c.Type == CardType.Skill ||
                          c.Keywords.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Spell)));
        if (spell == null)
        {
            await CardPileCmd.Draw(choiceContext, 1, player);
            return;
        }
        if (jaina.Scripts.Character.JainaHandHelper.IsHandFull(player))
        {
            return;
        }
        await CardPileCmd.Add(spell, PileType.Hand);
    }

    /// <summary>
    /// 将指定类型卡的实例置入手牌（下一阶段任务卡 / 奥术师晨拥），
    /// 按 upgradeLevel 恢复升级形态（1 = 升级版 +）。
    /// </summary>
    private static async Task GrantCardToHand(PlayerChoiceContext choiceContext, Player player, System.Type cardType, int upgradeLevel, bool markGenerated)
    {
        var canonical = ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId(cardType));
        if (canonical == null)
        {
            return;
        }
        if (jaina.Scripts.Character.JainaHandHelper.IsHandFull(player))
        {
            return;
        }
        var combatState = player.Creature.CombatState;
        var card = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
            combatState, player, cardType, upgradeLevel);
        if (card == null)
        {
            return;
        }
        if (markGenerated)
        {
            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(card);
        }
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
    }
}
