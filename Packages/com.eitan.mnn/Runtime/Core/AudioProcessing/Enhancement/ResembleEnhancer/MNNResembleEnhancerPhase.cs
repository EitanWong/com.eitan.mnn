// Copyright Naoki Shibata and contributors 2010 - 2025.
// Adapted from SLEEF sleefsimdsp.c / df.h, Boost Software License 1.0.
// See Documentation~/SleefLicense.md. Finite phase in [-pi, pi].
using System;

namespace MNN.Unity
{
    internal static class MNNResembleEnhancerPhase
    {
        private struct Pair
        {
            internal float High, Low;
            internal Pair(float high, float low) { High = high; Low = low; }
        }
        private static float Fma(float a, float b, float c) => (float)((double)a * b + c);
        private static Pair Add(float a, float b)
        {
            float sum = a + b;
            return new Pair(sum, (a - sum) + b);
        }
        private static Pair AddExact(float a, float b)
        {
            float sum = a + b, v = sum - a;
            return new Pair(sum, (a - (sum - v)) + (b - v));
        }
        private static Pair Add(Pair a, float b)
        {
            float sum = a.High + b;
            return new Pair(sum, ((a.High - sum) + b) + a.Low);
        }
        private static Pair AddExact(Pair a, float b)
        {
            float sum = a.High + b, v = sum - a.High;
            float t = (a.High - (sum - v)) + (b - v);
            return new Pair(sum, t + a.Low);
        }
        private static Pair Add(float a, Pair b)
        {
            float sum = a + b.High;
            return new Pair(sum, ((a - sum) + b.High) + b.Low);
        }
        private static Pair Square(Pair value)
        {
            float product = value.High * value.High;
            return new Pair(product, Fma(value.High + value.High, value.Low,
                Fma(value.High, value.High, -product)));
        }
        private static Pair Multiply(Pair a, Pair b)
        {
            float product = a.High * b.High;
            return new Pair(product, Fma(a.High, b.Low, Fma(a.Low, b.High,
                Fma(a.High, b.High, -product))));
        }
        private static float Product(Pair a, Pair b) =>
            Fma(a.High, b.High, Fma(a.Low, b.High, a.High * b.Low));
        private static float Polynomial(Pair reduced)
        {
            Pair square = Square(reduced);
            float value = 2.6083159809786593541503e-6f;
            value = Fma(value, square.High, -0.0001981069071916863322258f);
            value = Fma(value, square.High, 0.00833307858556509017944336f);
            Pair multiplier = Add(1f, Multiply(Add(-0.166666597127914428710938f,
                value * square.High), square));
            return Product(reduced, multiplier);
        }
        internal static float Sin(float phase)
        {
            float quadrant = (float)Math.Round(phase * (float)(1.0 / Math.PI));
            float high = Fma(quadrant, -3.1414794921875f, phase);
            Pair reduced = AddExact(high, quadrant * -0.00011315941810607910156f);
            reduced = Add(reduced, quadrant * -1.9841872589410058936e-9f);
            float result = Polynomial(reduced);
            if (((int)quadrant & 1) != 0) result = -result;
            return phase == 0 ? phase : result;
        }
        internal static float Cos(float phase)
        {
            float quadrant = Fma((float)Math.Round(Fma(phase, (float)(1.0 / Math.PI), -.5f)), 2f, 1f);
            Pair reduced = AddExact(phase, quadrant * (-3.1414794921875f * .5f));
            reduced = AddExact(reduced, quadrant * (-0.00011315941810607910156f * .5f));
            reduced = AddExact(reduced, quadrant * (-1.9841872589410058936e-9f * .5f));
            float result = Polynomial(reduced);
            return ((int)quadrant & 2) == 0 ? -result : result;
        }
    }
}
