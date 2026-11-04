using System.Text;

namespace SkillDsl;

/// <summary>
/// 手写递归下降解析器：文本表达式 → <see cref="Expression"/> AST。
/// </summary>
/// <remarks>
/// <para>优先级由低到高：<c>||</c> → <c>&amp;&amp;</c> → <c>== != </c> → <c>&lt; &lt;= &gt; &gt;=</c>
/// → <c>+ -</c> → <c>* / %</c> → 一元 <c>- !</c> → 原子。</para>
/// <para>解析失败<b>只报第一个错误</b>（语法错误后无法可靠恢复）。需要"一次报全部错误"的是
/// 数据校验层——它会把每条表达式单独解析，从而自然收集到所有文件的错误。</para>
/// </remarks>
public static class ExpressionParser
{
	/// <summary>解析一段表达式文本。</summary>
	/// <exception cref="ScriptParseException">语法错误，异常里带出错位置。</exception>
	public static Expression Parse(string source)
	{
		ArgumentNullException.ThrowIfNull(source);

		Parser parser = new(ScriptLexer.Tokenize(source));
		Expression expression = parser.ParseExpression();
		parser.ExpectEnd();
		return expression;
	}

	private sealed class Parser
	{
		private readonly List<ScriptToken> _tokens;
		private int _position;

		public Parser(List<ScriptToken> tokens) => _tokens = tokens;

		private ScriptToken Current => _tokens[_position];

		public Expression ParseExpression() => ParseOr();

		public void ExpectEnd()
		{
			if (Current.Kind != ScriptTokenKind.EndOfInput)
			{
				throw Error($"表达式后有多余内容 '{Current.Text}'");
			}
		}

		private Expression ParseOr()
		{
			Expression left = ParseAnd();

			while (Match(ScriptTokenKind.OrOr))
			{
				left = new BinaryExpression(BinaryOperator.Or, left, ParseAnd());
			}

			return left;
		}

		private Expression ParseAnd()
		{
			Expression left = ParseEquality();

			while (Match(ScriptTokenKind.AndAnd))
			{
				left = new BinaryExpression(BinaryOperator.And, left, ParseEquality());
			}

			return left;
		}

		private Expression ParseEquality()
		{
			Expression left = ParseComparison();

			while (true)
			{
				if (Match(ScriptTokenKind.EqualEqual))
				{
					left = new BinaryExpression(BinaryOperator.Equal, left, ParseComparison());
					continue;
				}

				if (Match(ScriptTokenKind.BangEqual))
				{
					left = new BinaryExpression(BinaryOperator.NotEqual, left, ParseComparison());
					continue;
				}

				return left;
			}
		}

		private Expression ParseComparison()
		{
			Expression left = ParseAdditive();

			while (true)
			{
				BinaryOperator? op = Current.Kind switch
				{
					ScriptTokenKind.Less => BinaryOperator.Less,
					ScriptTokenKind.LessEqual => BinaryOperator.LessOrEqual,
					ScriptTokenKind.Greater => BinaryOperator.Greater,
					ScriptTokenKind.GreaterEqual => BinaryOperator.GreaterOrEqual,
					_ => null,
				};

				if (op is null)
				{
					return left;
				}

				_position++;
				left = new BinaryExpression(op.Value, left, ParseAdditive());
			}
		}

		private Expression ParseAdditive()
		{
			Expression left = ParseMultiplicative();

			while (true)
			{
				if (Match(ScriptTokenKind.Plus))
				{
					left = new BinaryExpression(BinaryOperator.Add, left, ParseMultiplicative());
					continue;
				}

				if (Match(ScriptTokenKind.Minus))
				{
					left = new BinaryExpression(BinaryOperator.Subtract, left, ParseMultiplicative());
					continue;
				}

				return left;
			}
		}

		private Expression ParseMultiplicative()
		{
			Expression left = ParseUnary();

			while (true)
			{
				if (Match(ScriptTokenKind.Star))
				{
					left = new BinaryExpression(BinaryOperator.Multiply, left, ParseUnary());
					continue;
				}

				if (Match(ScriptTokenKind.Slash))
				{
					left = new BinaryExpression(BinaryOperator.Divide, left, ParseUnary());
					continue;
				}

				if (Match(ScriptTokenKind.Percent))
				{
					left = new BinaryExpression(BinaryOperator.Modulo, left, ParseUnary());
					continue;
				}

				return left;
			}
		}

		private Expression ParseUnary()
		{
			if (Match(ScriptTokenKind.Minus))
			{
				return new UnaryExpression(UnaryOperator.Negate, ParseUnary());
			}

			if (Match(ScriptTokenKind.Bang))
			{
				return new UnaryExpression(UnaryOperator.Not, ParseUnary());
			}

			return ParsePrimary();
		}

		private Expression ParsePrimary()
		{
			ScriptToken token = Current;

			switch (token.Kind)
			{
				case ScriptTokenKind.Number:
					_position++;
					return new LiteralExpression(ScriptValue.FromNumber(token.Number));

				case ScriptTokenKind.String:
					_position++;
					return new LiteralExpression(ScriptValue.FromString(token.Text));

				case ScriptTokenKind.True:
					_position++;
					return new LiteralExpression(ScriptValue.FromBool(true));

				case ScriptTokenKind.False:
					_position++;
					return new LiteralExpression(ScriptValue.FromBool(false));

				case ScriptTokenKind.Null:
					_position++;
					return new LiteralExpression(ScriptValue.Null);

				case ScriptTokenKind.LParen:
					{
						_position++;
						Expression inner = ParseExpression();
						Expect(ScriptTokenKind.RParen, "缺少右括号 ')'");
						return inner;
					}

				case ScriptTokenKind.Identifier:
					return ParseIdentifierOrCall();

				default:
					throw Error($"意外的记号 '{token.Text}'");
			}
		}

		private Expression ParseIdentifierOrCall()
		{
			ScriptToken name = Current;
			_position++;

			if (Current.Kind == ScriptTokenKind.LParen)
			{
				_position++;
				List<Expression> arguments = new();

				if (Current.Kind != ScriptTokenKind.RParen)
				{
					arguments.Add(ParseExpression());
					while (Match(ScriptTokenKind.Comma))
					{
						arguments.Add(ParseExpression());
					}
				}

				Expect(ScriptTokenKind.RParen, $"函数 {name.Text} 的调用缺少右括号 ')'");
				return new CallExpression(name.Text, arguments);
			}

			StringBuilder path = new(name.Text);
			while (Current.Kind == ScriptTokenKind.Dot)
			{
				_position++;
				if (Current.Kind != ScriptTokenKind.Identifier)
				{
					throw Error("点号 '.' 后面必须是标识符");
				}

				path.Append('.').Append(Current.Text);
				_position++;
			}

			return new VariableExpression(path.ToString());
		}

		private bool Match(ScriptTokenKind kind)
		{
			if (Current.Kind != kind)
			{
				return false;
			}

			_position++;
			return true;
		}

		private void Expect(ScriptTokenKind kind, string message)
		{
			if (!Match(kind))
			{
				throw Error(message);
			}
		}

		private ScriptParseException Error(string message)
			=> new(message, Current.Position);
	}
}
