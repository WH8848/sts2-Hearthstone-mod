using Godot;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character;

/// <summary>
/// 吉安娜的卡牌池
/// </summary>
[RegisterSharedCardPool]
public class JainaCardPool : TypeListCardPoolModel
{
    public override string Title => "jaina";

    public override string EnergyColorName => "jaina";

    // 使用原版储君的能量图标颜色作为参考（冰蓝色调）
    public override Color DeckEntryCardColor => new(0.4f, 0.7f, 1f);

    public override bool IsColorless => false;

    public override string CardFrameMaterialPath => "card_frame_colorless";
}