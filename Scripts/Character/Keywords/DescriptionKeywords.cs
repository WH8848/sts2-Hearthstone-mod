using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;

namespace jaina.Scripts.Character.Keywords;


//添加了一系列用来补充描述的关键词
[RegisterOwnedCardKeyword(nameof(Minion))]
[RegisterOwnedCardKeyword(nameof(SpecialHeroSkill))]
public class DescriptionKeywords
{
public static readonly CardKeyword Minion = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Minion)).GetModCardKeyword();
public static readonly CardKeyword SpecialHeroSkill = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(SpecialHeroSkill)).GetModCardKeyword();
}