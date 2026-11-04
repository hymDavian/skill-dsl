namespace SkillDsl.Methods;

/// <summary>
/// 一次方法派发的结果。
/// </summary>
/// <remarks>
/// <b>"被拒绝"是正常业务结果，不是异常。</b>
/// LLM 调用越权方法时，必须把原因作为工具结果回喂给模型让它换个做法——
/// 抛异常会打断整个 tick。只有"实现自身的 bug"才交给 <see cref="Diagnostics.IErrorSink"/>。
/// </remarks>
public sealed record MethodDispatchResult(
	bool Ok,
	ScriptValue Return,
	string? ErrorCode,
	string? Message)
{
	/// <summary>成功且无返回值。</summary>
	public static MethodDispatchResult Void()
		=> new(true, ScriptValue.Null, null, null);

	/// <summary>成功并带返回值。</summary>
	public static MethodDispatchResult Success(ScriptValue value)
		=> new(true, value, null, null);

	/// <summary>失败。</summary>
	public static MethodDispatchResult Failure(string errorCode, string message)
		=> new(false, ScriptValue.Null, errorCode, message);
}
