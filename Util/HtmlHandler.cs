/*
 *
 *
 * Written by claud because I cba to write my own Html parser and didn't want to use Agility.
 *
 *
 */

using System.Net;
using System.Text;
using System.Text.RegularExpressions;
// ReSharper disable ArrangeNamespaceBody
// ReSharper disable IdentifierTypo
// ReSharper disable UnusedMember.Global
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault

#pragma warning disable IDE0130

namespace RawHtml;

public enum TokenKind
{
	Doctype,
	OpenTag,
	CloseTag,
	SelfClosingTag,
	Text,
	Comment,
	CData,
	EndOfFile,
}

public sealed record Token(TokenKind Kind, string Raw, string TagName,
	Dictionary<string, string> Attributes);

public sealed class HtmlLexer(string html)
{
	private int _pos;

	private static readonly HashSet<string> VoidElements = new(StringComparer.OrdinalIgnoreCase)
	{
		"area","base","br","col","embed","hr","img","input",
		"link","meta","param","source","track","wbr"
	};

	public IReadOnlyList<Token> Tokenize()
	{
		List<Token> tokens = [];

		while (_pos < html.Length)
		{
			tokens.Add(html[_pos] == '<' ? ReadTag() : ReadText());
		}

		tokens.Add(new Token(TokenKind.EndOfFile, "", "", []));
		return tokens;
	}

	private Token ReadText()
	{
		int start = _pos;
		while (_pos < html.Length && html[_pos] != '<')
			_pos++;

		string raw = html[start.._pos];
		return new Token(TokenKind.Text, raw, "", []);
	}

	private Token ReadTag()
	{
		int start = _pos;
		_pos++;

		if (Peek("!--"))
		{
			_pos += 3;
			int end = html.IndexOf("-->", _pos, StringComparison.Ordinal);
			if (end == -1) end = html.Length - 3;
			_pos = end + 3;
			return new Token(TokenKind.Comment, html[start.._pos], "", []);
		}

		if (Peek("![CDATA["))
		{
			_pos += 8;
			int end = html.IndexOf("]]>", _pos, StringComparison.Ordinal);
			if (end == -1) end = html.Length - 3;
			_pos = end + 3;
			return new Token(TokenKind.CData, html[start.._pos], "", []);
		}

		if (Peek("!DOCTYPE", ignoreCase: true) || Peek("!doctype", ignoreCase: true))
		{
			int end = html.IndexOf('>', _pos);
			if (end == -1) end = html.Length - 1;
			_pos = end + 1;
			return new Token(TokenKind.Doctype, html[start.._pos], "", []);
		}

		if (_pos < html.Length && html[_pos] == '/')
		{
			_pos++;
			string closeName = ReadName();
			SkipTo('>');
			_pos++;
			return new Token(TokenKind.CloseTag, html[start.._pos], closeName, []);
		}

		string tagName = ReadName();
		Dictionary<string, string> attrs = ReadAttributes();

		SkipWhitespace();

		bool selfClose = false;
		if (_pos < html.Length && html[_pos] == '/')
		{
			selfClose = true;
			_pos++;
		}

		if (_pos < html.Length && html[_pos] == '>')
			_pos++;

		TokenKind kind = (selfClose || VoidElements.Contains(tagName))
			? TokenKind.SelfClosingTag
			: TokenKind.OpenTag;

		return new Token(kind, html[start.._pos], tagName, attrs);
	}

	private string ReadName()
	{
		SkipWhitespace();
		int start = _pos;
		while (_pos < html.Length && !char.IsWhiteSpace(html[_pos])
			   && html[_pos] != '>' && html[_pos] != '/' && html[_pos] != '=')
			_pos++;
		return html[start.._pos];
	}

	private Dictionary<string, string> ReadAttributes()
	{
		Dictionary<string, string> attrs = new(StringComparer.OrdinalIgnoreCase);

		while (_pos < html.Length)
		{
			SkipWhitespace();
			if (_pos >= html.Length || html[_pos] == '>' || html[_pos] == '/')
				break;

			string name = ReadAttributeName();
			if (string.IsNullOrEmpty(name)) break;

			SkipWhitespace();

			if (_pos < html.Length && html[_pos] == '=')
			{
				_pos++;
				SkipWhitespace();
				string value = ReadAttributeValue();
				attrs[name] = value;
			}
			else
			{
				attrs[name] = name;
			}
		}

		return attrs;
	}

	private string ReadAttributeName()
	{
		int start = _pos;
		while (_pos < html.Length && !char.IsWhiteSpace(html[_pos])
			   && html[_pos] != '=' && html[_pos] != '>' && html[_pos] != '/')
			_pos++;
		return html[start.._pos];
	}

	private string ReadAttributeValue()
	{
		if (_pos >= html.Length) return "";

		char quote = html[_pos];
		if (quote == '"' || quote == '\'')
		{
			_pos++;
			int start = _pos;
			while (_pos < html.Length && html[_pos] != quote)
				_pos++;
			string value = html[start.._pos];
			if (_pos < html.Length) _pos++;
			return HtmlDecode(value);
		}

		int ustart = _pos;
		while (_pos < html.Length && !char.IsWhiteSpace(html[_pos]) && html[_pos] != '>')
			_pos++;
		return HtmlDecode(html[ustart.._pos]);
	}

	private void SkipWhitespace()
	{
		while (_pos < html.Length && char.IsWhiteSpace(html[_pos]))
			_pos++;
	}

	private void SkipTo(char c)
	{
		while (_pos < html.Length && html[_pos] != c)
			_pos++;
	}

	private bool Peek(string s, bool ignoreCase = false)
	{
		if (_pos + s.Length > html.Length) return false;
		string slice = html.Substring(_pos, s.Length);
		return ignoreCase
			? slice.Equals(s, StringComparison.OrdinalIgnoreCase)
			: slice == s;
	}

	public static string HtmlDecode(string input) =>
		input
			.Replace("&amp;", "&")
			.Replace("&lt;", "<")
			.Replace("&gt;", ">")
			.Replace("&quot;", "\"")
			.Replace("&#39;", "'")
			.Replace("&apos;", "'")
			.Replace("&nbsp;", "\u00a0");
}

public enum NodeType { Element, Text, Comment, Document }

public abstract class HtmlNode
{
	public HtmlNode? Parent { get; internal set; }
	public abstract NodeType NodeType { get; }
	public abstract string OuterHtml { get; }
}

public sealed class HtmlTextNode(string text) : HtmlNode
{
	public string Text { get; } = HtmlLexer.HtmlDecode(text);
	public override NodeType NodeType => NodeType.Text;
	public override string OuterHtml => Text;
}

public sealed class HtmlCommentNode(string raw) : HtmlNode
{
	public string Raw { get; } = raw;
	public override NodeType NodeType => NodeType.Comment;
	public override string OuterHtml => Raw;
}

public sealed class HtmlElement(string tagName, Dictionary<string, string> attributes) : HtmlNode
{
	public string TagName { get; } = tagName.ToLowerInvariant();
	public Dictionary<string, string> Attributes { get; } = new(attributes, StringComparer.OrdinalIgnoreCase);
	public List<HtmlNode> ChildNodes { get; } = [];

	public override NodeType NodeType => NodeType.Element;

	public string? Id => GetAttribute("id");

	public IReadOnlyList<string> ClassList =>
		GetAttribute("class")?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
		?? [];

	public bool HasClass(string className) =>
		ClassList.Contains(className, StringComparer.OrdinalIgnoreCase);

	public string? GetAttribute(string name) => Attributes.GetValueOrDefault(name);

	public string? RegexGetInnerAttribute(string attributeName)
	{
		string pattern = $"""{Regex.Escape(attributeName)}\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s>]+))""";
		Match match = Regex.Match(InnerHtml, pattern, RegexOptions.IgnoreCase);
		if (!match.Success) return null;

		for (int i = 1; i <= 3; i++)
			if (match.Groups[i].Success)
				return WebUtility.HtmlDecode(match.Groups[i].Value);

		return null;
	}
	public string? RegexGetOuterAttribute(string attributeName)
	{
		string pattern = $"""{Regex.Escape(attributeName)}\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s>]+))""";
		Match match = Regex.Match(OuterHtml, pattern, RegexOptions.IgnoreCase);
		if (!match.Success) return null;

		for (int i = 1; i <= 3; i++)
			if (match.Groups[i].Success)
				return WebUtility.HtmlDecode(match.Groups[i].Value);

		return null;
	}

	public bool HasAttribute(string name) => Attributes.ContainsKey(name);

	public IEnumerable<HtmlElement> Children =>
		ChildNodes.OfType<HtmlElement>();

	public string InnerText
	{
		get
		{
			StringBuilder sb = new();
			CollectText(this, sb);
			return sb.ToString();
		}
	}

	public string TextContent => InnerText;

	private static void CollectText(HtmlElement el, StringBuilder sb)
	{
		foreach (HtmlNode node in el.ChildNodes)
		{
			switch (node)
			{
				case HtmlTextNode t:
					sb.Append(t.Text);
					break;
				case HtmlElement child:
					CollectText(child, sb);
					break;
			}
		}
	}

	public string InnerHtml =>
		string.Concat(ChildNodes.Select(n => n.OuterHtml));

	public override string OuterHtml
	{
		get
		{
			StringBuilder sb = new();
			sb.Append('<').Append(TagName);
			foreach (KeyValuePair<string, string> a in Attributes)
				sb.Append($" {a.Key}=\"{a.Value}\"");
			sb.Append('>');
			sb.Append(InnerHtml);
			sb.Append($"</{TagName}>");
			return sb.ToString();
		}
	}

	public override string ToString() => OuterHtml;
}

public sealed partial class HtmlDocument
{
	public HtmlElement? DocumentElement { get; private set; }
	public HtmlElement? Head { get; private set; }
	public HtmlElement? Body { get; private set; }
	public List<HtmlNode> Nodes { get; } = [];

	public static HtmlDocument Parse(string html)
	{
		HtmlDocument doc = new();
		HtmlLexer lexer = new(html);
		IReadOnlyList<Token> tokens = lexer.Tokenize();
		doc.Build(tokens);
		return doc;
	}

	private void Build(IReadOnlyList<Token> tokens)
	{
		Stack<HtmlElement> stack = new();
		HtmlElement? current = null;

		foreach ((TokenKind tokenKind, string trimmed, string tagName, Dictionary<string, string> dictionary) in tokens)
		{
			switch (tokenKind)
			{
				case TokenKind.Doctype:
				case TokenKind.Comment:
					current?.ChildNodes.Add(new HtmlCommentNode(trimmed) { Parent = current });
					break;

				case TokenKind.Text:
					if (!string.IsNullOrWhiteSpace(trimmed))
					{
						HtmlTextNode textNode = new(trimmed);
						if (current is not null)
						{
							textNode.Parent = current;
							current.ChildNodes.Add(textNode);
						}
						else
						{
							Nodes.Add(textNode);
						}
					}
					break;

				case TokenKind.OpenTag:
					{
						HtmlElement el = new(tagName, dictionary);
						AttachNode(el, current);

						switch (el.TagName)
						{
							case "html":
								DocumentElement = el;
								break;
							case "head":
								Head = el;
								break;
							case "body":
								Body = el;
								break;
						}

						if (current is not null) stack.Push(current);
						current = el;
						break;
					}

				case TokenKind.SelfClosingTag:
					{
						HtmlElement el = new(tagName, dictionary);
						AttachNode(el, current);
						break;
					}

				case TokenKind.CloseTag:
					{
						while (stack.Count > 0 &&
						       !current!.TagName.Equals(tagName, StringComparison.OrdinalIgnoreCase))
						{
							current = stack.Pop();
						}

						if (current?.TagName.Equals(tagName, StringComparison.OrdinalIgnoreCase) == true)
							current = stack.Count > 0 ? stack.Pop() : null;

						break;
					}

				case TokenKind.EndOfFile:
				case TokenKind.CData:
				default:
					break;
			}
		}
	}

	private void AttachNode(HtmlElement el, HtmlElement? parent)
	{
		if (parent is not null)
		{
			el.Parent = parent;
			parent.ChildNodes.Add(el);
		}
		else
		{
			Nodes.Add(el);
		}
	}

	public IEnumerable<HtmlElement> All =>
		DocumentElement is not null
			? Descendants(DocumentElement)
			: Nodes.OfType<HtmlElement>().SelectMany(e => new[] { e }.Concat(Descendants(e)));

	private static IEnumerable<HtmlElement> Descendants(HtmlElement root)
	{
		yield return root;
		foreach (HtmlElement child in root.Children)
			foreach (HtmlElement desc in Descendants(child))
				yield return desc;
	}

	public IEnumerable<HtmlElement> GetElementsByTagName(string tagName) =>
		All.Where(e => e.TagName.Equals(tagName, StringComparison.OrdinalIgnoreCase));

	public IEnumerable<HtmlElement> GetElementsByClassName(string className) =>
		All.Where(e => e.HasClass(className));

	public HtmlElement? GetElementById(string id) =>
		All.FirstOrDefault(e => e.Id?.Equals(id, StringComparison.OrdinalIgnoreCase) == true);

	public IEnumerable<HtmlElement> GetElementsByAttribute(string attr, string? value = null) =>
		All.Where(e => value is null
			? e.HasAttribute(attr)
			: e.GetAttribute(attr)?.Equals(value, StringComparison.OrdinalIgnoreCase) == true);

	public IEnumerable<HtmlElement> SelectNodes(string xpath)
	{
		XPathLiteEvaluator evaluator = new(this);
		return evaluator.Evaluate(xpath);
	}

	public HtmlElement? SelectSingleNode(string xpath) =>
		SelectNodes(xpath).FirstOrDefault();
}

internal sealed class XPathLiteEvaluator(HtmlDocument doc)
{
	public IEnumerable<HtmlElement> Evaluate(string xpath)
	{
		xpath = xpath.Trim();

		if (xpath.StartsWith("//"))
		{
			string rest = xpath[2..];
			return EvaluateStep(rest, doc.All);
		}

		if (xpath.StartsWith('/'))
		{
			string[] parts = xpath[1..].Split('/');
			IEnumerable<HtmlElement> context = doc.Nodes.OfType<HtmlElement>();

			foreach (string part in parts)
			{
				(string tag, Predicate<HtmlElement>? pred, int? index) = ParseStep(part);
				context = context.SelectMany(e => e.Children)
								 .Where(e => MatchTag(e, tag));
				if (pred is not null) context = context.Where(e => pred(e));
				if (index is not null)
					context = context.Skip(index.Value - 1).Take(1);
			}

			return context;
		}

		return EvaluateStep(xpath, doc.All);
	}

	private IEnumerable<HtmlElement> EvaluateStep(string expr, IEnumerable<HtmlElement> pool)
	{
		if (expr.Contains("//"))
		{
			int idx = expr.IndexOf("//", StringComparison.Ordinal);
			string first = expr[..idx];
			string rest = expr[(idx + 2)..];

			IEnumerable<HtmlElement> intermediate = EvaluateStep(first, pool);

			IEnumerable<HtmlElement> allDesc = intermediate
				.SelectMany(e => e.Children)
				.SelectMany(GetSelfAndDescendants);

			return EvaluateStep(rest, allDesc);
		}

		(string tagName, Predicate<HtmlElement>? predicate, int? posIndex) = ParseStep(expr);

		IEnumerable<HtmlElement> matches = pool.Where(e => MatchTag(e, tagName));
		if (predicate is not null) matches = matches.Where(e => predicate(e));

		if (posIndex is not null)
		{
			matches = matches
				.GroupBy(e => e.Parent)
				.SelectMany(g => g.Skip(posIndex.Value - 1).Take(1));
		}

		return matches;
	}

	private static IEnumerable<HtmlElement> GetSelfAndDescendants(HtmlElement el)
	{
		yield return el;
		foreach (HtmlElement child in el.Children)
			foreach (HtmlElement desc in GetSelfAndDescendants(child))
				yield return desc;
	}

	private static (string Tag, Predicate<HtmlElement>? Pred, int? Index) ParseStep(string step)
	{
		step = step.Trim();

		int bracketPos = step.IndexOf('[');
		string tag = bracketPos == -1 ? step : step[..bracketPos];
		tag = tag.Trim();

		if (bracketPos == -1)
			return (tag, null, null);

		List<Predicate<HtmlElement>> predicates = [];
		int? posIndex = null;

		string remaining = step[bracketPos..];
		Regex predRegex = new(@"\[([^\]]*)\]");

		foreach (Match m in predRegex.Matches(remaining))
		{
			string inner = m.Groups[1].Value.Trim();

			if (int.TryParse(inner, out int idx))
			{
				posIndex = idx;
				continue;
			}

			Regex attrRegex = new(@"^@(?<attr>[\w\-:]+)(?:\s*(?<op>[~|^$*]?=)\s*['""](?<val>[^'""]*)['""])?$");
			Match am = attrRegex.Match(inner);

			if (am.Success)
			{
				string attr = am.Groups["attr"].Value;
				string op = am.Groups["op"].Value;
				string val = am.Groups["val"].Value;
				bool hasVal = am.Groups["op"].Success;

				predicates.Add(el =>
				{
					if (!el.HasAttribute(attr)) return false;
					if (!hasVal) return true;

					string attrVal = el.GetAttribute(attr) ?? "";

					return op switch
					{
						"=" => attrVal.Equals(val, StringComparison.OrdinalIgnoreCase),
						"~=" => attrVal.Split(' ').Contains(val, StringComparer.OrdinalIgnoreCase),
						"|=" => attrVal == val || attrVal.StartsWith(val + "-", StringComparison.OrdinalIgnoreCase),
						"^=" => attrVal.StartsWith(val, StringComparison.OrdinalIgnoreCase),
						"$=" => attrVal.EndsWith(val, StringComparison.OrdinalIgnoreCase),
						"*=" => attrVal.Contains(val, StringComparison.OrdinalIgnoreCase),
						_ => false,
					};
				});
			}

			Regex textRegex = new(@"^text\(\)\s*=\s*['""]([^'""]*)['""]$");
			Match tm = textRegex.Match(inner);
			if (!tm.Success) continue;
			{
				string textVal = tm.Groups[1].Value;
				predicates.Add(el => el.InnerText.Trim()
					.Equals(textVal, StringComparison.OrdinalIgnoreCase));
			}
		}

		Predicate<HtmlElement>? combined = predicates.Count == 0
			? null
			: el => predicates.All(p => p(el));

		return (tag, combined, posIndex);
	}

	private static bool MatchTag(HtmlElement el, string tag) =>
		tag == "*" || el.TagName.Equals(tag, StringComparison.OrdinalIgnoreCase);
}

internal enum CssCombinator
{
	Descendant,
	Child,
	Adjacent,
	Sibling,
}

internal sealed class CssSimpleFilter
{
	public string? Tag { get; init; }
	public string? Id { get; init; }
	public List<string> Classes { get; init; } = [];
	public List<CssAttrFilter> Attrs { get; init; } = [];
	public List<CssPseudo> Pseudos { get; init; } = [];

	public bool Matches(HtmlElement el)
	{
		if (Tag is not null && Tag != "*" &&
		    !el.TagName.Equals(Tag, StringComparison.OrdinalIgnoreCase))
			return false;

		if (Id is not null &&
		    !string.Equals(el.Id, Id, StringComparison.OrdinalIgnoreCase))
			return false;

		foreach (string cls in Classes)
			if (!el.HasClass(cls)) return false;

		foreach (CssAttrFilter af in Attrs)
			if (!af.Matches(el)) return false;

		foreach (CssPseudo ps in Pseudos)
			if (!ps.Matches(el)) return false;

		return true;
	}
}

internal sealed class CssAttrFilter(string attr, string op, string value)
{
	public bool Matches(HtmlElement el)
	{
		if (!el.HasAttribute(attr)) return false;
		if (op == "") return true;

		string attrVal = el.GetAttribute(attr) ?? "";

		return op switch
		{
			"=" => attrVal.Equals(value, StringComparison.OrdinalIgnoreCase),
			"~=" => attrVal.Split(' ', StringSplitOptions.RemoveEmptyEntries)
						   .Contains(value, StringComparer.OrdinalIgnoreCase),
			"|=" => attrVal.Equals(value, StringComparison.OrdinalIgnoreCase) ||
					attrVal.StartsWith(value + "-", StringComparison.OrdinalIgnoreCase),
			"^=" => attrVal.StartsWith(value, StringComparison.OrdinalIgnoreCase),
			"$=" => attrVal.EndsWith(value, StringComparison.OrdinalIgnoreCase),
			"*=" => attrVal.Contains(value, StringComparison.OrdinalIgnoreCase),
			_ => false,
		};
	}
}

internal sealed class CssPseudo(string name, string arg = "")
{
	private readonly string _name = name.ToLowerInvariant();
	private readonly string _arg = arg.Trim();

	public bool Matches(HtmlElement el)
	{
		List<HtmlElement> siblings = el.Parent is HtmlElement p
			? p.Children.ToList()
			: [];

		int index1Based = siblings.IndexOf(el) + 1;

		return _name switch
		{
			"first-child" => index1Based == 1,
			"last-child" => index1Based == siblings.Count,
			"only-child" => siblings.Count == 1,
			"empty" => !el.Children.Any() && string.IsNullOrWhiteSpace(el.InnerText),
			"nth-child" => MatchNth(_arg, index1Based),
			"nth-last-child" => MatchNth(_arg, siblings.Count - index1Based + 1),
			"not" => MatchNot(el, _arg),
			_ => false,
		};
	}

	private static bool MatchNth(string expr, int n)
	{
		expr = expr.Trim().ToLowerInvariant();

		switch (expr)
		{
			case "odd":
				return n % 2 != 0;
			case "even":
				return n % 2 == 0;
		}

		if (int.TryParse(expr, out int exact)) return n == exact;

		Match m = Regex.Match(expr, @"^(?<a>-?\d*)n(?:\+(?<b>\d+))?$");
		if (!m.Success) return false;

		string aStr = m.Groups["a"].Value;
		int a = aStr is "" or "+" ? 1 : aStr == "-" ? -1 : int.Parse(aStr);
		int b = m.Groups["b"].Success ? int.Parse(m.Groups["b"].Value) : 0;

		if (a == 0) return n == b;
		int rem = n - b;
		return rem >= 0 && rem % a == 0;
	}

	private static bool MatchNot(HtmlElement el, string innerSelector)
	{
		List<CssSelectorSequence> seqs = CssSelectorParser.ParseSequences(innerSelector);
		return !seqs.Any(seq => seq.Last().Filter.Matches(el));
	}
}

internal sealed class CssSelectorStep(CssCombinator combinator, CssSimpleFilter filter)
{
	public CssCombinator Combinator { get; } = combinator;
	public CssSimpleFilter Filter { get; } = filter;
}

internal sealed class CssSelectorSequence(List<CssSelectorStep> steps)
{
	public CssSelectorStep Last() => steps[^1];

	public bool Matches(HtmlElement el)
	{
		HtmlElement current = el;

		for (int i = steps.Count - 1; i >= 0; i--)
		{
			CssSelectorStep step = steps[i];

			if (!step.Filter.Matches(current)) return false;

			if (i == 0) break;

			CssSelectorStep prevStep = steps[i - 1];

			switch (step.Combinator)
			{
				case CssCombinator.Descendant:
					{
						HtmlElement? ancestor = current.Parent as HtmlElement;
						while (ancestor is not null && !prevStep.Filter.Matches(ancestor))
							ancestor = ancestor.Parent as HtmlElement;
						if (ancestor is null) return false;
						current = ancestor;
						i--;
						break;
					}

				case CssCombinator.Child:
					{
						HtmlElement? parent = current.Parent as HtmlElement;
						if (parent is null || !prevStep.Filter.Matches(parent)) return false;
						current = parent;
						i--;
						break;
					}

				case CssCombinator.Adjacent:
					{
						if (current.Parent is not HtmlElement par) return false;
						List<HtmlElement> sibs = par.Children.ToList();
						int idx = sibs.IndexOf(current);
						if (idx <= 0) return false;
						HtmlElement prev = sibs[idx - 1];
						if (!prevStep.Filter.Matches(prev)) return false;
						current = prev;
						i--;
						break;
					}

				case CssCombinator.Sibling:
					{
						if (current.Parent is not HtmlElement par) return false;
						List<HtmlElement> sibs = par.Children.ToList();
						int idx = sibs.IndexOf(current);
						HtmlElement? matchedSib = sibs
							.Take(idx)
							.LastOrDefault(s => prevStep.Filter.Matches(s));
						if (matchedSib is null) return false;
						current = matchedSib;
						i--;
						break;
					}
			}
		}

		return true;
	}
}

internal static class CssSelectorParser
{
	public static List<CssSelectorSequence> ParseSequences(string selector)
	{
		List<string> parts = SplitOnComma(selector);
		return parts.Select(p => ParseSingleSequence(p.Trim())).ToList();
	}

	private static List<string> SplitOnComma(string s)
	{
		List<string> parts = [];
		int depth = 0;
		int start = 0;

		for (int i = 0; i < s.Length; i++)
		{
			char c = s[i];
			switch (c)
			{
				case '(':
				case '[':
					depth++;
					break;
				case ')':
				case ']':
					depth--;
					break;
				case ',' when depth == 0:
					parts.Add(s[start..i]);
					start = i + 1;
					break;
			}
		}

		parts.Add(s[start..]);
		return parts;
	}

	private static CssSelectorSequence ParseSingleSequence(string selector)
	{
		List<CssSelectorStep> steps = [];
		int pos = 0;

		CssCombinator nextCombinator = CssCombinator.Descendant;

		while (pos < selector.Length)
		{
			bool hadWhitespace = false;
			while (pos < selector.Length && char.IsWhiteSpace(selector[pos]))
			{
				hadWhitespace = true;
				pos++;
			}

			if (pos >= selector.Length) break;

			char ch = selector[pos];
			if (ch is '>' or '+' or '~')
			{
				nextCombinator = ch switch
				{
					'>' => CssCombinator.Child,
					'+' => CssCombinator.Adjacent,
					'~' => CssCombinator.Sibling,
					_ => CssCombinator.Descendant,
				};
				pos++;

				while (pos < selector.Length && char.IsWhiteSpace(selector[pos]))
					pos++;
			}
			else if (hadWhitespace && steps.Count > 0)
			{
				nextCombinator = CssCombinator.Descendant;
			}

			CssSimpleFilter filter = ParseSimpleFilter(selector, ref pos);
			steps.Add(new CssSelectorStep(nextCombinator, filter));
			nextCombinator = CssCombinator.Descendant;
		}

		return new CssSelectorSequence(steps);
	}

	private static CssSimpleFilter ParseSimpleFilter(string s, ref int pos)
	{
		string? tag = null;
		string? id = null;
		List<string> classes = [];
		List<CssAttrFilter> attrs = [];
		List<CssPseudo> pseudos = [];

		while (pos < s.Length)
		{
			char c = s[pos];

			if (char.IsWhiteSpace(c) || c is '>' or '+' or ',' or '~')
				break;

			if (c == '*' || char.IsLetter(c) || c == '_' || c == '-')
			{
				int start = pos;
				while (pos < s.Length && IsNameChar(s[pos]))
					pos++;
				tag = s[start..pos];
			}
			else switch (c)
			{
				case '#':
				{
					pos++;
					int start = pos;
					while (pos < s.Length && IsNameChar(s[pos]))
						pos++;
					id = s[start..pos];
					break;
				}
				case '.':
				{
					pos++;
					int start = pos;
					while (pos < s.Length && IsNameChar(s[pos]))
						pos++;
					classes.Add(s[start..pos]);
					break;
				}
				case '[':
					attrs.Add(ParseAttrFilter(s, ref pos));
					break;
				case ':':
					pseudos.Add(ParsePseudo(s, ref pos));
					break;
				default:
					pos++;
					break;
			}
		}

		return new CssSimpleFilter
		{
			Tag = tag,
			Id = id,
			Classes = classes,
			Attrs = attrs,
			Pseudos = pseudos,
		};
	}

	private static CssAttrFilter ParseAttrFilter(string s, ref int pos)
	{
		pos++;
		int start = pos;

		while (pos < s.Length && s[pos] != '=' && s[pos] != ']' && !IsOpStart(s[pos]))
			pos++;
		string attr = s[start..pos].Trim();

		if (pos >= s.Length || s[pos] == ']')
		{
			if (pos < s.Length) pos++;
			return new CssAttrFilter(attr, "", "");
		}

		string op;
		if (pos + 1 < s.Length && s[pos + 1] == '=')
		{
			op = s[pos..(pos + 2)];
			pos += 2;
		}
		else
		{
			op = "=";
			pos++;
		}

		string value;
		if (pos < s.Length && (s[pos] == '"' || s[pos] == '\''))
		{
			char q = s[pos++];
			int vstart = pos;
			while (pos < s.Length && s[pos] != q) pos++;
			value = s[vstart..pos];
			if (pos < s.Length) pos++;
		}
		else
		{
			int vstart = pos;
			while (pos < s.Length && s[pos] != ']') pos++;
			value = s[vstart..pos].Trim();
		}

		if (pos < s.Length && s[pos] == ']') pos++;

		return new CssAttrFilter(attr, op, value);
	}

	private static CssPseudo ParsePseudo(string s, ref int pos)
	{
		pos++;
		if (pos < s.Length && s[pos] == ':') pos++;

		int start = pos;
		while (pos < s.Length && IsNameChar(s[pos]))
			pos++;
		string name = s[start..pos];

		string arg = "";
		if (pos < s.Length && s[pos] == '(')
		{
			pos++;
			int depth = 1;
			int astart = pos;
			while (pos < s.Length && depth > 0)
			{
				switch (s[pos])
				{
					case '(':
						depth++;
						break;
					case ')':
						depth--;
						break;
				}

				if (depth > 0) pos++;
				else pos++;
			}
			arg = s[astart..(pos - 1)];
		}

		return new CssPseudo(name, arg);
	}

	private static bool IsNameChar(char c) =>
		char.IsLetterOrDigit(c) || c == '-' || c == '_';

	private static bool IsOpStart(char c) =>
		c is '~' or '|' or '^' or '$' or '*';
}

public partial class HtmlDocument
{
	public IEnumerable<HtmlElement> QuerySelectorAll(string cssSelector)
	{
		List<CssSelectorSequence> sequences =
			CssSelectorParser.ParseSequences(cssSelector);

		return All.Where(el => sequences.Any(seq => seq.Matches(el)));
	}

	public HtmlElement? QuerySelector(string cssSelector) =>
		QuerySelectorAll(cssSelector).FirstOrDefault();
}

public static class HtmlElementQueryExtensions
{
	extension(HtmlElement root)
	{
		public IEnumerable<HtmlElement> QuerySelectorAll(string cssSelector)
		{
			List<CssSelectorSequence> sequences =
				CssSelectorParser.ParseSequences(cssSelector);

			return root.Descendants()
				.Where(el => sequences.Any(seq => seq.Matches(el)));
		}

		public HtmlElement? QuerySelector(string cssSelector) => root.QuerySelectorAll(cssSelector).FirstOrDefault();

		public IEnumerable<HtmlElement> Descendants() => root.Children.SelectMany(c => c.AsSelfAndDescendants());

		public IEnumerable<HtmlElement> AsSelfAndDescendants() => new[] { root }.Concat(root.Descendants());

		public IEnumerable<HtmlElement> GetElementsByTagName(string tagName) => root.QuerySelectorAll(tagName);
	}

	extension(string input)
	{
		public string HtmlDecode() => System.Web.HttpUtility.HtmlDecode(input);
	}
}