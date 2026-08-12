using Godot;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character;

/// <summary>
/// 炉石传说中立卡池 - 吉安娜的衍生牌（Token）专用池。
/// 衍生牌（不稳定的骷髅/狂热者/小精灵等）不进入吉安娜卡池，
/// 统一注册到此中立池，避免出现在吉安娜的掉落与图鉴主分类中。
/// </summary>
[RegisterSharedCardPool]
public class JainaNeutralCardPool : TypeListCardPoolModel
{
    public override string Title => "jaina_neutral";

    public override string EnergyColorName => "jaina_neutral";

    // 中立池使用灰色调（炉石中立风格）
    public override Color DeckEntryCardColor => new(0.75f, 0.75f, 0.75f);

    public override bool IsColorless => true;

    public override string CardFrameMaterialPath => "card_frame_colorless";
}
