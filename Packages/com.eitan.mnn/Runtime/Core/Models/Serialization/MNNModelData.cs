using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace MNN.Unity
{
    // Model metadata includes dictionaries and nested arrays unsupported by JsonUtility.
    internal static class MNNModelData
    {
        internal static object Read(string path) => Parse(File.ReadAllText(path));
        internal static object Parse(string text) => new Reader(text).Read();
        internal static Dictionary<string, object> Object(object value) => value as Dictionary<string, object> ?? throw new InvalidDataException("Expected JSON object.");
        internal static List<object> Array(object value) => value as List<object> ?? throw new InvalidDataException("Expected JSON array.");
        internal static int Integer(object value)
        {
            double number = Number(value);
            if (number != Math.Truncate(number) || number < int.MinValue || number > int.MaxValue)
                throw new InvalidDataException("Expected int32 JSON number.");
            return (int)number;
        }

        internal static double Number(object value) => value is double number && !double.IsNaN(number) && !double.IsInfinity(number) ? number : throw new InvalidDataException("Expected finite JSON number.");
        internal static object Get(object value, string key) => Object(value).TryGetValue(key, out var result) ? result : throw new InvalidDataException("Missing model metadata: " + key);
        internal static int[] Shape(object value) => Array(value).ConvertAll(Integer).ToArray();
        internal static float[] Floats(object value)
        {
            var values = new List<float>();
            Flatten(value, values);
            return values.ToArray();
        }

        private static void Flatten(object value, List<float> values)
        {
            if (value is List<object> list)
            {
                foreach (var item in list)
                    Flatten(item, values);
            }
            else
            {
                float number = (float)Number(value);
                if (float.IsInfinity(number))
                    throw new InvalidDataException("Model value exceeds float32.");
                values.Add(number);
            }
        }

        private sealed class Reader
        {
            private readonly string _text;
            private int _index;
            internal Reader(string text)
            {
                _text = text ?? throw new ArgumentNullException(nameof(text));
            }

            internal object Read()
            {
                var result = Value(0);
                Space();
                if (_index != _text.Length)
                    throw Error();
                return result;
            }

            private InvalidDataException Error() => new InvalidDataException("Invalid model JSON at character " + _index + ".");
            private void Space()
            {
                while (_index < _text.Length && char.IsWhiteSpace(_text[_index]))
                    ++_index;
            }

            private bool Take(char value)
            {
                Space();
                if (_index < _text.Length && _text[_index] == value)
                {
                    ++_index;
                    return true;
                }

                return false;
            }

            private object Value(int depth)
            {
                if (depth > 64)
                    throw Error();
                Space();
                if (_index >= _text.Length)
                    throw Error();
                if (_text[_index] == '"')
                    return String();
                if (Take('{'))
                {
                    var result = new Dictionary<string, object>();
                    if (Take('}'))
                        return result;
                    do
                    {
                        Space();
                        string key = String();
                        if (!Take(':') || result.ContainsKey(key))
                            throw Error();
                        result[key] = Value(depth + 1);
                    }
                    while (Take(','));
                    if (!Take('}'))
                        throw Error();
                    return result;
                }

                if (Take('['))
                {
                    var result = new List<object>();
                    if (Take(']'))
                        return result;
                    do
                    {
                        result.Add(Value(depth + 1));
                    }
                    while (Take(','));
                    if (!Take(']'))
                        throw Error();
                    return result;
                }

                foreach (var literal in new[]{"true", "false", "null"})
                    if (_text.Length - _index >= literal.Length && string.CompareOrdinal(_text, _index, literal, 0, literal.Length) == 0)
                    {
                        _index += literal.Length;
                        return literal == "null" ? null : (object)(literal == "true");
                    }

                int start = _index;
                if (_text[_index] == '-')
                    ++_index;
                if (_index >= _text.Length)
                    throw Error();
                if (_text[_index] == '0')
                    ++_index;
                else
                {
                    if (_text[_index] < '1' || _text[_index] > '9')
                        throw Error();
                    while (_index < _text.Length && _text[_index] >= '0' && _text[_index] <= '9')
                        ++_index;
                }

                if (_index < _text.Length && _text[_index] == '.')
                {
                    ++_index;
                    Digits();
                }

                if (_index < _text.Length && (_text[_index] == 'e' || _text[_index] == 'E'))
                {
                    ++_index;
                    if (_index < _text.Length && (_text[_index] == '+' || _text[_index] == '-'))
                        ++_index;
                    Digits();
                }

                if (!double.TryParse(_text.Substring(start, _index - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || double.IsInfinity(number) || double.IsNaN(number))
                    throw Error();
                return number;
            }

            private void Digits()
            {
                int start = _index;
                while (_index < _text.Length && _text[_index] >= '0' && _text[_index] <= '9')
                    ++_index;
                if (_index == start)
                    throw Error();
            }

            private string String()
            {
                if (_index >= _text.Length || _text[_index++] != '"')
                    throw Error();
                var result = new StringBuilder();
                while (_index < _text.Length)
                {
                    char c = _text[_index++];
                    if (c == '"')
                        return result.ToString();
                    if (c < 32)
                        throw Error();
                    if (c != '\\')
                    {
                        result.Append(c);
                        continue;
                    }

                    if (_index >= _text.Length)
                        throw Error();
                    c = _text[_index++];
                    switch (c)
                    {
                        case '"':
                        case '\\':
                        case '/':
                            result.Append(c);
                            break;
                        case 'b':
                            result.Append('\b');
                            break;
                        case 'f':
                            result.Append('\f');
                            break;
                        case 'n':
                            result.Append('\n');
                            break;
                        case 'r':
                            result.Append('\r');
                            break;
                        case 't':
                            result.Append('\t');
                            break;
                        case 'u':
                            if (_text.Length - _index < 4 || !ushort.TryParse(_text.Substring(_index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                                throw Error();
                            result.Append((char)code);
                            _index += 4;
                            break;
                        default:
                            throw Error();
                    }
                }

                throw Error();
            }
        }
    }
}
