using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using MinionLib.Minion;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 吉安娜随从基类 - 真正的生物单位。
/// 属性：攻击力（上方数字）、生命值（下方数字）。
/// 不显示血条和意图，视觉使用闪电充能球模型，固定在玩家身边。
/// 两种行为模式（<see cref="JainaMinionBehaviorMode"/>）：
/// - 手动模式（默认）：随从永不自动行动，一切行动靠玩家点击随从触发（行动点制）。
/// - 自动模式：玩家回合结束时自动攻击随机敌人，并执行各随从独有被动。
/// </summary>
public abstract class JainaMinionBase : MinionModel, IModCreatureVisualsFactory
{
    /// <summary>
    /// 随从基础攻击力（通过 MinionSummonOptions.PrimaryStatAmount 传入实际值）
    /// </summary>
    public int BaseAttackValue = 0;

    /// <summary>
    /// 召唤时的回合数（用于"召唤当回合不可攻击"规则）
    /// </summary>
    private int _summonedTurn = -1;

    /// <summary>
    /// 手动模式：本回合剩余可点击攻击次数由 JainaAttackAction 的 Amount 唯一维护
    /// （MinionLib DecrementAfterAct → PowerCmd.Decrement 自动递减并刷新意图）
    /// </summary>
    protected bool _hasAttackedThisTurn;

    /// <summary>
    /// 随从行为模式（默认手动：不自动行动，点击驱动）
    /// </summary>
    public virtual JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    /// <summary>
    /// 手动模式下每回合可点击攻击的次数（默认 1 次）
    /// </summary>
    public virtual int ActionsPerTurn => 1;

    /// <summary>
    /// 冲锋：召唤当回合即可点击攻击（召唤时立即授予行动点，炉石语义）。
    /// 非冲锋随从召唤当回合不可攻击。
    /// </summary>
    public virtual bool HasCharge => false;

    /// <summary>
    /// 随从战斗视觉：使用各随从自己的卡图原画场景（不再用闪电充能球模型）
    /// </summary>
    protected override string VisualsPath => MinionVisualsPath;

    /// <summary>
    /// 各随从的卡图视觉资源路径
    /// </summary>
    protected abstract string MinionVisualsPath { get; }

    /// <summary>
    /// RitsuLib 运行时视觉工厂：直接用代码构建 NCreatureVisuals 节点。
    /// 绕开 tscn 场景导出（Godot 导出器会丢弃无法解析的 script 引用，
    /// 导致 pck 内场景退化为纯 Node2D 而 InvalidCastException）。
    /// 结构对齐原 assets/minion_visuals/*.tscn：%Visuals/%Bounds/%CenterPos/%IntentPos
    /// 必须是 root 的直接子节点（游戏用相对路径 GetNode("IntentPos") 等查找，
    /// 不能嵌套容器）。缩放仅作用于 Sprite2D 自身（场上显示为卡图一半大小），
    /// Bounds/IntentPos 保持原尺寸供布局/意图定位使用。
    /// </summary>
    public NCreatureVisuals? TryCreateCreatureVisuals()
    {
        var root = new NCreatureVisuals();
        _visualsRoot = root;

        // 卡图显示缩小为一半（仅缩放 Sprite2D，不嵌套容器）
        var texture = ResourceLoader.Load<Texture2D>(MinionVisualsPath);
        var sprite = new Sprite2D { Name = "Visuals", Texture = texture, Scale = new Vector2(0.5f, 0.5f) };
        root.AddChild(sprite);
        sprite.UniqueNameInOwner = true;
        sprite.Owner = root;

        var bounds = new Control
        {
            Name = "Bounds",
            AnchorRight = 1f,
            AnchorBottom = 1f,
            OffsetLeft = -250f,
            OffsetTop = -190f,
            OffsetRight = 250f,
            OffsetBottom = 190f,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        root.AddChild(bounds);
        bounds.UniqueNameInOwner = true;
        bounds.Owner = root;

        var center = new Marker2D { Name = "CenterPos" };
        root.AddChild(center);
        center.UniqueNameInOwner = true;
        center.Owner = root;

        var intent = new Marker2D { Name = "IntentPos", Position = new Vector2(0f, -235f) };
        root.AddChild(intent);
        intent.UniqueNameInOwner = true;
        intent.Owner = root;

        // 悬停交互区（覆盖缩小后的显示区域 ±125×±95）：悬停显示随从卡完整卡面（炉石式）
        var hoverArea = new Control
        {
            Name = "HoverArea",
            OffsetLeft = -125f,
            OffsetTop = -95f,
            OffsetRight = 125f,
            OffsetBottom = 95f,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        root.AddChild(hoverArea);

        hoverArea.MouseEntered += () =>
        {
            if (!Creature.IsAlive)
            {
                return;
            }
            bool showOnLeft = false;
            try
            {
                var screenX = _visualsRoot?.GetGlobalTransformWithCanvas().Origin.X ?? 0f;
                var viewportWidth = _visualsRoot?.GetViewport().GetVisibleRect().Size.X ?? 1920f;
                showOnLeft = screenX > viewportWidth / 2f;
            }
            catch
            {
            }
            ShowMinionCard(showOnLeft);
        };
        hoverArea.MouseExited += HideMinionCard;

        return root;
    }

    /// <summary>
    /// 悬停时显示的随从卡卡面节点（游戏官方 NCard 渲染，炉石式完整卡面）
    /// </summary>
    private Godot.Node? _hoverCardNode;

    /// <summary>
    /// 悬停附加节点（左侧衍生物卡面 / 右侧注释标签），随主卡一起清理
    /// </summary>
    private readonly List<Godot.Node> _hoverExtraNodes = [];

    /// <summary>
    /// 视觉根节点（TryCreateCreatureVisuals 创建；悬停卡面的回退挂载点）
    /// </summary>
    private CanvasItem? _visualsRoot;

    /// <summary>
    /// 是否已连接游戏原生悬停层（NCreature.Hitbox）
    /// </summary>
    private bool _hoverConnected;

    /// <summary>
    /// 显示随从卡的完整卡面（NCard：卡框/费用/卡图/名称/描述/关键词），
    /// 显示在随从左侧或右侧（showOnLeft=true 左侧）。
    /// </summary>
    private void ShowMinionCard(bool showOnLeft)
    {
        try
        {
            if (_hoverCardNode != null)
            {
                return;
            }
            var cardType = JainaMinionCardMap.GetCardType(GetType());
            if (cardType == null)
            {
                MegaCrit.Sts2.Core.Logging.Log.Info("[JainaHover] cardType null");
                return;
            }
            var canonical = MegaCrit.Sts2.Core.Models.ModelDb.GetByIdOrNull<MegaCrit.Sts2.Core.Models.CardModel>(
                MegaCrit.Sts2.Core.Models.ModelDb.GetId(cardType));
            if (canonical == null)
            {
                MegaCrit.Sts2.Core.Logging.Log.Info("[JainaHover] canonical null");
                return;
            }
            var cardNode = MegaCrit.Sts2.Core.Nodes.Cards.NCard.Create(canonical);
            if (cardNode == null)
            {
                MegaCrit.Sts2.Core.Logging.Log.Info("[JainaHover] NCard.Create null (TestMode?)");
                return;
            }
            // 挂到战斗房间根（所有随从之上，避免多随从时卡面被其他随从视觉遮挡"图层忽高忽低"）；
            // 战斗房间不可用时回退到随从节点，再回退到视觉根
            var room = MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom.Instance;
            var creatureNode = room?.GetCreatureNode(Creature);
            var host = (CanvasItem?)(room ?? (Godot.Node?)creatureNode) ?? _visualsRoot;
            if (host == null)
            {
                MegaCrit.Sts2.Core.Logging.Log.Warn("[JainaHover] no host for hover card");
                return;
            }
            cardNode.UpdateVisuals(MegaCrit.Sts2.Core.Entities.Cards.PileType.None,
                MegaCrit.Sts2.Core.Entities.Cards.CardPreviewMode.Normal);
            cardNode.MouseFilter = Control.MouseFilterEnum.Ignore;
            host.AddChild(cardNode);
            // 卡面放随从旁边（横跨 ±90 起，卡面宽约 112 缩放后；稍抬高对齐卡图区域）
            cardNode.Scale = Vector2.One * 0.72f;
            // 以随从节点的全局位置为锚点计算卡面全局位置，再转成 host 局部坐标
            // （host 为战斗房间根时，卡面坐标是房间坐标而非随从局部坐标）
            Vector2 anchor = Vector2.Zero;
            try
            {
                anchor = ((CanvasItem?)creatureNode ?? _visualsRoot)?.GetGlobalTransformWithCanvas().Origin ?? Vector2.Zero;
            }
            catch
            {
            }
            var hoverCardSize = cardNode.Size * cardNode.Scale;
            var targetGlobal = showOnLeft
                ? anchor + new Vector2(-hoverCardSize.X - 100f, -190f)
                : anchor + new Vector2(100f, -190f);
            cardNode.Position = host.GetGlobalTransformWithCanvas().AffineInverse() * targetGlobal;
            cardNode.ZIndex = 500;
            // 视口约束：卡面必须完整出现在屏幕内（含 8px 边距），超出部分平移到视口内
            ClampCardToViewport(host, cardNode);
            MegaCrit.Sts2.Core.Logging.Log.Info($"[JainaHover] card shown: {canonical.Id} host={(host == _visualsRoot ? "visuals-root" : "creature-node")} insideTree={cardNode.IsInsideTree()} ready={cardNode.IsNodeReady()}");
            _hoverCardNode = cardNode;
            // 附加悬停内容：主卡左侧衍生物卡面 + 右侧注释（主卡 AdditionalHoverTips）
            ShowExtraHoverContent(host, cardNode, canonical);
        }
        catch (System.Exception ex)
        {
            // 悬停卡面失败不影响随从视觉/战斗
            MegaCrit.Sts2.Core.Logging.Log.Warn($"[JainaHover] error: {ex}");
        }
    }

    /// <summary>
    /// 悬停附加内容：主卡<b>左侧</b>显示衍生物卡面（AdditionalHoverTips 中的 CardHoverTip），
    /// 主卡<b>右侧</b>显示注释文本（其余 HoverTip：关键词解释等）。
    /// </summary>
    private void ShowExtraHoverContent(CanvasItem host, Control mainCard, CardModel canonical)
    {
        try
        {
            var tips = canonical.HoverTips?.ToList() ?? [];
            if (tips.Count == 0)
            {
                return;
            }
            var canvasTransform = host.GetGlobalTransformWithCanvas();
            var mainPos = canvasTransform * mainCard.Position;
            var mainSize = mainCard.Size * mainCard.Scale;

            // 主卡左侧：衍生物卡面（CardHoverTip，每张再向左错开）
            float leftX = mainPos.X - 10f;
            foreach (var tip in tips)
            {
                if (tip is MegaCrit.Sts2.Core.HoverTips.CardHoverTip cardTip && cardTip.Card != null)
                {
                    var extraCard = MegaCrit.Sts2.Core.Nodes.Cards.NCard.Create(cardTip.Card);
                    if (extraCard == null)
                    {
                        continue;
                    }
                    extraCard.UpdateVisuals(MegaCrit.Sts2.Core.Entities.Cards.PileType.None,
                        MegaCrit.Sts2.Core.Entities.Cards.CardPreviewMode.Normal);
                    extraCard.MouseFilter = Control.MouseFilterEnum.Ignore;
                    host.AddChild(extraCard);
                    extraCard.Scale = Vector2.One * 0.72f;
                    leftX -= extraCard.Size.X * extraCard.Scale.X + 10f;
                    extraCard.Position = canvasTransform.AffineInverse() * new Vector2(leftX, mainPos.Y);
                    extraCard.ZIndex = 499;
                    ClampCardToViewport(host, extraCard);
                    _hoverExtraNodes.Add(extraCard);
                }
            }

            // 主卡右侧：注释文本（HoverTip：标题 + 描述，合并为一个标签）
            var notes = tips.OfType<MegaCrit.Sts2.Core.HoverTips.HoverTip>()
                .Select(h => h.Title + "\n" + h.Description)
                .ToList();
            if (notes.Count > 0)
            {
                var label = new Godot.Label
                {
                    Text = string.Join("\n\n", notes),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    ZIndex = 501,
                };
                host.AddChild(label);
                label.Position = canvasTransform.AffineInverse() *
                                 new Vector2(mainPos.X + mainSize.X + 10f, mainPos.Y);
                _hoverExtraNodes.Add(label);
            }
        }
        catch (System.Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Warn($"[JainaHover] extra hover content error: {ex}");
        }
    }

    /// <summary>
    /// 隐藏随从卡卡面。
    /// 场景切换（回主菜单/结束战斗）时父节点可能正在增删子节点，
    /// 此时 QueueFreeSafely 内的同步 RemoveChild 会报
    /// "Parent node is busy adding/removing children" —— 延迟到帧末安全清理。
    /// 注意：NCard 是对象池化的（NCard.Create 走 NodePool），必须先恢复本方法
    /// 对卡节点设置过的所有值（ZIndex=500/Scale=0.72/Position），否则节点回池后
    /// 残留状态会污染复用的卡——正是"图层忽高忽低/别的牌被改了"的根因。
    /// </summary>
    private void HideMinionCard()
    {
        var node = _hoverCardNode;
        _hoverCardNode = null;
        // 清理附加节点（衍生物卡面同样要恢复池化 NCard 状态）
        foreach (var extra in _hoverExtraNodes)
        {
            if (extra == null || !GodotObject.IsInstanceValid(extra))
            {
                continue;
            }
            if (extra is Control extraControl)
            {
                extraControl.ZIndex = 0;
                extraControl.Scale = Vector2.One;
                extraControl.Position = Vector2.Zero;
            }
            var captured = extra;
            Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(captured))
                {
                    captured.QueueFreeSafely();
                }
            }).CallDeferred();
        }
        _hoverExtraNodes.Clear();
        if (node == null || !GodotObject.IsInstanceValid(node))
        {
            return;
        }
        // 恢复对池化 NCard 的修改（ShowMinionCard 设置过：ZIndex=500/Scale=0.72/Position）
        if (node is Control cardControl)
        {
            cardControl.ZIndex = 0;
            cardControl.Scale = Vector2.One;
            cardControl.Position = Vector2.Zero;
        }
        // 延迟清理：等当前帧的节点增删完成后移除，避免 busy 报错
        Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(node))
            {
                node.QueueFreeSafely();
            }
        }).CallDeferred();
    }

    /// <summary>
    /// 视口约束：把悬停卡面的全局位置平移到视口内（8px 边距），保证卡面完整可见。
    /// </summary>
    private static void ClampCardToViewport(CanvasItem host, Control cardNode)
    {
        try
        {
            var canvasTransform = host.GetGlobalTransformWithCanvas();
            var viewport = host.GetViewport();
            if (viewport == null)
            {
                return;
            }
            var vpRect = viewport.GetVisibleRect();
            // 卡面缩放后的实际尺寸（Control 的 Size 为布局尺寸，Scale 缩放绘制）
            var cardSize = cardNode.Size * cardNode.Scale;
            // 卡面当前全局左上角（host 局部坐标 → 全局）
            var globalTopLeft = canvasTransform * cardNode.Position;
            const float margin = 8f;

            var x = globalTopLeft.X;
            var y = globalTopLeft.Y;
            // 约束：左上角不小于视口+边距，右下角不超出视口-边距
            x = Mathf.Max(x, vpRect.Position.X + margin);
            y = Mathf.Max(y, vpRect.Position.Y + margin);
            x = Mathf.Min(x, vpRect.End.X - margin - cardSize.X);
            y = Mathf.Min(y, vpRect.End.Y - margin - cardSize.Y);
            // 卡面大于视口时避免负位置（居中）
            if (cardSize.X > vpRect.Size.X - margin * 2f)
            {
                x = vpRect.Position.X + (vpRect.Size.X - cardSize.X) / 2f;
            }
            if (cardSize.Y > vpRect.Size.Y - margin * 2f)
            {
                y = vpRect.Position.Y + (vpRect.Size.Y - cardSize.Y) / 2f;
            }

            // 转回 host 局部坐标
            cardNode.Position = canvasTransform.AffineInverse() * new Vector2(x, y);
        }
        catch
        {
            // 约束失败保持原位置
        }
    }

    /// <summary>
    /// 显示血条（存活时），与奥斯提一致：主人与联机队友都可以查看随从生命值。
    /// （原设计隐藏血条，联机时队友看不到随从生命值）
    /// </summary>
    public override bool IsHealthBarVisible => Creature.IsAlive;

    /// <summary>
    /// 血条视觉缩短一半：MinionLib 强制随从可交互使血条显示，
    /// 默认血条宽度 = Bounds(250) + 24，这里缩减 137 使血条约为原来一半。
    /// </summary>
    public override float HpBarSizeReduction => 137f;

    /// <summary>
    /// 随从不显示在怪物图鉴中
    /// </summary>
    public override bool ShouldShowInCompendium => false;

    /// <summary>
    /// 随从死亡后从战斗场景移除（生命值为零时消失）
    /// </summary>
    public override bool ShouldDisappearFromDoom => true;

    /// <summary>
    /// 随从意图与行动状态机。
    /// 两种模式都使用<see cref="JainaConditionalAttackIntent"/>动态意图：
    /// 随从可以攻击时显示攻击意图（等同于攻击力），
    /// 攻击过后或不可攻击时（召唤当回合、攻击力为 0、行动点耗尽）意图消失。
    /// </summary>
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        // 攻击意图随"当前是否可攻击"动态显示/隐藏（继承 SingleAttackIntent，
        // 显示攻击力数值标签）
        var intent = new JainaConditionalAttackIntent(
            () => BaseAttackValue,
            CanShowAttackIntent);

        // 手动模式：IDLE 状态机（不自动行动），意图由行动点驱动
        if (BehaviorMode == JainaMinionBehaviorMode.Manual)
        {
            var idle = new MoveState(
                "MINION_IDLE",
                _ => Task.CompletedTask,
                intent)
            {
                FollowUpState = null
            };
            idle.FollowUpState = idle; // 循环自身
            return new MonsterMoveStateMachine([idle], idle);
        }

        // 自动模式：延迟读取 BaseAttackValue，确保召唤时的攻击设定生效
        var attackMove = new MoveState(
            "MINION_ATTACK",
            async targets =>
            {
                var target = targets.FirstOrDefault();
                if (target == null || !Creature.IsAlive) return;
                // Move 标记：触发荆棘反伤与振翅（Flutter）层数减少（IsPoweredAttack）
                await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), [target], BaseAttackValue, ValueProp.Move, Creature);
            },
            intent)
        {
            FollowUpState = null
        };
        attackMove.FollowUpState = attackMove; // 循环自身，意图恒定

        return new MonsterMoveStateMachine([attackMove], attackMove);
    }

    /// <summary>
    /// 当前是否可以向敌人显示攻击意图（= 是否还能发动攻击）：
    /// - 存活且攻击力 &gt; 0；
    /// - 非召唤当回合（召唤当回合不可攻击）；
    /// - 手动模式：本回合还有剩余行动点（JainaAttackAction.Amount 为唯一事实源）；
    /// - 自动模式：本回合尚未攻击过。
    /// </summary>
    public bool CanShowAttackIntent()
    {
        if (!Creature.IsAlive || BaseAttackValue <= 0)
        {
            return false;
        }
        // 召唤当回合不可攻击（冲锋随从除外：召唤当回合即可攻击）
        if (!HasCharge && IsSummonedThisTurn())
        {
            return false;
        }
        if (BehaviorMode == JainaMinionBehaviorMode.Manual)
        {
            return (Creature.GetPower<JainaAttackAction>()?.Amount ?? 0m) > 0m;
        }
        return !_hasAttackedThisTurn;
    }

    /// <summary>
    /// 立即刷新随从的意图显示（游戏原生意图揭示流程也会刷新，
    /// 但攻击后/回合开始时主动刷新可保证意图即时出现或消失）。
    /// </summary>
    public void RefreshIntentDisplay()
    {
        try
        {
            var node = NCombatRoom.Instance?.GetCreatureNode(Creature);
            if (node != null)
            {
                _ = node.RefreshIntents();
            }
        }
        catch
        {
            // 战斗 UI 未就绪时忽略，下一轮揭示流程会自然刷新
        }
    }

    /// <summary>
    /// 被召唤时初始化：设置生命/攻击，应用随从副单位标记（不触发击杀胜利结算）
    /// </summary>
    public override async Task OnSummon(PlayerChoiceContext choiceContext, Player owner, MinionSummonOptions options)
    {
        if (options.MaxHp is decimal maxHp)
        {
            await CreatureCmd.SetMaxAndCurrentHp(Creature, maxHp);
        }
        if (options.PrimaryStatAmount is decimal attack && attack > 0m)
        {
            BaseAttackValue = (int)attack;
        }

        // 记录召唤回合（用于"召唤当回合不可攻击"规则，冲锋除外）
        _summonedTurn = Creature.PetOwner?.PlayerCombatState?.TurnNumber ?? -1;

        // 标记为随从副单位（不触发击杀胜利结算、死亡不触发致命等）
        await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.MinionPower>(
            choiceContext, Creature, 1m, owner.Creature, options.Source);

        // 意图自举：玩家侧随从不会被游戏 RollMove（0.111.1 仅对 Enemy 调用，
        // CombatManager.AfterCreatureAdded 只处理 IsEnemy；CreatureCmd.Add 对非敌人
        // 显式 rollNewMove:false），Monster.NextMove 默认是空的 UNSET_MOVE，
        // 意图 UI 会读 NextMove.Intents 导致条件意图静默消失。
        // 这里主动 RollMove + RefreshIntents 填充 NextMove（含条件意图实例）。
        try
        {
            var opponents = Creature.CombatState?.GetOpponentsOf(Creature);
            if (opponents != null)
            {
                Creature.PrepareForNextTurn(opponents);
            }
        }
        catch
        {
            // 战斗场景未就绪时忽略，回合开始揭示流程会再次刷新
        }

        // 战吼只在"从手牌打出随从卡"时触发（炉石规则：随机召唤/效果召唤不触发战吼）。
        // 判断依据：只有 JainaMinionCardTemplate.OnPlay 召唤时 Source 传的是随从卡实例。
        if (options.Source is jaina.Scripts.Character.Cards.JainaMinionCardTemplate)
        {
            await OnBattlecry(choiceContext);
        }

        // 随从军势：吉安娜护甲无法阻挡的伤害由随从按召唤顺序抵挡（吉安娜固有机制，与遗物无关）。
        // 在第一个随从召唤时挂到吉安娜身上（幂等）。
        var petOwner = Creature.PetOwner;
        if (petOwner != null && !petOwner.Creature.Powers.Any(p => p is Powers.MinionSquadPower))
        {
            await PowerCmd.Apply<Powers.MinionSquadPower>(
                choiceContext, [petOwner.Creature], 1m, petOwner.Creature, null);
        }

        // 冲锋：召唤当回合立即授予行动点（可点击攻击；行动点回合末自动移除，下回合正常授予）
        if (HasCharge && BehaviorMode == JainaMinionBehaviorMode.Manual)
        {
            var applier = Creature.PetOwner?.Creature ?? Creature;
            await PowerCmd.Apply<JainaAttackAction>(choiceContext, Creature, ActionsPerTurn, applier, null);
            RefreshIntentDisplay();
        }

        // 悬停卡面：连接游戏原生悬停层（NCreature.Hitbox）——视觉根节点的 Control
        // MouseEntered 被游戏层级拦截不触发，Hitbox 是游戏自己悬停检测用的层
        if (!_hoverConnected)
        {
            try
            {
                var creatureNode = NCombatRoom.Instance?.GetCreatureNode(Creature);
                if (creatureNode?.Hitbox != null)
                {
                    creatureNode.Hitbox.MouseEntered += OnMinionHoverEnter;
                    creatureNode.Hitbox.MouseExited += OnMinionHoverExit;
                    _hoverConnected = true;
                    MegaCrit.Sts2.Core.Logging.Log.Info("[JainaHover] hitbox hover connected");
                }
                else
                {
                    MegaCrit.Sts2.Core.Logging.Log.Warn("[JainaHover] creature node not ready for hover connect");
                }
            }
            catch (System.Exception ex)
            {
                MegaCrit.Sts2.Core.Logging.Log.Warn($"[JainaHover] hover connect error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 悬停进入（Hitbox）：显示随从卡卡面
    /// </summary>
    private void OnMinionHoverEnter()
    {
        if (!Creature.IsAlive)
        {
            return;
        }
        bool showOnLeft = false;
        try
        {
            var node = NCombatRoom.Instance?.GetCreatureNode(Creature);
            if (node != null)
            {
                var screenX = node.GetGlobalTransformWithCanvas().Origin.X;
                var viewportWidth = node.GetViewport().GetVisibleRect().Size.X;
                showOnLeft = screenX > viewportWidth / 2f;
            }
        }
        catch
        {
        }
        ShowMinionCard(showOnLeft);
    }

    /// <summary>
    /// 悬停退出（Hitbox）：隐藏随从卡卡面
    /// </summary>
    private void OnMinionHoverExit() => HideMinionCard();

    /// <summary>
    /// 战吼效果：随从从手牌打出时触发（随机召唤/效果召唤不触发）。子类重写。
    /// </summary>
    public virtual Task OnBattlecry(PlayerChoiceContext choiceContext) => Task.CompletedTask;

    /// <summary>
    /// 玩家回合开始时：
    /// 手动模式授予本回合的点击攻击行动点，自动模式重置"本回合已攻击"标记；
    /// 随后刷新意图显示（可攻击时出现攻击意图）。
    /// （自动模式无需授予行动点，随从会自行攻击。）
    /// </summary>
    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Player || !Creature.IsAlive)
        {
            return;
        }
        // 召唤当回合不可攻击（行动点从下一回合开始授予）
        if (IsSummonedThisTurn())
        {
            return;
        }

        if (BehaviorMode == JainaMinionBehaviorMode.Manual)
        {
            // 行动点由随从主人施加，Amount = 本回合可点击攻击次数（唯一事实源）
            var applier = Creature.PetOwner?.Creature ?? Creature;
            await PowerCmd.Apply<JainaAttackAction>(choiceContext, Creature, ActionsPerTurn, applier, null);
        }
        else
        {
            // 自动模式：新回合可以再次攻击，意图恢复显示
            _hasAttackedThisTurn = false;
        }

        RefreshIntentDisplay();
    }

    /// <summary>
    /// 玩家回合结束时：
    /// 自动模式 - 攻击力 > 0 的随从对随机可命中敌人造成攻击力点伤害；
    /// 手动模式 - 随从不自动行动（靠玩家点击）；
    /// 两种模式都会执行随从独有回合结束被动。
    /// </summary>
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player)
        {
            return;
        }
        if (!Creature.IsAlive)
        {
            return;
        }
        // 灾厄：生命 ≤ 灾厄层数 → 回合结束死亡。
        // 游戏 DoomPower 只判定 GetCreaturesOnSide（不含随从 Pets），随从受灾厄不会死亡，
        // 这里补上随从的灾厄判定（DoomKill 是 public 静态方法）。
        var doom = Creature.GetPower<MegaCrit.Sts2.Core.Models.Powers.DoomPower>();
        if (doom != null && Creature.CurrentHp <= doom.Amount)
        {
            await MegaCrit.Sts2.Core.Models.Powers.DoomPower.DoomKill([Creature]);
            return;
        }
        // 召唤当回合：不可以攻击，但随从独有回合结束被动照常触发
        if (IsSummonedThisTurn())
        {
            await PerformTurnEndPassive(choiceContext);
            return;
        }

        if (BehaviorMode == JainaMinionBehaviorMode.Auto)
        {
            await PerformTurnEndAttack(choiceContext);
        }
        await PerformTurnEndPassive(choiceContext);
    }

    /// <summary>
    /// 是否为召唤当回合（召唤后第一回合内）
    /// </summary>
    protected bool IsSummonedThisTurn()
    {
        var turn = Creature.PetOwner?.PlayerCombatState?.TurnNumber ?? -1;
        return turn == _summonedTurn && _summonedTurn >= 0;
    }

    /// <summary>
    /// 回合结束攻击：对随机可命中敌人造成攻击力点伤害。
    /// </summary>
    protected async Task PerformTurnEndAttack(PlayerChoiceContext choiceContext)
    {
        if (BaseAttackValue <= 0 || Creature == null || Creature.CombatState == null)
        {
            return;
        }
        var opponents = Creature.CombatState
            .GetOpponentsOf(Creature)
            .Where(e => e != null && e.IsAlive && e.IsHittable)
            .ToList();
        if (opponents.Count == 0)
        {
            return;
        }
        var target = CombatState.RunState.Rng.CombatTargets.NextItem(opponents);
        if (target == null)
        {
            return;
        }
        // Move 标记：触发荆棘反伤与振翅（Flutter）层数减少（IsPoweredAttack）
        await CreatureCmd.Damage(choiceContext, [target], BaseAttackValue, ValueProp.Move, Creature);

        // 已攻击：意图消失（下回合开始恢复显示）
        _hasAttackedThisTurn = true;
        RefreshIntentDisplay();
    }

    /// <summary>
    /// 各随从独有的回合结束被动（基类默认为空）。
    /// </summary>
    protected virtual Task PerformTurnEndPassive(PlayerChoiceContext choiceContext) => Task.CompletedTask;

    /// <summary>
    /// 随从死亡：触发亡语。
    /// 0.111.1 中致命伤害不会调用 AfterDamageReceived(Late)（CreatureCmd.Damage 对致命伤害
    /// 直接走 Kill → KillWithoutCheckingWinCondition），因此亡语必须挂 AfterDeath
    /// （Hook.AfterDeath 在 RemoveAllPowersAfterDeath 之前触发，随从 Monster 天然在监听列表）。
    /// 场面清理（移除 Powers / 从 CombatManager/CombatState 摘除）由核心死亡流程
    /// （RemoveAllPowersAfterDeath + OnPetDied）与 MinionLib MinionKillPatch 负责，
    /// 这里不做 detach（CombatState.RemoveCreature(unattach:true) 会把 CombatState 置 null，
    /// 干扰核心死亡收尾）。
    /// </summary>
    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature,
        bool wasRemovalPrevented, float deathAnimLength)
    {
        if (creature != Creature)
        {
            return;
        }
        // 炉石亡语语义：死亡被阻止（随从被救活）时不触发亡语——随从并没有死。
        // 游戏在死亡被阻止时也会调用 AfterDeath（wasRemovalPrevented=true）。
        if (wasRemovalPrevented)
        {
            MegaCrit.Sts2.Core.Logging.Log.Info(
                $"[JainaDeathrattle] AfterDeath prevented (no rattle): monster={GetType().Name}");
            return;
        }
        MegaCrit.Sts2.Core.Logging.Log.Info(
            $"[JainaDeathrattle] AfterDeath: monster={GetType().Name} hasRattle={HasDeathrattle} " +
            $"combatStateNull={Creature.CombatState == null}");
        if (HasDeathrattle)
        {
            try
            {
                await OnDeathrattle(choiceContext);
            }
            catch (System.Exception ex)
            {
                // 亡语失败记录日志（不吞异常，便于排查）
                MegaCrit.Sts2.Core.Logging.Log.Warn($"[JainaDeathrattle] error on {GetType().Name}: {ex}");
            }
        }
    }

    /// <summary>
    /// 是否拥有亡语词条（子类设置为 true 时，随从死亡会触发 <see cref="OnDeathrattle"/>）。
    /// </summary>
    public virtual bool HasDeathrattle => false;

    /// <summary>
    /// 亡语效果：随从死亡时触发。子类重写以实现具体效果。
    /// </summary>
    public virtual Task OnDeathrattle(PlayerChoiceContext choiceContext) => Task.CompletedTask;

    /// <summary>
    /// 造成伤害后（基类钩子）：
    /// 冰霜女巫吉安娜光环下，元素随从造成伤害回复主人等量生命（吸血）。
    /// 子类覆写本方法时需调用 base（如水元素在冻结逻辑前调用）。
    /// </summary>
    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result,
        ValueProp props, Creature target, CardModel? cardSource)
    {
        if (dealer != Creature || result.TotalDamage <= 0)
        {
            return;
        }
        var owner = Creature.PetOwner;
        if (owner == null)
        {
            return;
        }
        // 主人有冰霜女巫吉安娜光环（元素吸血），且本随从是元素 → 回复主人等量生命
        if (owner.Creature.Powers.Any(p => p is Powers.FrostLichJainaPower) && IsElementalMinion())
        {
            await CreatureCmd.Heal(owner.Creature, result.TotalDamage);
        }
    }

    /// <summary>
    /// 本随从是否为元素种族（通过随从卡映射查卡的种族关键词，含以后新增的元素随从）
    /// </summary>
    public bool IsElementalMinion()
    {
        var cardType = JainaMinionCardMap.GetCardType(GetType());
        if (cardType == null)
        {
            return false;
        }
        var canonical = MegaCrit.Sts2.Core.Models.ModelDb.GetByIdOrNull<MegaCrit.Sts2.Core.Models.CardModel>(
            MegaCrit.Sts2.Core.Models.ModelDb.GetId(cardType));
        return canonical?.CanonicalKeywords?.Contains(Keywords.JainaKeywords.Elemental) ?? false;
    }
}
