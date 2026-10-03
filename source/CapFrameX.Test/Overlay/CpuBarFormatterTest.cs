using System.Collections.Generic;
using System.Text;
using CapFrameX.Overlay;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Overlay
{
    [TestClass]
    public class CpuBarFormatterTest
    {
        [TestMethod]
        public void GetBarGlyph_BoundsAndLevels()
        {
            Assert.AreEqual(' ', CpuBarFormatter.GetBarGlyph(0.0));
            Assert.AreEqual(' ', CpuBarFormatter.GetBarGlyph(-5.0));
            Assert.AreEqual(' ', CpuBarFormatter.GetBarGlyph(double.NaN));
            Assert.AreEqual(' ', CpuBarFormatter.GetBarGlyph(5.0));
            Assert.AreEqual('▂', CpuBarFormatter.GetBarGlyph(25.0));
            Assert.AreEqual('▄', CpuBarFormatter.GetBarGlyph(50.0));
            Assert.AreEqual('▆', CpuBarFormatter.GetBarGlyph(75.0));
            Assert.AreEqual('█', CpuBarFormatter.GetBarGlyph(95.0));
            Assert.AreEqual('█', CpuBarFormatter.GetBarGlyph(100.0));
            Assert.AreEqual('█', CpuBarFormatter.GetBarGlyph(120.0));
        }

        [TestMethod]
        public void FormatThreadBars_EmptyOrNull_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, CpuBarFormatter.FormatThreadBars(null));
            Assert.AreEqual(string.Empty, CpuBarFormatter.FormatThreadBars(new List<double>()));
        }

        [TestMethod]
        public void FormatThreadBars_8Threads_PairsWithSpaces()
        {
            var loads = new[] { 10.0, 95.0, 50.0, 0.0, 75.0, 25.0, 100.0, 100.0 };
            string result = CpuBarFormatter.FormatThreadBars(loads);

            // 8 threads = 4 pairs: " █ ▄  ▆▂ ██"
            Assert.AreEqual(" █ ▄  ▆▂ ██", result);

            int byteCount = Encoding.UTF8.GetByteCount(result);
            Assert.IsTrue(byteCount < 63, $"Byte count {byteCount} must fit in 63 bytes");
        }

        [TestMethod]
        public void FormatThreadBars_16Threads_FitsUnder63Bytes()
        {
            var loads = new double[16];
            for (int i = 0; i < 16; i++) loads[i] = i * 6.5;

            string result = CpuBarFormatter.FormatThreadBars(loads);
            int byteCount = Encoding.UTF8.GetByteCount(result);

            // 16 glyphs (16*3 = 48 bytes) + 7 spaces = 55 bytes
            Assert.AreEqual(55, byteCount);
            Assert.IsTrue(byteCount < 63, $"Byte count {byteCount} must fit in 63 bytes");
        }

        [TestMethod]
        public void FormatThreadBars_32Threads_AggregatesTo16Cores_FitsUnder63Bytes()
        {
            var loads = new double[32];
            for (int i = 0; i < 32; i++) loads[i] = 50.0;

            string result = CpuBarFormatter.FormatThreadBars(loads);
            int byteCount = Encoding.UTF8.GetByteCount(result);

            // 16 cores (16*3 = 48 bytes) + 3 spaces = 51 bytes
            Assert.AreEqual(51, byteCount);
            Assert.IsTrue(byteCount < 63, $"Byte count {byteCount} must fit in 63 bytes");
        }

        [TestMethod]
        public void FormatCoreBars_8Cores_SeparatorsEvery4()
        {
            var loads = new[] { 10.0, 25.0, 50.0, 75.0, 90.0, 95.0, 0.0, 50.0 };
            string result = CpuBarFormatter.FormatCoreBars(loads);

            // " ▂▄▆ ▇█ ▄"
            Assert.AreEqual(" ▂▄▆ ▇█ ▄", result);

            int byteCount = Encoding.UTF8.GetByteCount(result);
            Assert.IsTrue(byteCount < 63, $"Byte count {byteCount} must fit in 63 bytes");
        }
    }
}
