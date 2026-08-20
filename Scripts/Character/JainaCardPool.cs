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

    // 吉安娜主题卡框颜色：#69CCF0（RGB 105, 204, 240，冰蓝）
    public override Color DeckEntryCardColor => new("#69CCF0");

    public override bool IsColorless => false;

    public override string CardFrameMaterialPath => "card_frame_colorless";

    /// <summary>
    /// 吉安娜主题卡框材质：HSV 调色（h=0.542 蓝、s=0.56、v=0.94——冰蓝主题）。
    /// RitsuLib 的 MaterialUtils.CreateHsvShaderMaterial 加载 res://shaders/hsv.gdshader
    /// （游戏原版 shader；本 mod 的 shaders/hsv.gdshader 提供同路径兜底）。
    /// </summary>
    private static readonly Material? _poolFrameMaterial =
        STS2RitsuLib.Utils.MaterialUtils.CreateHsvShaderMaterial(0.542f, 0.56f, 0.94f);

    public override Material? PoolFrameMaterial => _poolFrameMaterial;
}