using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 打开时空之门光环：施放 8 个"你的牌库之外的法术牌"（对局内衍生的攻击/技能牌）
/// 后获得奖励：时空扭曲直接置入手牌，随后本 Power 消失
/// （打出 1 次打开时空之门只能获得 1 次奖励）。
/// 挂在玩家身上，打出打开时空之门时施加。可见（能力图标显示任务进度）。
/// </summary>
[RegisterPower]
public sealed class OpenTimeGatePower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_open_time_gate_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    /// <summary>需要施放的牌库之外法术牌数量</summary>
    private const int RequiredCasts = 8;

    /// <summary>奖励的时空扭曲是否为升级版（时空扭曲+）</summary>
    public bool RewardUpgraded { get; set; }

    private int _count;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => true;

    /// <summary>
    /// 悬停描述：动态显示当前任务进度（已施放 {Count}/8 个牌组之外的法术牌）。
    /// 覆写 Description（smartDescription 非 virtual 无法注入变量）。
    /// </summary>
    public override LocString Description
    {
        get
        {
            var loc = new LocString("powers", base.Id.Entry + ".description");
            loc.Add("Count", _count);
            return loc;
        }
    }

    /// <summary>
    /// 打出此能力后才开始计数：清空已统计的施放次数
    /// （防御性——打出前的施放不应计入任务进度）。
    /// </summary>
    public void StartCountingAfterPlay()
    {
        _count = 0;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner?.Player;
        if (player == null || cardPlay.Card.Owner != player)
        {
            return;
        }
        // 只计数"牌库之外的法术牌"（本局生成过的攻击/技能牌，含实例标记或类型记录），
        // 且必须为玩家手打——不计数随从自动打出的（罗曼斯重放等 AutoPlay 标记的）
        var card = cardPlay.Card;
        if (card.Type != CardType.Attack && card.Type != CardType.Skill)
        {
            return;
        }
        if (!jaina.Scripts.Character.JainaCastTracker.IsOutsideDeckCard(card))
        {
            return;
        }
        if (RommathReplayTracker.IsMarked(card))
        {
            return;
        }
        _count++;
        MegaCrit.Sts2.Core.Logging.Log.Info($"[JainaDebug] OpenTimeGate count={_count}/8 card={card.Id.Entry}");
        if (_count < RequiredCasts)
        {
            return;
        }
        // 达到 8 个：奖励时空扭曲（升级后为时空扭曲+）直接置入手牌；
        // 手牌满时不丢失——排队等待空位（炉石任务奖励语义）
        var canonical = ModelDb.GetByIdOrNull<CardModel>(
            ModelDb.GetId(typeof(jaina.Scripts.Character.Cards.TimeWarpCard)));
        if (canonical == null)
        {
            return;
        }
        var combatState = player.Creature.CombatState;
        var warp = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
            combatState, player, typeof(jaina.Scripts.Character.Cards.TimeWarpCard), RewardUpgraded ? 1 : 0);
        if (warp == null)
        {
            return;
        }
        jaina.Scripts.Character.JainaCastTracker.MarkGenerated(warp);
        MegaCrit.Sts2.Core.Logging.Log.Info("[JainaDebug] OpenTimeGate reward granted");
        await JainaPendingRewardQueue.GrantOrQueue(choiceContext, player, warp);
        await PowerCmd.Remove(this);
    }
}
