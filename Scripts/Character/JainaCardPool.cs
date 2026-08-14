using Godot;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Characters;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character;

/// <summary>
/// 吉安娜的卡牌池。
/// 实现 <see cref="IModColorfulPhilosophersCardPool"/>：色彩哲学家事件会把吉安娜的卡池
/// 列为候选（本地化见 events.json 的 COLORFUL_PHILOSOPHERS.pages.INITIAL.options.JAINA）。
/// </summary>
[RegisterSharedCardPool]
public class JainaCardPool : TypeListCardPoolModel, IModColorfulPhilosophersCardPool
{
    public override string Title => "jaina";

    // 吉安娜主题能量图标（自定义冰蓝图标，RitsuLib 接管显示）
    public override string EnergyColorName => "jaina";

    public override string? TextEnergyIconPath => "res://assets/energy/energy_jaina_text.png";

    public override string? BigEnergyIconPath => "res://assets/energy/energy_jaina_big.png";

    // 使用原版储君的能量图标颜色作为参考（冰蓝色调）
    public override Color DeckEntryCardColor => new(0.4f, 0.7f, 1f);

    public override bool IsColorless => false;

    public override string CardFrameMaterialPath => "card_frame_colorless";
}