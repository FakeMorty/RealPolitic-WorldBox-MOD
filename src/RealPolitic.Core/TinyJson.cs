using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealPolitic.Core;

public static partial class TinyJson
{
	public sealed partial class Writer
	{
		private readonly StringBuilder _sb = new StringBuilder();

		private readonly Stack<bool> _needsComma = new Stack<bool>();

		private bool _pendingKey;

		private string _pendingKeyName = string.Empty;

		public Writer BeginObject()
		{
			FlushPending();
			_sb.Append('{');
			_needsComma.Push(item: false);
			return this;
		}

		public Writer EndObject()
		{
			if (_needsComma.Count > 0)
			{
				_needsComma.Pop();
			}
			_sb.Append('}');
			return this;
		}

		public Writer BeginArray()
		{
			FlushPending();
			_sb.Append('[');
			_needsComma.Push(item: false);
			return this;
		}

		public Writer EndArray()
		{
			if (_needsComma.Count > 0)
			{
				_needsComma.Pop();
			}
			_sb.Append(']');
			return this;
		}

		public Writer Key(string name)
		{
			_pendingKey = true;
			_pendingKeyName = name;
			return this;
		}

		public Writer Value(string? value)
		{
			FlushPending();
			WriteString(value);
			return this;
		}

		public Writer Value(int value)
		{
			FlushPending();
			_sb.Append(value.ToString(CultureInfo.InvariantCulture));
			return this;
		}

		public Writer Value(float value)
		{
			FlushPending();
			if (float.IsNaN(value) || float.IsInfinity(value))
			{
				_sb.Append("0");
			}
			else
			{
				_sb.Append(value.ToString("R", CultureInfo.InvariantCulture));
			}
			return this;
		}

		public Writer Value(bool value)
		{
			FlushPending();
			_sb.Append(value ? "true" : "false");
			return this;
		}

		public override string ToString()
		{
			return _sb.ToString();
		}

		private void FlushPending()
		{
			if (_needsComma.Count == 0)
			{
				_pendingKey = false;
				return;
			}
			if (_needsComma.ToArray()[0])
			{
				_sb.Append(',');
			}
			_needsComma.Pop();
			_needsComma.Push(item: true);
			if (_pendingKey)
			{
				WriteString(_pendingKeyName);
				_sb.Append(':');
				_pendingKey = false;
			}
		}

		private void WriteString(string? value)
		{
			if (value == null)
			{
				_sb.Append("null");
				return;
			}
			_sb.Append('"');
			foreach (char c in value)
			{
				switch (c)
				{
				case '"':
					_sb.Append("\\\"");
					continue;
				case '\\':
					_sb.Append("\\\\");
					continue;
				case '\n':
					_sb.Append("\\n");
					continue;
				case '\r':
					_sb.Append("\\r");
					continue;
				case '\t':
					_sb.Append("\\t");
					continue;
				}
				if (c < ' ')
				{
					StringBuilder stringBuilder = _sb.Append("\\u");
					int num = c;
					stringBuilder.Append(num.ToString("x4"));
				}
				else
				{
					_sb.Append(c);
				}
			}
			_sb.Append('"');
		}
	}

	public enum TokenKind { Unknown, Object, Array, String, Number, Boolean, Null }

	public sealed class Node
	{
        public TokenKind Kind;
		public string? Str;

		public double Num;

		public bool Bool;

		public bool IsNull;

		public Dictionary<string, Node> Obj = new Dictionary<string, Node>();

		public List<Node> Arr = new List<Node>();

		public bool Has(string key)
		{
			return Obj.ContainsKey(key);
		}

		public Node? Get(string key)
		{
			if (!Obj.TryGetValue(key, out var value))
			{
				return null;
			}
			return value;
		}

		/// <summary>Чтение 64-битного числа: значение хранится строкой или числом JSON.</summary>
		public long GetLong(string key, long fallback = 0)
		{
			Node node = Get(key);
			if (node == null || node.IsNull)
			{
				return fallback;
			}
			if (node.Str != null)
			{
				if (!long.TryParse(node.Str, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result))
				{
					return fallback;
				}
				return result;
			}
			return (long)node.Num;
		}

		public int GetInt(string key, int fallback = 0)
		{
			Node node = Get(key);
			if (node == null || node.IsNull)
			{
				return fallback;
			}
			if (node.Str != null)
			{
				if (!int.TryParse(node.Str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
				{
					return fallback;
				}
				return result;
			}
			return (int)node.Num;
		}

		public float GetFloat(string key, float fallback = 0f)
		{
			Node node = Get(key);
			if (node == null || node.IsNull)
			{
				return fallback;
			}
			if (node.Str != null)
			{
				if (!float.TryParse(node.Str, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
				{
					return fallback;
				}
				return result;
			}
			return (float)node.Num;
		}

		public bool GetBool(string key, bool fallback = false)
		{
			Node node = Get(key);
			if (node == null || node.IsNull)
			{
				return fallback;
			}
			if (node.Str != null)
			{
				if (!(node.Str == "true"))
				{
					return node.Str == "1";
				}
				return true;
			}
			return node.Bool;
		}

		public string GetString(string key, string fallback = "")
		{
			Node node = Get(key);
			if (node == null || node.IsNull)
			{
				return fallback;
			}
			return node.Str ?? fallback;
		}
	}

	public static Node Parse(string json)
	{
		int pos = 0;
		return ParseValue(json, ref pos);
	}

	private static Node ParseValue(string s, ref int pos)
	{
		SkipWs(s, ref pos);
		if (pos >= s.Length)
		{
			return new Node
			{
				Kind = TokenKind.Null,
                IsNull = true
			};
		}
		switch (s[pos])
		{
		case '{':
			return ParseObject(s, ref pos);
		case '[':
			return ParseArray(s, ref pos);
		case '"':
			return new Node
			{
				Kind = TokenKind.String,
                Str = ParseString(s, ref pos)
			};
		case 'f':
		case 't':
			return ParseBool(s, ref pos);
		case 'n':
			pos += 4;
			return new Node
			{
				Kind = TokenKind.Null,
                IsNull = true
			};
		default:
			return ParseNumber(s, ref pos);
		}
	}

	private static Node ParseObject(string s, ref int pos)
	{
		Node node = new Node { Kind = TokenKind.Object };
		pos++;
		SkipWs(s, ref pos);
		if (pos < s.Length && s[pos] == '}')
		{
			pos++;
			return node;
		}
		while (pos < s.Length)
		{
			SkipWs(s, ref pos);
			string key = ParseString(s, ref pos);
			SkipWs(s, ref pos);
			if (pos < s.Length && s[pos] == ':')
			{
				pos++;
			}
			Node value = ParseValue(s, ref pos);
			node.Obj[key] = value;
			SkipWs(s, ref pos);
			if (pos < s.Length && s[pos] == ',')
			{
				pos++;
				continue;
			}
			if (pos < s.Length && s[pos] == '}')
			{
				pos++;
			}
			break;
		}
		return node;
	}

	private static Node ParseArray(string s, ref int pos)
	{
		Node node = new Node { Kind = TokenKind.Array };
		pos++;
		SkipWs(s, ref pos);
		if (pos < s.Length && s[pos] == ']')
		{
			pos++;
			return node;
		}
		while (pos < s.Length)
		{
			Node item = ParseValue(s, ref pos);
			node.Arr.Add(item);
			SkipWs(s, ref pos);
			if (pos < s.Length && s[pos] == ',')
			{
				pos++;
				continue;
			}
			if (pos < s.Length && s[pos] == ']')
			{
				pos++;
			}
			break;
		}
		return node;
	}

	private static Node ParseBool(string s, ref int pos)
	{
		if (s[pos] == 't')
		{
			pos += 4;
			return new Node
			{
				Kind = TokenKind.Boolean,
                Bool = true
			};
		}
		pos += 5;
		return new Node
		{
			Kind = TokenKind.Boolean,
            Bool = false
		};
	}

	private static Node ParseNumber(string s, ref int pos)
	{
		int num = pos;
		while (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '-' || s[pos] == '+' || s[pos] == '.' || s[pos] == 'e' || s[pos] == 'E'))
		{
			pos++;
		}
		double.TryParse(s.Substring(num, pos - num), NumberStyles.Float, CultureInfo.InvariantCulture, out var result);
		return new Node
		{
			Kind = TokenKind.Number,
            Num = result
		};
	}

	private static string ParseString(string s, ref int pos)
	{
		if (pos >= s.Length || s[pos] != '"')
		{
			return string.Empty;
		}
		pos++;
		StringBuilder stringBuilder = new StringBuilder();
		while (pos < s.Length)
		{
			char c = s[pos];
			if (c == '"')
			{
				pos++;
				break;
			}
			if (c == '\\' && pos + 1 < s.Length)
			{
				char c2 = s[pos + 1];
				switch (c2)
				{
				case 'n':
					stringBuilder.Append('\n');
					break;
				case 'r':
					stringBuilder.Append('\r');
					break;
				case 't':
					stringBuilder.Append('\t');
					break;
				case '"':
					stringBuilder.Append('"');
					break;
				case '\\':
					stringBuilder.Append('\\');
					break;
				case '/':
					stringBuilder.Append('/');
					break;
				case 'u':
					if (pos + 5 < s.Length)
					{
						if (int.TryParse(s.Substring(pos + 2, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
						{
							stringBuilder.Append((char)result);
						}
						pos += 4;
					}
					break;
				default:
					stringBuilder.Append(c2);
					break;
				}
				pos += 2;
			}
			else
			{
				stringBuilder.Append(c);
				pos++;
			}
		}
		return stringBuilder.ToString();
	}

	private static void SkipWs(string s, ref int pos)
	{
		while (pos < s.Length && (s[pos] == ' ' || s[pos] == '\n' || s[pos] == '\r' || s[pos] == '\t'))
		{
			pos++;
		}
	}
}
