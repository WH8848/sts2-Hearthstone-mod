using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Utils;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 吉安娜动态卡牌类型注册。
/// 游戏 CardType 为封闭枚举（Attack/Skill/Power/...），
/// 通过 RitsuLib 动态枚举（DynamicEnumValueRegistry）注册稳定的"随从"类型值。
/// 动态值基于 ID 确定性生成，存档/联机可稳定序列化。
/// </summary>
public static class JainaCardTypes
{
    /// <summary>
    /// 随从类型（动态枚举值，需在 <see cref="Entry.Init"/> 中先调用 <see cref="Initialize"/>）
    /// </summary>
    public static CardType Minion { get; private set; }

    /// <summary>
    /// 英雄类型（动态枚举值，英雄卡专用：打出时获得护甲、触发效果、替换英雄技能、更改角色模型）
    /// </summary>
    public static CardType Hero { get; private set; }

    /// <summary>
    /// 注册动态 CardType 值（必须在模型初始化前调用）
    /// </summary>
    public static void Initialize()
    {
        if (Minion != CardType.None)
        {
            return;
        }
        Minion = DynamicEnumValueRegistry<CardType>.RegisterOwned(Entry.ModId, "MINION").Value;
        Hero = DynamicEnumValueRegistry<CardType>.RegisterOwned(Entry.ModId, "HERO").Value;
    }
}
