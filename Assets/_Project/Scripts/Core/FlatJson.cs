using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CatMerge.Core
{
    /// <summary>
    /// Parses a flat JSON object of string values: <c>{ "key": "value", ... }</c>. That is all the localization
    /// files need, and JsonUtility can't read dictionaries. Anything else (numbers, nesting, duplicate keys) is an error.
    /// </summary>
    public static class FlatJson
    {
        public static Dictionary<string, string> Parse(string json)
        {
            var result = new Dictionary<string, string>();
            var reader = new Reader(json ?? string.Empty);

            reader.Expect('{');
            if (reader.TryConsume('}'))
            {
                reader.ExpectEnd();
                return result;
            }
            do
            {
                int keyPos = reader.Position;
                string key = reader.ReadString();
                reader.Expect(':');
                string value = reader.ReadString();
                if (result.ContainsKey(key)) throw reader.Error($"duplicate key \"{key}\"", keyPos);
                result.Add(key, value);
            } while (reader.TryConsume(','));
            reader.Expect('}');
            reader.ExpectEnd();
            return result;
        }

        private sealed class Reader
        {
            private readonly string _text;
            private int _pos;

            public Reader(string text)
            {
                _text = text;
                if (_text.Length > 0 && _text[0] == '﻿') _pos = 1; // BOM
            }

            public int Position => _pos;

            public void Expect(char c)
            {
                if (!TryConsume(c)) throw Error($"expected '{c}'", _pos);
            }

            public bool TryConsume(char c)
            {
                SkipWhitespace();
                if (_pos >= _text.Length || _text[_pos] != c) return false;
                _pos++;
                return true;
            }

            public void ExpectEnd()
            {
                SkipWhitespace();
                if (_pos < _text.Length) throw Error("unexpected text after the closing '}'", _pos);
            }

            public string ReadString()
            {
                SkipWhitespace();
                if (_pos >= _text.Length || _text[_pos] != '"') throw Error("expected a string in double quotes", _pos);
                int start = _pos++;
                var sb = new StringBuilder();
                while (_pos < _text.Length)
                {
                    char c = _text[_pos++];
                    if (c == '"') return sb.ToString();
                    if (c == '\n' || c == '\r') throw Error("line break inside a string (use \\n)", _pos - 1);
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }
                    if (_pos >= _text.Length) break;
                    char e = _text[_pos++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            if (_pos + 4 > _text.Length ||
                                !int.TryParse(_text.Substring(_pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                            {
                                throw Error("bad \\u escape", _pos - 2);
                            }
                            sb.Append((char)code);
                            _pos += 4;
                            break;
                        default:
                            throw Error($"unknown escape \\{e}", _pos - 2);
                    }
                }
                throw Error("unterminated string", start);
            }

            private void SkipWhitespace()
            {
                while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos])) _pos++;
            }

            public FormatException Error(string message, int pos)
            {
                int line = 1, column = 1;
                for (int i = 0; i < pos && i < _text.Length; i++)
                {
                    if (_text[i] == '\n')
                    {
                        line++;
                        column = 1;
                    }
                    else
                    {
                        column++;
                    }
                }
                return new FormatException($"JSON: {message} at line {line}, column {column}.");
            }
        }
    }
}
