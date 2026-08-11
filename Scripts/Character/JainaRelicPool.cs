using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character;

/// <summary>
/// 吉安娜的遗物池
/// </summary>
[RegisterSharedRelicPool]
public class JainaRelicPool : TypeListRelicPoolModel
{
    public override string EnergyColorName => "jaina";
}