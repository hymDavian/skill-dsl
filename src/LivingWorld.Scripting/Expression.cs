namespace LivingWorld.Core.Scripting;

/// <summary>一元运算符。</summary>
public enum UnaryOperator
{
	/// <summary>取负：<c>-x</c>，只接受数值。</summary>
	Negate = 0,

	/// <summary>逻辑非：<c>!x</c>，只接受布尔。</summary>
	Not = 1,
}

/// <summary>二元运算符。枚举顺序即优先级分组顺序（低 → 高见解析器）。</summary>
public enum BinaryOperator
{
	/// <summary>加：<c>+</c>，只接受数值。</summary>
	Add = 0,

	/// <summary>减：<c>-</c>，只接受数值。</summary>
	Subtract = 1,

	/// <summary>乘：<c>*</c>，只接受数值。</summary>
	Multiply = 2,

	/// <summary>除：<c>/</c>，只接受数值，除数为 0 时报错。</summary>
	Divide = 3,

	/// <summary>取余：<c>%</c>，只接受数值，除数为 0 时报错。</summary>
	Modulo = 4,

	/// <summary>相等：<c>==</c>，要求两侧同类型。</summary>
	Equal = 5,

	/// <summary>不等：<c>!=</c>，要求两侧同类型。</summary>
	NotEqual = 6,

	/// <summary>小于：<c>&lt;</c>，只接受数值。</summary>
	Less = 7,

	/// <summary>小于等于：<c>&lt;=</c>，只接受数值。</summary>
	LessOrEqual = 8,

	/// <summary>大于：<c>&gt;</c>，只接受数值。</summary>
	Greater = 9,

	/// <summary>大于等于：<c>&gt;=</c>，只接受数值。</summary>
	GreaterOrEqual = 10,

	/// <summary>逻辑与：<c>&amp;&amp;</c>，只接受布尔，<b>短路求值</b>。</summary>
	And = 11,

	/// <summary>逻辑或：<c>||</c>，只接受布尔，<b>短路求值</b>。</summary>
	Or = 12,
}

/// <summary>表达式 AST 的根类型。不可变。</summary>
public abstract record Expression;

/// <summary>
/// 字面量：数字 / 布尔 / 字符串 / null。
/// </summary>
public sealed record LiteralExpression(ScriptValue Value) : Expression;

/// <summary>
/// 变量引用，路径为点分形式（如 <c>caster.Str</c>、<c>skill.level</c>）。
/// </summary>
/// <remarks>
/// 路径由宿主通过 <see cref="IScriptContext"/> 解析，表达式内核<b>不认识任何具体变量</b>——
/// 这是防越权的关键：能读到什么，完全由宿主决定。
/// </remarks>
public sealed record VariableExpression(string Path) : Expression;

/// <summary>一元运算节点。</summary>
public sealed record UnaryExpression(UnaryOperator Operator, Expression Operand) : Expression;

/// <summary>二元运算节点。</summary>
public sealed record BinaryExpression(BinaryOperator Operator, Expression Left, Expression Right) : Expression;

/// <summary>
/// 函数调用节点。<see cref="Name"/> 必须在 <see cref="ScriptFunctionTable"/> 白名单内。
/// </summary>
public sealed record CallExpression(string Name, IReadOnlyList<Expression> Arguments) : Expression;
