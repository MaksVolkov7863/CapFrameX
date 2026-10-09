using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Overlay
{
    /// <summary>
    /// Formats per-thread and per-core CPU loads into tall, distinct Unicode bar graphs (столбики)
    /// with vertical height steps, P/E hybrid core separation, and horizontal progress bars for CapFrameX OSD.
    /// </summary>
    public static class CpuBarFormatter
    {
        // 8 vertical pillar height levels (U+2581..U+2588)
        // ' ' represents strictly 0% or idle / offline (height 1/8 baseline)
        // '▂', '▃', '▄', '▅', '▆', '▇', '█' represent rising load up to 100% tall pillar
        private static readonly char[] PillarGlyphs = new[]
        {
            ' ', // 0%       (flat baseline mark)
            '▂', // 1 - 12%  (level 2 - clear visible column base even at low/desktop load)
            '▃', // 13 - 25% (level 3)
            '▄', // 26 - 38% (level 4)
            '▅', // 39 - 51% (level 5 - half pillar)
            '▆', // 52 - 64% (level 6)
            '▇', // 65 - 77% (level 7)
            '█'  // 78 - 100% (level 8 - tall solid pillar)
        };

        // Compact baseline glyphs (U+2581..U+2588)
        private static readonly char[] BaselineGlyphs = new[]
        {
            ' ', ' ', '▂', '▃', '▄', '▅', '▆', '▇', '█'
        };

        /// <summary>
        /// Gets a vertical pillar glyph with 8 height levels (1/8 to full tall block).
        /// At low desktop/game loads (1-12%), displays level 2 '▂' so pillars never collapse into flat lines.
        /// </summary>
        public static char GetTrackBarGlyph(double loadPercent)
        {
            if (double.IsNaN(loadPercent) || loadPercent <= 0.5)
                return ' ';
            if (loadPercent <= 12.0)
                return '▂';
            if (loadPercent <= 25.0)
                return '▃';
            if (loadPercent <= 38.0)
                return '▄';
            if (loadPercent <= 51.0)
                return '▅';
            if (loadPercent <= 64.0)
                return '▆';
            if (loadPercent <= 77.0)
                return '▇';
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
        /// Formats thread loads into distinct vertical bars with spacing between threads.
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
                // Up to 16 threads (e.g. 4C/8T, 6C/12T, 8C/16T):
                // 1 space between each thread so columns are clearly separated and don't merge.
                // 16 glyphs * 3 bytes + 15 spaces = 63 bytes max.
                for (int i = 0; i < count; i++)
                {
                    sb.Append(GetTrackBarGlyph(threadLoads[i]));
                    if (i < count - 1)
                    {
                        sb.Append(' ');
                    }
                }
            }
            else
            {
                // > 16 threads (e.g. 24 or 32 threads):
                // Show physical core loads (max load of thread pair) with space between cores
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
                        sb.Append(' ');
                    }
                }
            }

            return TruncateToUtf8Limit(sb.ToString(), 63);
        }

        /// <summary>
        /// Formats hybrid CPU architecture (e.g. Intel 12th/13th/14th Gen or AMD Ryzen AI):
        /// P-cores | Divider | E-cores.
        /// If there are no E-cores, only P-cores are formatted without divider.
        /// </summary>
        public static string FormatHybridBars(IReadOnlyList<double> pThreadLoads, IReadOnlyList<double> eThreadLoads)
        {
            if ((pThreadLoads == null || pThreadLoads.Count == 0) && (eThreadLoads == null || eThreadLoads.Count == 0))
                return string.Empty;

            var sb = new StringBuilder();

            if (pThreadLoads != null && pThreadLoads.Count > 0)
            {
                int pCount = Math.Min(pThreadLoads.Count, 16);
                for (int i = 0; i < pCount; i++)
                {
                    sb.Append(GetTrackBarGlyph(pThreadLoads[i]));
                    if (i < pCount - 1)
                    {
                        sb.Append(' ');
                    }
                }
            }

            if (eThreadLoads != null && eThreadLoads.Count > 0)
            {
                sb.Append(" │ ");
                int eCount = Math.Min(eThreadLoads.Count, 16);
                for (int i = 0; i < eCount; i++)
                {
                    sb.Append(GetTrackBarGlyph(eThreadLoads[i]));
                    if (i < eCount - 1)
                    {
                        sb.Append(' ');
                    }
                }
            }

            return TruncateToUtf8Limit(sb.ToString(), 63);
        }

        /// <summary>
        /// Formats hybrid CPU architecture from a single list by thread offset.
        /// </summary>
        public static string FormatHybridBars(IReadOnlyList<double> threadLoads, int pCoreThreads = 16, int eCoreThreads = 8)
        {
            if (threadLoads == null || threadLoads.Count == 0)
                return string.Empty;

            int pCount = Math.Min(threadLoads.Count, pCoreThreads);
            var pList = new List<double>();
            for (int i = 0; i < pCount; i++) pList.Add(threadLoads[i]);

            var eList = new List<double>();
            if (threadLoads.Count > pCount && eCoreThreads > 0)
            {
                int eCount = Math.Min(threadLoads.Count - pCount, eCoreThreads);
                for (int i = 0; i < eCount; i++) eList.Add(threadLoads[pCount + i]);
            }

            return FormatHybridBars(pList, eList);
        }

        /// <summary>
        /// Formats a horizontal progress bar with fixed visual width.
        /// Uses '█' for filled and '░' for unfilled (both have identical glyph width in OSD fonts),
        /// and pads the percentage to 3 digits so boundaries never jump.
        /// Example: "[█████░░░░░]  50%"
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
            int rounded = (int)Math.Round(percent);
            sb.Append($"] {rounded,3}%");
            return TruncateToUtf8Limit(sb.ToString(), 63);
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
                    sb.Append(' ');
                }
            }
            return TruncateToUtf8Limit(sb.ToString(), 63);
        }

        private static string TruncateToUtf8Limit(string text, int maxBytes = 63)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            if (Encoding.UTF8.GetByteCount(text) <= maxBytes)
                return text;

            var sb = new StringBuilder();
            int currentBytes = 0;
            foreach (char c in text)
            {
                int charBytes = Encoding.UTF8.GetByteCount(new[] { c });
                if (currentBytes + charBytes > maxBytes)
                    break;
                sb.Append(c);
                currentBytes += charBytes;
            }
            return sb.ToString();
        }
    }
}
