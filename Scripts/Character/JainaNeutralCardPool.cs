using Godot;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character;

/// <summary>
/// 炉石传说中立卡池 - 吉安娜的衍生牌（Token）专用池。
/// 衍生牌（不稳定的骷髅/狂热者/小精灵等）不进入吉安娜卡池，
/// 统一注册到此中立池，避免出现在吉安娜的掉落与图鉴主分类中。
/// 实现 <see cref="IJainaExcludedFromRandomPool"/>：本池的卡（任务奖励卡
/// 时空扭曲/源生之石/奥术师晨拥与全部衍生牌）不可被发现、不可被随机释放/随机生成
/// （JainaRandomPoolHelper 接口动态收集，无需手动注册）。
/// </summary>
[RegisterSharedCardPool]
public class JainaNeutralCardPool : TypeListCardPoolModel, IJainaExcludedFromRandomPool
{
    public override string Title => "jaina_neutral";

    // 中立池与吉安娜共用能量图标（自定义冰蓝图标）
    public override string EnergyColorName => "jaina";

    public override string? TextEnergyIconPath => "res://assets/energy/energy_jaina_text.png";

    public override string? BigEnergyIconPath => "res://assets/energy/energy_jaina_big.png";

    // 中立池使用灰色调（炉石中立风格）
    public override Color DeckEntryCardColor => new(0.75f, 0.75f, 0.75f);

    public override bool IsColorless => true;

    public override string CardFrameMaterialPath => "card_frame_colorless";
}
