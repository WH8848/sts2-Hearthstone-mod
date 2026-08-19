using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 禁忌序列光环：每发现一张牌计数 +1，达到阈值（升级前 8 / 升级后 7）后
/// 获得奖励（源生之石直接置入手牌），随后本 Power 消失——
/// 即"打出 1 次禁忌序列只能获得 1 次奖励"（再次打出重新计数）。
/// 挂在玩家身上，打出禁忌序列时施加。可见（能力图标显示任务进度）。
/// </summary>
[RegisterPower]
public sealed class ForbiddenSequencePower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_forbidden_sequence_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    /// <summary>达到该发现次数即获得奖励（升级前 8，升级后 7）</summary>
    public int Threshold { get; set; } = 8;

    /// <summary>
    /// 当前发现进度。
    /// [SavedProperty]：联机状态同步/战斗存档读档会重建 Power 实例，
    /// 普通字段丢失为默认值——进度会退回 0 导致任务线错乱。
    /// </summary>
    [SavedProperty]
    public int Count { get; set; }

    private long _lastSeq;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => true;

    /// <summary>
    /// 悬停描述：动态显示当前任务进度（发现 {Count}/{Threshold} 张）。
    /// 覆写 Description（smartDescription 非 virtual 无法注入变量）。
    /// </summary>
    public override LocString Description
    {
        get
        {
            var loc = new LocString("powers", base.Id.Entry + ".description");
            loc.Add("Count", Count);
            loc.Add("Threshold", Threshold);
            return loc;
        }
    }

    /// <summary>
    /// 打出禁忌序列后才开始计数：把计数起点设为当前最新发现 Seq，
    /// 打出之前已完成的发现不计入任务进度。
    /// </summary>
    public void StartCountingAfterPlay()
    {
        _lastSeq = DiscoverTracker.GetLatestSeq(Owner?.Player);
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner?.Player;
        if (player == null || cardPlay.Card.Owner != player)
        {
            return;
        }
        await ConsumePendingDiscover(choiceContext, player);
    }

    public override async Task AfterPotionUsed(PotionModel potion, Creature? target)
    {
        var player = Owner?.Player;
        if (player == null || potion.Owner != player)
        {
            return;
        }
        // 药水使用钩子无 PlayerChoiceContext：随机目标无玩家交互，用 Throwing 上下文
        await ConsumePendingDiscover(new ThrowingPlayerChoiceContext(), player);
    }

    private async Task ConsumePendingDiscover(PlayerChoiceContext choiceContext, Player player)
    {
        // 循环消费所有未处理发现（广阔智慧一次发现两张 → 两次计数）
        while (DiscoverTracker.TryGetPending(player, _lastSeq) is { } pending)
        {
            _lastSeq = pending.Seq;
            // 随机释放（AutoPlay）触发的发现不计入任务进度（与源生之石同一语义）：
            // 只推进游标不计数——否则符文/匣中古神随机释放发现类卡会错误推进任务
            if (pending.IsAuto)
            {
                continue;
            }
            Count++;
            if (Count < Threshold)
            {
                continue;
            }
            // 达到阈值：奖励 = 源生之石直接置入手牌；
            // 手牌满时不丢失——排队等待空位（炉石任务奖励语义）
            var canonical = ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId(typeof(jaina.Scripts.Character.Cards.ForbiddenStoneCard)));
            if (canonical == null)
            {
                return;
            }
            var combatState = player.Creature.CombatState;
            var stone = combatState.CreateCard(canonical, player);
            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(stone);
            await JainaPendingRewardQueue.GrantOrQueue(choiceContext, player, stone);
            await PowerCmd.Remove(this);
            return;
        }
    }
}
