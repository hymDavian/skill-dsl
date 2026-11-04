namespace LivingWorld.Core.Scripting.Methods;

/// <summary>
/// 调用现场的一个实参：要么是常量，要么是一段待求值的表达式。
/// </summary>
/// <remarks>
/// 这就是数据里两种写法的落点：字面量 → <see cref="Constant"/>；
/// <c>$expr</c> 表达式 → <see cref="Computed"/>。
/// </remarks>
public abstract record MethodArgument
{
	/// <summary>常量实参。</summary>
	public sealed record Constant(ScriptValue Value) : MethodArgument;

	/// <summary>表达式实参，派发时按调用上下文求值。</summary>
	public sealed record Computed(Expression Expression) : MethodArgument;
}
