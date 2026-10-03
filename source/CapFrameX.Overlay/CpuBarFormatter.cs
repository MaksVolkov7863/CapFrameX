using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Overlay
{
    /// <summary>
    /// Formats per-thread and per-core CPU loads into compact, informative Unicode bar graphs (столбики)
    /// for rendering in CapFrameX OSD (Hook-Free DWM and In-Game Hook DirectX 11/12 &amp; Vulkan).
    /// </summary>
    public static class CpuBarFormatter
    {
        // Unicode Lower One Eighth to Full Block (U+2581 .. U+2588)
        private static readonly char[] BarGlyphs = new[]
        {
            ' ', // U+2581: Lower 1/8 block (used for 0-6% so an idle/unloaded thread is clearly visible as a baseline tick)
            ' ', // U+2581: Lower 1/8 block (7-18%)
            '▂', // U+2582: Lower 1/4 block (19-31%)
            '▃', // U+2583: Lower 3/8 block (32-44%)
            '▄', // U+2584: Lower 1/2 block (45-56%)
            '▅', // U+2585: Lower 5/8 block (57-69%)
            '▆', // U+2586: Lower 3/4 block (70-81%)
            '▇', // U+2587: Lower 7/8 block (82-93%)
            '█'  // U+2588: Full block      (94-100%)
        };

        /// <summary>
        /// Converts a load percentage (0.0 to 100.0) into a corresponding vertical bar glyph.
        /// </summary>
        public static char GetBarGlyph(double loadPercent)
        {
            if (double.IsNaN(loadPercent) || loadPercent <= 0.0)
                return ' ';
            if (loadPercent >= 94.0)
                return '█';

            int index = (int)Math.Round((loadPercent / 100.0) * 8.0, MidpointRounding.AwayFromZero);
            if (index < 0) index = 0;
            if (index > 8) index = 8;
            return BarGlyphs[index];
        }

        /// <summary>
        /// Formats thread loads into a compact bar string with SMT/HT thread pairs.
        /// Guaranteed to fit within in-game OSD shared memory limit (63 UTF-8 bytes).
        /// </summary>
        public static string FormatThreadBars(IReadOnlyList<double> threadLoads)
        {
            if (threadLoads == null || threadLoads.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            int count = threadLoads.Count;

            if (count <= 16)
            {
                // Up to 16 threads (e.g. 6C/12T, 8C/16T):
                // Pair threads by core with a space between cores: "▅█ ▃▂ ▇▄ █ "
                for (int i = 0; i < count; i++)
                {
                    sb.Append(GetBarGlyph(threadLoads[i]));
                    if (i % 2 == 1 && i < count - 1)
                    {
                        sb.Append(' ');
                    }
                }
            }
            else if (count <= 20)
            {
                // 18-20 threads: omit spaces between pairs to strictly stay under the 63-byte UTF-8 limit
                for (int i = 0; i < count; i++)
                {
                    sb.Append(GetBarGlyph(threadLoads[i]));
                }
            }
            else
            {
                // > 20 threads (e.g. 24 or 32 threads on high-end desktop/workstation):
                // Aggregate each physical core pair (max load of the 2 threads) so all cores fit cleanly
                int coreCount = (count + 1) / 2;
                for (int c = 0; c < coreCount; c++)
                {
                    int t1 = c * 2;
                    int t2 = t1 + 1;
                    double load = threadLoads[t1];
                    if (t2 < count && threadLoads[t2] > load)
                    {
                        load = threadLoads[t2];
                    }

                    sb.Append(GetBarGlyph(load));
                    // Add a separator space every 4 cores for readability
                    if (c % 4 == 3 && c < coreCount - 1)
                    {
                        sb.Append(' ');
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Formats physical core loads into a compact bar string (one bar per physical core).
        /// </summary>
        public static string FormatCoreBars(IReadOnlyList<double> coreLoads)
        {
            if (coreLoads == null || coreLoads.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            int count = Math.Min(coreLoads.Count, 20); // Cap at 20 cores to fit SHM limit
            for (int i = 0; i < count; i++)
            {
                sb.Append(GetBarGlyph(coreLoads[i]));
                // Space every 4 cores for readability
                if (i % 4 == 3 && i < count - 1)
                {
                    sb.Append(' ');
                }
            }
            return sb.ToString();
        }
    }
}
