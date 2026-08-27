using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// "打出记录"兜底钩子（隐藏 Power,挂玩家角色）：
/// <see cref="JainaCastTracker.RecordPlayed"/> 只会被 mod 自己的卡在 OnPlay 首行调用——
/// <b>原版卡</b>(如"熵"Entropy/原版法术/原版随从)打出时不会被记录 →
/// 蓄谋诈骗犯战吼读取"上一张"(LastPlayedCardByPlayer)读不到 → 无法重放,
/// 实测"打出熵后打诈骗犯,战吼什么都没重放"。
/// 本 Power 在 <b>每一张卡打出后</b>(AfterCardPlayed)统一调用 RecordPlayed 兜底:
/// - RecordPlayed 内部幂等(mod 卡已记录→同值覆盖;AutoPlay 卡被 IsMarked 排除;
///   英雄技能卡被排除;非法术卡只记"上一张");
/// - 原版卡从此纳入"上一张"/施放池(倒带/晨拥等的池仍按吉安娜卡池过滤,不受影响)。
/// 联机:两端确定性执行同一钩子;Entry CombatBegan 对每个玩家幂等挂载(同引燃时钟)。
/// </summary>
[RegisterPower]
public sealed class PlayedRecordHookPower : PowerModel
{
    public override MegaCrit.Sts2.Core.Entities.Powers.PowerType Type =>
        MegaCrit.Sts2.Core.Entities.Powers.PowerType.Buff;

    public override MegaCrit.Sts2.Core.Entities.Powers.PowerStackType StackType =>
        MegaCrit.Sts2.Core.Entities.Powers.PowerStackType.Single;

    protected override bool IsVisibleInternal => false;

    /// <summary>
    /// 每张卡打出后:兜底记录(含原版卡——mod 卡双记录幂等)
    /// </summary>
    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        try
        {
            if (cardPlay?.Card != null)
            {
                jaina.Scripts.Character.JainaCastTracker.RecordPlayed(cardPlay.Card);
            }
        }
        catch (System.Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Warn($"[Jaina] PlayedRecordHook record failed: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 幂等挂载(战斗开始对所有玩家调用;已有则不动)
    /// </summary>
    public static async Task EnsureAppliedAsync(PlayerChoiceContext choiceContext, Player player)
    {
        if (player?.Creature == null || player.Creature.CombatState == null ||
            player.Creature.GetPower<PlayedRecordHookPower>() != null)
        {
            return;
        }
        await MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<PlayedRecordHookPower>(
            choiceContext, player.Creature, 1m, player.Creature, null);
    }
}
