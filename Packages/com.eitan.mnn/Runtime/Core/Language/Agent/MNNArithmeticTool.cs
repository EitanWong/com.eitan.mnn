using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public class MNNArithmeticTool : IMNNAgentTool
    {
        public string Name => "calculate";
        public string Description => "Exact decimal arithmetic. Input is only an expression with numbers, + - * / and parentheses, e.g. (2+3)*4. No code or units.";
        public bool RequiresNetwork => false;
        public Task<string> ExecuteAsync(string input, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(input) || input.Length > 128) throw new ArgumentException("Provide a bounded arithmetic expression.");
            try { return Task.FromResult(input + " = " + new Parser(input).Read().ToString(CultureInfo.InvariantCulture)); }
            catch (ArithmeticException error) { throw new ArgumentException("Arithmetic is outside the supported range.", error); }
        }

        private sealed class Parser
        {
            private readonly string _text;
            private int _position, _depth;
            internal Parser(string text) { _text = text.Replace('×', '*').Replace('÷', '/'); }
            internal decimal Read()
            {
                decimal value = Expression();
                SkipSpace();
                if (_position != _text.Length) throw new ArgumentException("Unexpected arithmetic input.");
                return value;
            }
            private void SkipSpace() { while (_position < _text.Length && char.IsWhiteSpace(_text[_position])) ++_position; }
            private bool Take(char value)
            {
                SkipSpace();
                if (_position >= _text.Length || _text[_position] != value) return false;
                ++_position; return true;
            }
            private decimal Expression()
            {
                decimal value = Term();
                while (true)
                {
                    if (Take('+')) value = checked(value + Term());
                    else if (Take('-')) value = checked(value - Term());
                    else return value;
                }
            }
            private decimal Term()
            {
                decimal value = Factor();
                while (true)
                {
                    if (Take('*')) value = checked(value * Factor());
                    else if (Take('/')) value /= Factor();
                    else return value;
                }
            }
            private decimal Factor()
            {
                if (++_depth > 16) throw new ArgumentException("Too many nested arithmetic operations.");
                decimal value;
                if (Take('-')) value = -Factor();
                else if (Take('+')) value = Factor();
                else if (Take('('))
                {
                    value = Expression();
                    if (!Take(')')) throw new ArgumentException("Missing closing parenthesis.");
                }
                else value = Number();
                --_depth;
                return value;
            }
            private decimal Number()
            {
                SkipSpace();
                int start = _position;
                while (_position < _text.Length && (_text[_position] >= '0' && _text[_position] <= '9' || _text[_position] == '.')) ++_position;
                if (!decimal.TryParse(_text.Substring(start, _position - start), NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out decimal value)) throw new ArgumentException("Expected a decimal number.");
                return value;
            }
        }
    }
}
