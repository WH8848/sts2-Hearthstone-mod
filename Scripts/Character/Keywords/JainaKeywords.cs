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
public class JainaKeywords
{
    /// <summary>
    /// 亡语：当随从死亡时触发其效果。
    /// </summary>
    public static readonly CardKeyword Deathrattle = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Deathrattle)).GetModCardKeyword();
}
