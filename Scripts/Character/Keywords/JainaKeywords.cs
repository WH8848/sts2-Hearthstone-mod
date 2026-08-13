using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;

namespace jaina.Scripts.Character.Keywords;

/// <summary>
/// 吉安娜自定义随从关键词。
/// </summary>
// 词条（战吼/亡语/冲锋/冻结/双生法术/灌注/压轴）不注入卡面描述上方，
// 改为直接以金色词条样式出现在描述文本中（见各卡 description）。
// 注册仍保留 Keywords 列表，悬停卡时显示词条解释。
[RegisterOwnedKeyword(nameof(Deathrattle), IconPath = "res://icon.svg")]
[RegisterOwnedKeyword(nameof(Charge), IconPath = "res://icon.svg")]
[RegisterOwnedKeyword(nameof(Freeze), IconPath = "res://icon.svg")]
[RegisterOwnedKeyword(nameof(Twinspell), IconPath = "res://icon.svg")]
[RegisterOwnedKeyword(nameof(Empower), IconPath = "res://icon.svg")]
[RegisterOwnedKeyword(nameof(Finisher), IconPath = "res://icon.svg")]
[RegisterOwnedKeyword(nameof(Battlecry), IconPath = "res://icon.svg")]
// 法术牌：术语关键词（不注入卡面描述，仅提供悬停解释；攻击牌和技能牌都视为法术牌）
[RegisterOwnedKeyword(nameof(Spell), IconPath = "res://icon.svg")]
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

    /// <summary>
    /// 法术牌：攻击牌和技能牌都视为法术牌。
    /// </summary>
    public static readonly CardKeyword Spell = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Spell)).GetModCardKeyword();

    /// <summary>
    /// 战吼：卡牌从手牌中被使用时，会立即触发其效果。
    /// </summary>
    public static readonly CardKeyword Battlecry = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Battlecry)).GetModCardKeyword();
}
