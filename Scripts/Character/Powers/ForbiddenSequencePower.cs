using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
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

    private int _count;

    private long _lastSeq;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => true;

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
            _count++;
            if (_count < Threshold)
            {
                continue;
            }
            // 达到阈值：奖励 = 源生之石直接置入手牌
            var canonical = ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId(typeof(jaina.Scripts.Character.Cards.ForbiddenStoneCard)));
            if (canonical == null)
            {
                return;
            }
            if (jaina.Scripts.Character.JainaHandHelper.IsHandFull(player))
            {
                return;
            }
            var combatState = player.Creature.CombatState;
            var stone = combatState.CreateCard(canonical, player);
            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(stone);
            await CardPileCmd.AddGeneratedCardToCombat(stone, PileType.Hand, player);
            await PowerCmd.Remove(this);
            return;
        }
    }
}
