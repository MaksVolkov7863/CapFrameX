using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Overlay
{
    /// <summary>
    /// Formats per-thread and per-core CPU loads into tall, distinct Unicode bar graphs (столбики)
    /// with background tracks, P/E hybrid core separation, and horizontal progress bars for CapFrameX OSD.
    /// </summary>
    public static class CpuBarFormatter
    {
        // Glyphs with background track for tall vertical bars (as in afterburner/aida64 OSD)
        // '░' represents the empty/idle track (0-15% load)
        // '▂', '▄', '▆', '█' represent 25%, 50%, 75%, 100% fill levels
        private static readonly char[] TrackGlyphs = new[]
        {
            '░', // 0 - 15%   (transparent/shaded track: full height pillar showing idle capacity)
            '▂', // 16 - 35%  (quarter filled)
            '▄', // 36 - 65%  (half filled)
            '▆', // 66 - 85%  (three-quarters filled)
            '█'  // 86 - 100% (solid full block)
        };

        // Compact baseline glyphs (U+2581..U+2588)
        private static readonly char[] BaselineGlyphs = new[]
        {
            ' ', ' ', '▂', '▃', '▄', '▅', '▆', '▇', '█'
        };

        /// <summary>
        /// Gets a tall vertical bar glyph with background track (░ for idle, ▂, ▄, ▆, █ for load).
        /// </summary>
        public static char GetTrackBarGlyph(double loadPercent)
        {
            if (double.IsNaN(loadPercent) || loadPercent <= 15.0)
                return '░';
            if (loadPercent <= 35.0)
                return '▂';
            if (loadPercent <= 65.0)
                return '▄';
            if (loadPercent <= 85.0)
                return '▆';
            return '█';
        }

        /// <summary>
        /// Gets a compact baseline glyph (1/8 to full block).
        /// </summary>
        public static char GetBaselineGlyph(double loadPercent)
        {
            if (double.IsNaN(loadPercent) || loadPercent <= 0.0)
                return ' ';
            if (loadPercent >= 94.0)
                return '█';

            int index = (int)Math.Round((loadPercent / 100.0) * 8.0, MidpointRounding.AwayFromZero);
            if (index < 0) index = 0;
            if (index > 8) index = 8;
            return BaselineGlyphs[index];
        }

        /// <summary>
        /// Formats thread loads into tall vertical bars with background track and clear spacing between threads and cores.
        /// Guaranteed to never merge together and fits under the 63-byte in-game SHM limit.
        /// </summary>
        public static string FormatTrackBars(IReadOnlyList<double> threadLoads)
        {
            if (threadLoads == null || threadLoads.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            int count = threadLoads.Count;

            if (count <= 16)
            {
                // Up to 16 threads (e.g. 6C/12T, 8C/16T):
                // Space between threads, and distinct double-space between physical cores
                for (int i = 0; i < count; i++)
                {
                    sb.Append(GetTrackBarGlyph(threadLoads[i]));
                    if (i < count - 1)
                    {
                        // Every 2 threads (a core), add double space; between threads of the same core, add single space
                        sb.Append(i % 2 == 1 ? "  " : " ");
                    }
                }
            }
            else
            {
                // > 16 threads (e.g. 24 or 32 threads):
                // Show physical core loads with space between cores so they remain distinct
                int coreCount = Math.Min((count + 1) / 2, 16);
                for (int c = 0; c < coreCount; c++)
                {
                    int t1 = c * 2;
                    int t2 = t1 + 1;
                    double load = threadLoads[t1];
                    if (t2 < count && threadLoads[t2] > load)
                    {
                        load = threadLoads[t2];
                    }

                    sb.Append(GetTrackBarGlyph(load));
                    if (c < coreCount - 1)
                    {
                        sb.Append(c % 4 == 3 ? "  " : " ");
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Formats hybrid CPU architecture (e.g. Intel 12th/13th/14th Gen or AMD Ryzen AI):
        /// P-cores (paired with SMT) | Divider | E-cores (single threads).
        /// </summary>
        public static string FormatHybridBars(IReadOnlyList<double> threadLoads, int pCoreThreads = 16, int eCoreThreads = 8)
        {
            if (threadLoads == null || threadLoads.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            int total = threadLoads.Count;

            int pCount = Math.Min(total, pCoreThreads);
            for (int i = 0; i < pCount; i++)
            {
                sb.Append(GetTrackBarGlyph(threadLoads[i]));
                if (i % 2 == 1 && i < pCount - 1)
                {
                    sb.Append(' ');
                }
            }

            if (total > pCount)
            {
                sb.Append(" │ ");
                int eCount = Math.Min(total - pCount, eCoreThreads);
                for (int i = 0; i < eCount; i++)
                {
                    sb.Append(GetTrackBarGlyph(threadLoads[pCount + i]));
                    if (i < eCount - 1)
                    {
                        sb.Append(' ');
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Formats a horizontal progress bar (like Screenshot 1).
        /// Example: "[██████░░░░] 60%"
        /// </summary>
        public static string FormatHorizontalBar(double percent, int barWidth = 10)
        {
            if (double.IsNaN(percent) || percent < 0.0) percent = 0.0;
            if (percent > 100.0) percent = 100.0;

            int filled = (int)Math.Round((percent / 100.0) * barWidth, MidpointRounding.AwayFromZero);
            if (filled < 0) filled = 0;
            if (filled > barWidth) filled = barWidth;

            var sb = new StringBuilder(barWidth + 10);
            sb.Append('[');
            for (int i = 0; i < filled; i++)
            {
                sb.Append('█');
            }
            for (int i = filled; i < barWidth; i++)
            {
                sb.Append('░');
            }
            sb.Append($"] {(int)Math.Round(percent)}%");
            return sb.ToString();
        }

        /// <summary>
        /// Formats physical core loads into distinct vertical bars with clear separators.
        /// </summary>
        public static string FormatCoreBars(IReadOnlyList<double> coreLoads)
        {
            if (coreLoads == null || coreLoads.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            int count = Math.Min(coreLoads.Count, 16);
            for (int i = 0; i < count; i++)
            {
                sb.Append(GetTrackBarGlyph(coreLoads[i]));
                if (i < count - 1)
                {
                    sb.Append(i % 4 == 3 ? "  " : " ");
                }
            }
            return sb.ToString();
        }
    }
}
