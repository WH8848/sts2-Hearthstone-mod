using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 时空扭曲光环：使玩家在结束当前回合后获得一个额外回合（一次性，Amount 消耗后移除）。
/// 参考原版 AmbergrisPower（ShouldTakeExtraTurn/AfterTakingExtraTurn）。
/// </summary>
[RegisterPower]
public sealed class TimeWarpPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    public override bool ShouldTakeExtraTurn(Player player)
    {
        return Amount > 0 && player == Owner?.Player;
    }

    public override async Task AfterTakingExtraTurn(Player player)
    {
        if (player == Owner?.Player)
        {
            await PowerCmd.Decrement(this);
        }
    }
}
