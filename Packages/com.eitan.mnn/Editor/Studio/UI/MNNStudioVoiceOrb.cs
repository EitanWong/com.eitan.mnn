using UnityEditor;
using UnityEngine;

namespace MNN.Unity.Editor.UI
{
    /// <summary>Procedural, reusable voice visualization. No per-frame textures or downloads.</summary>
    internal static class MNNStudioVoiceOrb
    {
        private static readonly Vector3[] Contour = new Vector3[97];
        internal static void Draw(Rect rect, MNNStudioVoiceState state, float level, double time, bool reducedMotion)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            Color old = Handles.color;
            var matrix = Handles.matrix;
            try
            {
                Handles.BeginGUI();
                float motion = reducedMotion ? 0 : 1;
                float pulse = motion * Mathf.Sin((float)time * 1.8f) * .025f;
                float radius = Mathf.Min(rect.width, rect.height) * (.27f + pulse + Mathf.Clamp01(level) * .09f * motion);
                Vector3 center = rect.center;
                Color baseColor = state == MNNStudioVoiceState.Error ? new Color(.85f, .35f, .3f) : state == MNNStudioVoiceState.Muted ? new Color(.45f, .5f, .57f) : new Color(.22f, .63f, .97f);
                for (int layer = 12; layer >= 0; --layer)
                {
                    float expansion = layer / 12f;
                    Handles.color = new Color(baseColor.r, baseColor.g, baseColor.b, .025f + (1 - expansion) * .05f);
                    Handles.DrawSolidDisc(center, Vector3.forward, radius * (1 + expansion * .28f));
                }

                Handles.color = baseColor;
                Handles.DrawSolidDisc(center, Vector3.forward, radius);
                for (int layer = 0; layer < 5; ++layer)
                {
                    float shift = motion * Mathf.Sin((float)time * .9f + layer) * radius * .12f;
                    Handles.color = new Color(.74f, .92f, 1, .12f);
                    Handles.DrawSolidDisc(center + new Vector3(shift, -radius * .15f, 0), Vector3.forward, radius * (1 - layer * .15f));
                }

                for (int i = 0; i < Contour.Length; ++i)
                {
                    float angle = i * Mathf.PI * 2 / (Contour.Length - 1);
                    float wave = reducedMotion ? 0 : Mathf.Sin(angle * 5 + (float)time * (state == MNNStudioVoiceState.Thinking ? 2.2f : 1.2f));
                    float r = radius * (.92f + wave * (.025f + level * .24f));
                    Contour[i] = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * r;
                }

                Handles.color = new Color(.9f, .97f, 1, .65f);
                Handles.DrawAAPolyLine(2, Contour);
                Handles.EndGUI();
            }
            finally
            {
                Handles.color = old;
                Handles.matrix = matrix;
            }
        }

        internal static void Meter(Rect rect, float level, double time, bool reducedMotion)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            for (int i = 0; i < 20; ++i)
            {
                float centerBias = 1f - Mathf.Abs(i - 9.5f) / 15f;
                float phase = reducedMotion ? 0 : Mathf.Sin((float)time * 18f + i * .83f) * .12f;
                float response = Mathf.Clamp01(level * Mathf.Max(.25f, centerBias) * (1f + phase));
                float height = 4 + response * (rect.height - 4);
                Color color = Color.Lerp(new Color(.34f, .43f, .53f, .55f), new Color(.16f, .62f, .98f, .98f), response);
                EditorGUI.DrawRect(new Rect(rect.x + i * rect.width / 20, rect.center.y - height / 2, rect.width / 20 - 3, height), color);
            }
        }
    }
}
