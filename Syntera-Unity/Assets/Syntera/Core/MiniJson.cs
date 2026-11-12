using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Syntera.Core
{
    // Minimal strict JSON reader for authored campaign data. Produces
    // Dictionary<string, object>, List<object>, string, double, bool or null.
    // Kept dependency-free so the rules core compiles without UnityEngine.
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            if (json == null) throw new ArgumentNullException("json");
            var parser = new Parser(json);
            object value = parser.ParseValue();
            parser.SkipWhitespace();
            if (!parser.AtEnd) throw parser.Error("Unexpected trailing content");
            return value;
        }

        // Writes Dictionary<string, object>, IList, string, int, long, double, float, bool or null.
        public static string Serialize(object value)
        {
            var sb = new StringBuilder();
            Write(sb, value);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object value)
        {
            if (value == null) { sb.Append("null"); return; }
            var s = value as string;
            if (s != null) { WriteString(sb, s); return; }
            if (value is bool) { sb.Append((bool)value ? "true" : "false"); return; }
            if (value is int || value is long) { sb.Append(Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)); return; }
            if (value is double || value is float)
            {
                double d = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (double.IsNaN(d) || double.IsInfinity(d)) throw new ArgumentException("JSON cannot represent " + d);
                sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            var dict = value as IDictionary<string, object>;
            if (dict != null)
            {
                sb.Append('{');
                bool first = true;
                foreach (var kv in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteString(sb, kv.Key);
                    sb.Append(':');
                    Write(sb, kv.Value);
                }
                sb.Append('}');
                return;
            }
            var list = value as System.Collections.IEnumerable;
            if (list != null)
            {
                sb.Append('[');
                bool first = true;
                foreach (var item in list)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    Write(sb, item);
                }
                sb.Append(']');
                return;
            }
            throw new ArgumentException("Cannot serialize " + value.GetType().Name);
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        private sealed class Parser
        {
            private readonly string text;
            private int pos;

            public Parser(string text) { this.text = text; }

            public bool AtEnd { get { return pos >= text.Length; } }

            public FormatException Error(string message)
            {
                return new FormatException(message + " at character " + pos);
            }

            public void SkipWhitespace()
            {
                while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;
            }

            public object ParseValue()
            {
                SkipWhitespace();
                if (AtEnd) throw Error("Unexpected end of JSON");
                char c = text[pos];
                if (c == '{') return ParseObject();
                if (c == '[') return ParseArray();
                if (c == '"') return ParseString();
                if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();
                if (Match("true")) return true;
                if (Match("false")) return false;
                if (Match("null")) return null;
                throw Error("Unexpected character '" + c + "'");
            }

            private bool Match(string literal)
            {
                if (string.CompareOrdinal(text, pos, literal, 0, literal.Length) != 0) return false;
                pos += literal.Length;
                return true;
            }

            private Dictionary<string, object> ParseObject()
            {
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                pos++;
                SkipWhitespace();
                if (pos < text.Length && text[pos] == '}') { pos++; return result; }
                while (true)
                {
                    SkipWhitespace();
                    if (AtEnd || text[pos] != '"') throw Error("Expected property name");
                    string key = ParseString();
                    SkipWhitespace();
                    if (AtEnd || text[pos] != ':') throw Error("Expected ':'");
                    pos++;
                    if (result.ContainsKey(key)) throw Error("Duplicate property '" + key + "'");
                    result[key] = ParseValue();
                    SkipWhitespace();
                    if (AtEnd) throw Error("Unterminated object");
                    if (text[pos] == ',') { pos++; continue; }
                    if (text[pos] == '}') { pos++; return result; }
                    throw Error("Expected ',' or '}'");
                }
            }

            private List<object> ParseArray()
            {
                var result = new List<object>();
                pos++;
                SkipWhitespace();
                if (pos < text.Length && text[pos] == ']') { pos++; return result; }
                while (true)
                {
                    result.Add(ParseValue());
                    SkipWhitespace();
                    if (AtEnd) throw Error("Unterminated array");
                    if (text[pos] == ',') { pos++; continue; }
                    if (text[pos] == ']') { pos++; return result; }
                    throw Error("Expected ',' or ']'");
                }
            }

            private string ParseString()
            {
                var sb = new StringBuilder();
                pos++;
                while (true)
                {
                    if (AtEnd) throw Error("Unterminated string");
                    char c = text[pos++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (AtEnd) throw Error("Unterminated escape");
                    char e = text[pos++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (pos + 4 > text.Length) throw Error("Bad unicode escape");
                            sb.Append((char)int.Parse(text.Substring(pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            pos += 4;
                            break;
                        default: throw Error("Bad escape '\\" + e + "'");
                    }
                }
            }

            private double ParseNumber()
            {
                int start = pos;
                if (text[pos] == '-') pos++;
                while (pos < text.Length && "0123456789.eE+-".IndexOf(text[pos]) >= 0) pos++;
                double value;
                if (!double.TryParse(text.Substring(start, pos - start), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                    throw Error("Bad number");
                return value;
            }
        }
    }

    // Typed accessors that fail loudly on malformed authored data.
    internal static class Json
    {
        public static Dictionary<string, object> Obj(object value, string context)
        {
            var d = value as Dictionary<string, object>;
            if (d == null) throw new FormatException(context + ": expected object");
            return d;
        }

        public static List<object> Arr(Dictionary<string, object> o, string key, string context)
        {
            object v;
            if (!o.TryGetValue(key, out v)) throw new FormatException(context + ": missing '" + key + "'");
            var a = v as List<object>;
            if (a == null) throw new FormatException(context + ": '" + key + "' must be an array");
            return a;
        }

        public static List<object> OptArr(Dictionary<string, object> o, string key)
        {
            object v;
            if (!o.TryGetValue(key, out v) || v == null) return new List<object>();
            var a = v as List<object>;
            if (a == null) throw new FormatException("'" + key + "' must be an array");
            return a;
        }

        public static string Str(Dictionary<string, object> o, string key, string context)
        {
            object v;
            if (!o.TryGetValue(key, out v) || !(v is string)) throw new FormatException(context + ": missing string '" + key + "'");
            return (string)v;
        }

        public static string OptStr(Dictionary<string, object> o, string key)
        {
            object v;
            return o.TryGetValue(key, out v) ? v as string : null;
        }

        public static int Int(Dictionary<string, object> o, string key, string context)
        {
            object v;
            if (!o.TryGetValue(key, out v) || !(v is double)) throw new FormatException(context + ": missing number '" + key + "'");
            double d = (double)v;
            if (d != Math.Floor(d)) throw new FormatException(context + ": '" + key + "' must be an integer");
            return (int)d;
        }

        public static bool Has(Dictionary<string, object> o, string key)
        {
            return o.ContainsKey(key);
        }
    }
}
