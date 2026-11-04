namespace LivingWorld.Core.Scripting;

/// <summary>词法单元种类。仅供解析器内部使用。</summary>
internal enum ScriptTokenKind
{
	Number,
	String,
	Identifier,
	True,
	False,
	Null,
	Plus,
	Minus,
	Star,
	Slash,
	Percent,
	LParen,
	RParen,
	Comma,
	Dot,
	Bang,
	AndAnd,
	OrOr,
	EqualEqual,
	BangEqual,
	Less,
	LessEqual,
	Greater,
	GreaterEqual,
	EndOfInput,
}

/// <summary>一个词法单元。仅供解析器内部使用。</summary>
internal readonly record struct ScriptToken(
	ScriptTokenKind Kind,
	string Text,
	double Number,
	int Position);

/// <summary>
/// 手写词法分析器。
/// </summary>
/// <remarks>
/// 不用正则、不引入第三方解析库：需要的是<b>精确的出错位置</b>与"只认识这套文法"的封闭性，
/// 这两点比少写两百行代码更重要。
/// </remarks>
internal sealed class ScriptLexer
{
	private readonly string _source;
	private int _index;

	private ScriptLexer(string source)
	{
		_source = source;
	}

	/// <summary>把源码切成词法单元序列（末尾必有一个 <see cref="ScriptTokenKind.EndOfInput"/>）。</summary>
	public static List<ScriptToken> Tokenize(string source)
	{
		ArgumentNullException.ThrowIfNull(source);
		ScriptLexer lexer = new(source);
		return lexer.Run();
	}

	private List<ScriptToken> Run()
	{
		List<ScriptToken> tokens = new();

		while (true)
		{
			SkipWhitespace();
			if (_index >= _source.Length)
			{
				tokens.Add(new ScriptToken(ScriptTokenKind.EndOfInput, string.Empty, 0d, _index));
				return tokens;
			}

			tokens.Add(Next());
		}
	}

	private ScriptToken Next()
	{
		int start = _index;
		char current = _source[_index];

		if (char.IsAsciiDigit(current) || current == '.')
		{
			// 注意：'.' 也可能是路径分隔符。只有"点后面跟数字"才当作小数。
			if (current == '.' && !PeekIsDigit(1))
			{
				_index++;
				return new ScriptToken(ScriptTokenKind.Dot, ".", 0d, start);
			}

			return ReadNumber(start);
		}

		if (current == '"')
		{
			return ReadString(start);
		}

		if (IsIdentifierStart(current))
		{
			return ReadIdentifier(start);
		}

		return ReadOperator(start);
	}

	private ScriptToken ReadNumber(int start)
	{
		while (_index < _source.Length && char.IsAsciiDigit(_source[_index]))
		{
			_index++;
		}

		if (_index < _source.Length && _source[_index] == '.' && PeekIsDigit(1))
		{
			_index++;
			while (_index < _source.Length && char.IsAsciiDigit(_source[_index]))
			{
				_index++;
			}
		}

		string text = _source[start.._index];
		double value = double.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
		return new ScriptToken(ScriptTokenKind.Number, text, value, start);
	}

	private ScriptToken ReadString(int start)
	{
		_index++; // 跳过起始引号
		System.Text.StringBuilder builder = new();

		while (_index < _source.Length)
		{
			char current = _source[_index];

			if (current == '"')
			{
				_index++;
				return new ScriptToken(ScriptTokenKind.String, builder.ToString(), 0d, start);
			}

			if (current != '\\')
			{
				builder.Append(current);
				_index++;
				continue;
			}

			// 转义
			_index++;
			if (_index >= _source.Length)
			{
				throw new ScriptParseException("字符串未闭合", start);
			}

			char escaped = _source[_index];
			_index++;
			builder.Append(escaped switch
			{
				'"' => '"',
				'\\' => '\\',
				'n' => '\n',
				't' => '\t',
				_ => throw new ScriptParseException($"不支持的转义字符：\\{escaped}", _index - 1),
			});
		}

		throw new ScriptParseException("字符串未闭合", start);
	}

	private ScriptToken ReadIdentifier(int start)
	{
		while (_index < _source.Length && IsIdentifierPart(_source[_index]))
		{
			_index++;
		}

		string text = _source[start.._index];
		ScriptTokenKind kind = text switch
		{
			"true" => ScriptTokenKind.True,
			"false" => ScriptTokenKind.False,
			"null" => ScriptTokenKind.Null,
			_ => ScriptTokenKind.Identifier,
		};

		return new ScriptToken(kind, text, 0d, start);
	}

	private ScriptToken ReadOperator(int start)
	{
		char current = _source[_index];
		char next = _index + 1 < _source.Length ? _source[_index + 1] : '\0';

		// 先匹配双字符运算符
		(ScriptTokenKind Kind, string Text)? pair = (current, next) switch
		{
			('&', '&') => (ScriptTokenKind.AndAnd, "&&"),
			('|', '|') => (ScriptTokenKind.OrOr, "||"),
			('=', '=') => (ScriptTokenKind.EqualEqual, "=="),
			('!', '=') => (ScriptTokenKind.BangEqual, "!="),
			('<', '=') => (ScriptTokenKind.LessEqual, "<="),
			('>', '=') => (ScriptTokenKind.GreaterEqual, ">="),
			_ => null,
		};

		if (pair is { } matched)
		{
			_index += 2;
			return new ScriptToken(matched.Kind, matched.Text, 0d, start);
		}

		ScriptTokenKind single = current switch
		{
			'+' => ScriptTokenKind.Plus,
			'-' => ScriptTokenKind.Minus,
			'*' => ScriptTokenKind.Star,
			'/' => ScriptTokenKind.Slash,
			'%' => ScriptTokenKind.Percent,
			'(' => ScriptTokenKind.LParen,
			')' => ScriptTokenKind.RParen,
			',' => ScriptTokenKind.Comma,
			'.' => ScriptTokenKind.Dot,
			'!' => ScriptTokenKind.Bang,
			'<' => ScriptTokenKind.Less,
			'>' => ScriptTokenKind.Greater,
			_ => throw new ScriptParseException($"无法识别的字符 '{current}'", start),
		};

		_index++;
		return new ScriptToken(single, current.ToString(), 0d, start);
	}

	private void SkipWhitespace()
	{
		while (_index < _source.Length && char.IsWhiteSpace(_source[_index]))
		{
			_index++;
		}
	}

	private bool PeekIsDigit(int offset)
	{
		int position = _index + offset;
		return position < _source.Length && char.IsAsciiDigit(_source[position]);
	}

	private static bool IsIdentifierStart(char value)
		=> char.IsAsciiLetter(value) || value == '_';

	private static bool IsIdentifierPart(char value)
		=> char.IsAsciiLetterOrDigit(value) || value == '_';
}
