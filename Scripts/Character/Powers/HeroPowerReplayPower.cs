using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 英雄技能重放（二级火焰冲击光环）：你的所有英雄技能具有[gold]重放1[/gold]——
/// 英雄技能卡打出时自动重放一次（施放两次）。
/// 挂在玩家身上，本局对战持续（与元素吸血光环同款，打出二级火焰冲击时幂等施加）。
/// 覆盖全部英雄技能卡：火焰冲击/二级火焰冲击/奥术爆裂/冰冷触摸
/// （HeroPowerHandHelper.IsHeroPowerCard 判定，按玩家区分，联机不影响队友）。
/// 参考游戏原版 DuplicationPower 的 ModifyCardPlayCount 机制（+1 = 重放一次）；
/// 与 StargazingReplayPower 不同：无目标卡限制、不消耗、不随回合结束移除。
/// </summary>
[RegisterPower]
public sealed class HeroPowerReplayPower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_hero_power_replay_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>
    /// 英雄技能卡打出时施放两次（重放 1）。
    /// 二级火焰冲击自身也是英雄技能 → 同样被重放（打出两次效果，含光环幂等施加）。
    /// </summary>
    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
    {
        if (card?.Owner?.Creature != Owner)
        {
            return playCount;
        }
        if (!HeroPowerHandHelper.IsHeroPowerCard(card))
        {
            return playCount;
        }
        return playCount + 1;
    }
}
