namespace SkillDsl.Narration;

/// <summary>待播条目的种类（<c>narr.*</c>）。</summary>
public enum NarrativeKind
{
	/// <summary>一条一闪而过的提示（HUD Toast）。</summary>
	Toast = 0,

	/// <summary>
	/// 引导玩家去跟某个人说话（<c>narr.start_dialogue</c>）。
	/// </summary>
/// <remarks>
	/// <b>不是“NPC 主动开口”</b>：它等价于“替玩家按了一下 <c>H</c>”，
	/// 打开面板后仍然由玩家说第一句。NPC 先说需要对话系统支持“反向开场”，那是另一批。
/// </remarks>
	Dialogue = 1,
}

/// <summary>
/// 权威侧产生的一条<b>待播</b>叙述条目。
/// </summary>
/// <remarks>
/// <para><b>为什么是"待播"而不是"直接弹"</b>：效果跑在 Core（无引擎）里，它没法弹任何东西；
/// 更关键的是——<b>回放要能重现当时的提示</b>。让效果直接调表现层，等于把"什么时候弹"交给帧率，
/// 同一串指令在两台机器上会弹在不同的时刻。</para>
/// <para><b>时间戳由权威侧盖</b>（<see cref="GameSeconds"/>）：去重窗口要按<b>游戏时间</b>算，
/// 不能按现实时间——暂停时现实时间照走，游戏时间不动。</para>
/// <para><b>目标单位 Id 是独立字段而不是塞进 <see cref="Text"/></b>：
/// 把 Id 拼进文本就是“字符串隐式协议”（硬约束 4），而它长得和提示文案一模一样，
/// 只能靠约定区分。字段分开后，前端拿到的是一个可信的 Id，不必拆字符串。</para>
/// </remarks>
/// <param name="Kind">种类。</param>
/// <param name="Text">文本（<see cref="NarrativeKind.Dialogue"/> 时可为空）。</param>
/// <param name="GameSeconds">产生时的游戏时间（秒）。</param>
/// <param name="TargetUnitId">对话对象 Id；只有 <see cref="NarrativeKind.Dialogue"/> 才有。</param>
public readonly record struct NarrativeMessage(
	NarrativeKind Kind,
	string Text,
	double GameSeconds,
	string? TargetUnitId = null);

/// <summary>
/// 让脚本方法能<b>投一条待播条目</b>的窄接口。
/// </summary>
/// <remarks>
/// <para><b>为什么它在 <c>Scripting</c> 而不是 <c>Units</c></b>：世界事件、机关、绑定都可能要说话，
/// 而它们<b>没有单位上下文</b>。挂在 <see cref="Units.IUnitScriptContext"/> 上就只有"单位效果"能说话，
/// 绑定（<c>world_event.*</c>）一辈子也发不出一条提示。</para>
/// <para><b>它不做去重与限量</b>：这两件事都要看"队列里已经有什么"，
/// 而队列住在权威循环里。让接口去做，等于把队列的状态漏给方法层。</para>
/// </remarks>
public interface INarrativeGateway
{
	/// <summary>投一条待播条目。</summary>
	/// <param name="kind">种类。</param>
	/// <param name="text">文本（<see cref="NarrativeKind.Dialogue"/> 时可空）。</param>
	/// <param name="targetUnitId">对话对象 Id；只有 <see cref="NarrativeKind.Dialogue"/> 才有。</param>
	/// <returns>
	/// 真的入队返回 true。同一条内容在去重窗口内重复、或队列已满时返回 false
	/// （<b>返回 false 不是错误</b>：机关每 tick 都触发时本来就该合并）。
	/// </returns>
	bool Publish(NarrativeKind kind, string text, string? targetUnitId = null);
}
