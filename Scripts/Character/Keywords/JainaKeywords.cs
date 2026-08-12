using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;

namespace jaina.Scripts.Character.Keywords;

/// <summary>
/// 吉安娜自定义随从关键词。
/// </summary>
[RegisterOwnedKeyword(nameof(Deathrattle), IconPath = "res://icon.svg", CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
[RegisterOwnedKeyword(nameof(Charge), IconPath = "res://icon.svg", CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
[RegisterOwnedKeyword(nameof(Freeze), IconPath = "res://icon.svg", CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
[RegisterOwnedKeyword(nameof(Twinspell), IconPath = "res://icon.svg", CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
[RegisterOwnedKeyword(nameof(Empower), IconPath = "res://icon.svg", CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
[RegisterOwnedKeyword(nameof(Finisher), IconPath = "res://icon.svg", CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
public class JainaKeywords
{
    /// <summary>
    /// 亡语：随从死亡或武器被摧毁时，会触发其效果。
    /// </summary>
    public static readonly CardKeyword Deathrattle = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Deathrattle)).GetModCardKeyword();

    /// <summary>
    /// 冲锋：随从在登场的回合即可立即发起攻击。
    /// </summary>
    public static readonly CardKeyword Charge = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Charge)).GetModCardKeyword();

    /// <summary>
    /// 冻结：被冻结的角色攻击造成的伤害减少 25%，可叠加，最大 4 层，回合结束全部消失。
    /// </summary>
    public static readonly CardKeyword Freeze = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Freeze)).GetModCardKeyword();

    /// <summary>
    /// 双生法术：施放时立即将一张该法术的复制置入你的手牌（复制出的牌不再具有双生法术）。
    /// </summary>
    public static readonly CardKeyword Twinspell = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Twinspell)).GetModCardKeyword();

    /// <summary>
    /// 灌注：每一层灌注都会增加一点英雄技能伤害，以及额外召唤一个 1/1 的小精灵。
    /// </summary>
    public static readonly CardKeyword Empower = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Empower)).GetModCardKeyword();

    /// <summary>
    /// 压轴：使用带有"压轴"的卡牌时，如果刚好消耗完你的能量，就会触发额外效果。
    /// </summary>
    public static readonly CardKeyword Finisher = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Finisher)).GetModCardKeyword();
}
