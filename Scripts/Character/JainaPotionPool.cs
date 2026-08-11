using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character;

/// <summary>
/// 吉安娜的药水池
/// </summary>
[RegisterSharedPotionPool]
public class JainaPotionPool : TypeListPotionPoolModel
{
    public override string EnergyColorName => "jaina";
}