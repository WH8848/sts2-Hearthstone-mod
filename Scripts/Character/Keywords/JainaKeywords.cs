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
[RegisterOwnedKeyword(nameof(Replay), IconPath = "res://icon.svg")]
// 英雄技能：英雄技能卡（如火焰冲击）专属关键词，不注入卡面描述，仅提供悬停解释
[RegisterOwnedKeyword(nameof(HeroPower), IconPath = "res://icon.svg")]
// 法术牌：内部标记关键词（不作为关键词展示——不注入卡面描述、不提供悬停解释，
// 仅用于游戏逻辑识别；攻击牌、技能牌和能力牌都视为法术牌）。
// 卡面上通过类型标签"攻击丨法术"等方式展示，见 SpellCardTypePlaquePatch。
[RegisterOwnedKeyword(nameof(Spell), IconPath = "res://icon.svg", IncludeInCardHoverTip = false)]
// 法术派系关键词（自动注入卡面描述之前，悬停显示解释；描述文本中不再手工书写词条）
[RegisterOwnedKeyword(nameof(Fire), IconPath = "res://icon.svg", CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
[RegisterOwnedKeyword(nameof(Frost), IconPath = "res://icon.svg",CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
[RegisterOwnedKeyword(nameof(Arcane), IconPath = "res://icon.svg",CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
[RegisterOwnedKeyword(nameof(Shadow), IconPath = "res://icon.svg",CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
// 任务：任务卡专属关键词（不注入卡面描述，仅提供悬停解释）
[RegisterOwnedKeyword(nameof(Quest), IconPath = "res://icon.svg")]
// 耐久度：拥有耐久度的卡牌，耐久度为0时卡牌被消耗（不注入卡面描述，仅提供悬停解释）
[RegisterOwnedKeyword(nameof(Durability), IconPath = "res://icon.svg")]
// 武器：武器只可装备1把，装备第2把会爆掉第1把（不注入卡面描述，仅提供悬停解释）
[RegisterOwnedKeyword(nameof(Weapon), IconPath = "res://icon.svg")]
// 种族：随从种族（不注入卡面描述，描述文本中以金色词条样式出现，悬停显示解释）
[RegisterOwnedKeyword(nameof(Elemental), IconPath = "res://icon.svg")]
[RegisterOwnedKeyword(nameof(Beast), IconPath = "res://icon.svg")]
[RegisterOwnedKeyword(nameof(Dragon), IconPath = "res://icon.svg")]
[RegisterOwnedKeyword(nameof(Undead), IconPath = "res://icon.svg")]
[RegisterOwnedKeyword(nameof(Demon), IconPath = "res://icon.svg")]
[RegisterOwnedKeyword(nameof(Draenei), IconPath = "res://icon.svg")]
[RegisterOwnedKeyword(nameof(Naga), IconPath = "res://icon.svg")]
// 吸血：造成伤害时回复等量生命（冰霜女巫吉安娜光环，元素随从拥有）
[RegisterOwnedKeyword(nameof(Lifesteal), IconPath = "res://icon.svg")]
// 发现：从若干张随机卡牌中选择一张置入手牌
[RegisterOwnedKeyword(nameof(Discover), IconPath = "res://icon.svg")]
// 免疫：不会受到任何伤害
[RegisterOwnedKeyword(nameof(Immune), IconPath = "res://icon.svg")]
// 地标：占随从槽，每两个回合可点击使用一次触发效果，拥有耐久度（不注入卡面描述，仅提供悬停解释）
[RegisterOwnedKeyword(nameof(Landmark), IconPath = "res://icon.svg")]
// 疲劳：当你抽牌堆和弃牌堆无牌可抽时，抽第1张牌失去1点生命，抽第2张牌失去2点生命，以此类推
[RegisterOwnedKeyword(nameof(Fatigue), IconPath = "res://icon.svg")]
// 微缩：使用（从手牌打出）带微缩的随从牌后，立即将一张0费1/1的复制置入手牌
[RegisterOwnedKeyword(nameof(Miniaturize), IconPath = "res://icon.svg")]
// 微型：微缩效果产生的0费1/1复制卡牌本身（衍生关键词，只能通过微缩获得）
[RegisterOwnedKeyword(nameof(Mini), IconPath = "res://icon.svg")]
// 交易：将此卡牌拖到弃牌堆上方松手会洗入你的弃牌堆，然后你从抽牌堆抽一张牌
[RegisterOwnedKeyword(nameof(Tradeable), IconPath = "res://icon.svg",CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
// 零费标记：旅社谍战洗入的其它角色卡牌内部标记（能量/星星/X 全部费用归零用；
// 不作为关键词展示，仅用于 ZeroCostMarkPatch 识别）
[RegisterOwnedKeyword(nameof(ZeroCostMark), IconPath = "res://icon.svg", IncludeInCardHoverTip = false)]
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
    /// 冻结：被冻结的角色攻击造成的伤害减少 12.5%，可叠加，最大 8 层，回合结束全部消失。
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
    /// 法术牌：内部标记关键词（不显示在卡面/悬停提示中，仅用于游戏逻辑识别；
    /// 攻击牌、技能牌和能力牌都视为法术牌）。
    /// </summary>
    public static readonly CardKeyword Spell = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Spell)).GetModCardKeyword();

    /// <summary>
    /// 战吼：卡牌从手牌中被使用时，会立即触发其效果。
    /// </summary>
    public static readonly CardKeyword Battlecry = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Battlecry)).GetModCardKeyword();

    /// <summary>
    /// 重放：卡牌打出后自动重放一次（同一效果再执行一次）。
    /// </summary>
    public static readonly CardKeyword Replay = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Replay)).GetModCardKeyword();

    /// <summary>
    /// 英雄技能：英雄的专属技能（如火焰冲击），每回合开始自动加入手牌，可无限升级。
    /// </summary>
    public static readonly CardKeyword HeroPower = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(HeroPower)).GetModCardKeyword();

    /// <summary>
    /// 火焰派系
    /// </summary>
    public static readonly CardKeyword Fire = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Fire)).GetModCardKeyword();

    /// <summary>
    /// 冰霜派系
    /// </summary>
    public static readonly CardKeyword Frost = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Frost)).GetModCardKeyword();

    /// <summary>
    /// 奥术派系
    /// </summary>
    public static readonly CardKeyword Arcane = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Arcane)).GetModCardKeyword();

    /// <summary>
    /// 暗影派系
    /// </summary>
    public static readonly CardKeyword Shadow = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Shadow)).GetModCardKeyword();

    /// <summary>
    /// 任务：完成任务可以获得奖励
    /// </summary>
    public static readonly CardKeyword Quest = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Quest)).GetModCardKeyword();

    /// <summary>
    /// 耐久度：耐久度为0时卡牌被消耗
    /// </summary>
    public static readonly CardKeyword Durability = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Durability)).GetModCardKeyword();

    /// <summary>
    /// 武器：武器只可装备1把，装备第2把会爆掉第1把
    /// </summary>
    public static readonly CardKeyword Weapon = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Weapon)).GetModCardKeyword();

    /// <summary>
    /// 元素：随从种族
    /// </summary>
    public static readonly CardKeyword Elemental = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Elemental)).GetModCardKeyword();

    /// <summary>
    /// 野兽：随从种族
    /// </summary>
    public static readonly CardKeyword Beast = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Beast)).GetModCardKeyword();

    /// <summary>
    /// 龙：随从种族
    /// </summary>
    public static readonly CardKeyword Dragon = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Dragon)).GetModCardKeyword();

    /// <summary>
    /// 亡灵：随从种族
    /// </summary>
    public static readonly CardKeyword Undead = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Undead)).GetModCardKeyword();

    /// <summary>
    /// 恶魔：随从种族
    /// </summary>
    public static readonly CardKeyword Demon = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Demon)).GetModCardKeyword();

    /// <summary>
    /// 德莱尼：随从种族
    /// </summary>
    public static readonly CardKeyword Draenei = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Draenei)).GetModCardKeyword();

    /// <summary>
    /// 纳迦：随从种族
    /// </summary>
    public static readonly CardKeyword Naga = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Naga)).GetModCardKeyword();

    /// <summary>
    /// 吸血：造成伤害时回复等量生命
    /// </summary>
    public static readonly CardKeyword Lifesteal = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Lifesteal)).GetModCardKeyword();

    /// <summary>
    /// 发现：从若干张随机卡牌中选择一张置入手牌
    /// </summary>
    public static readonly CardKeyword Discover = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Discover)).GetModCardKeyword();

    /// <summary>
    /// 免疫：不会受到任何伤害
    /// </summary>
    public static readonly CardKeyword Immune = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Immune)).GetModCardKeyword();

    /// <summary>
    /// 地标：占随从槽，每两个回合可点击使用一次触发效果，拥有耐久度
    /// </summary>
    public static readonly CardKeyword Landmark = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Landmark)).GetModCardKeyword();

    /// <summary>
    /// 疲劳：当你抽牌堆和弃牌堆无牌可抽时，抽第1张牌失去1点生命，抽第2张牌失去2点生命，以此类推
    /// </summary>
    public static readonly CardKeyword Fatigue = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Fatigue)).GetModCardKeyword();

    /// <summary>
    /// 微缩：当你使用带有"微缩"的随从牌后，会立即将一张0费1/1的复制置入你的手牌。
    /// 只在从手牌中使用时触发；召唤/复活不触发。
    /// </summary>
    public static readonly CardKeyword Miniaturize = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Miniaturize)).GetModCardKeyword();

    /// <summary>
    /// 微型：通过"微缩"效果产生的0费1/1复制卡牌本身（衍生关键词，只能通过微缩获得），
    /// 完整保留原卡牌的所有文字效果。
    /// </summary>
    public static readonly CardKeyword Mini = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Mini)).GetModCardKeyword();

    /// <summary>
    /// 交易：将此卡牌拖到弃牌堆上方松手会洗入你的弃牌堆，然后你从抽牌堆抽一张牌。
    /// </summary>
    public static readonly CardKeyword Tradeable = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Tradeable)).GetModCardKeyword();

    /// <summary>
    /// 零费标记：旅社谍战洗入的其它角色卡牌内部标记（内部关键词，不显示；
    /// 能量/星星/X 费用全部归零，见 ZeroCostMarkPatch）。
    /// </summary>
    public static readonly CardKeyword ZeroCostMark = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(ZeroCostMark)).GetModCardKeyword();
}
