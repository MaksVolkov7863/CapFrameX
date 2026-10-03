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
        // ' ' represents idle / baseline (height 1/8)
        // '▂', '▃', '▄', '▅', '▆', '▇', '█' represent rising load up to 100% tall pillar
        private static readonly char[] PillarGlyphs = new[]
        {
            ' ', // 0 - 5%   (bottom baseline mark)
            '▂', // 6 - 18%  (level 2)
            '▃', // 19 - 31% (level 3)
            '▄', // 32 - 45% (level 4 - half)
            '▅', // 46 - 58% (level 5)
            '▆', // 59 - 72% (level 6)
            '▇', // 73 - 86% (level 7)
            '█'  // 87 - 100% (level 8 - tall solid block)
        };

        // Compact baseline glyphs (U+2581..U+2588)
        private static readonly char[] BaselineGlyphs = new[]
        {
            ' ', ' ', '▂', '▃', '▄', '▅', '▆', '▇', '█'
        };

        /// <summary>
        /// Gets a vertical pillar glyph with 8 height levels (1/8 to full tall block).
        /// Never returns full-height checkerboard shade '░'.
        /// </summary>
        public static char GetTrackBarGlyph(double loadPercent)
        {
            if (double.IsNaN(loadPercent) || loadPercent <= 5.0)
                return ' ';
            if (loadPercent <= 18.0)
                return '▂';
            if (loadPercent <= 31.0)
                return '▃';
            if (loadPercent <= 45.0)
                return '▄';
            if (loadPercent <= 58.0)
                return '▅';
            if (loadPercent <= 72.0)
                return '▆';
            if (loadPercent <= 86.0)
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
        /// </summary>
        public static string FormatHybridBars(IReadOnlyList<double> threadLoads, int pCoreThreads = 16, int eCoreThreads = 8)
        {
            if (threadLoads == null || threadLoads.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            int total = threadLoads.Count;

            // Keep within 63 UTF-8 bytes limit
            int pCount = Math.Min(Math.Min(total, pCoreThreads), 12);
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

            return TruncateToUtf8Limit(sb.ToString(), 63);
        }

        /// <summary>
        /// Formats a horizontal progress bar.
        /// Example: "[█████-----] 50%"
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
                sb.Append('-');
            }
            sb.Append($"] {(int)Math.Round(percent)}%");
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
