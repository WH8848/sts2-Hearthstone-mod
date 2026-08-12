using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 灌注：每一层灌注都会增加一点英雄技能（火焰冲击）伤害，
/// 以及额外召唤一个 1/1 的小精灵。
/// 挂在吉安娜玩家身上，由灵体采集者等卡施加。
/// </summary>
[RegisterPower]
public sealed class EmpowerPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 灌注层数（由施放火焰冲击的卡读取）
    /// </summary>
    public int EmpowerStacks => (int)Amount;
}
