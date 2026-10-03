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
        public void GetTrackBarGlyph_BoundsAndLevels()
        {
            Assert.AreEqual(' ', CpuBarFormatter.GetTrackBarGlyph(0.0));
            Assert.AreEqual(' ', CpuBarFormatter.GetTrackBarGlyph(-5.0));
            Assert.AreEqual(' ', CpuBarFormatter.GetTrackBarGlyph(double.NaN));
            Assert.AreEqual(' ', CpuBarFormatter.GetTrackBarGlyph(5.0));
            Assert.AreEqual('▂', CpuBarFormatter.GetTrackBarGlyph(15.0));
            Assert.AreEqual('▃', CpuBarFormatter.GetTrackBarGlyph(25.0));
            Assert.AreEqual('▄', CpuBarFormatter.GetTrackBarGlyph(40.0));
            Assert.AreEqual('▅', CpuBarFormatter.GetTrackBarGlyph(55.0));
            Assert.AreEqual('▆', CpuBarFormatter.GetTrackBarGlyph(70.0));
            Assert.AreEqual('▇', CpuBarFormatter.GetTrackBarGlyph(85.0));
            Assert.AreEqual('█', CpuBarFormatter.GetTrackBarGlyph(95.0));
            Assert.AreEqual('█', CpuBarFormatter.GetTrackBarGlyph(100.0));
        }

        [TestMethod]
        public void FormatTrackBars_8Threads_DistinctSpaces_FitsUnder63Bytes()
        {
            var loads = new[] { 10.0, 95.0, 50.0, 0.0, 75.0, 25.0, 100.0, 100.0 };
            string result = CpuBarFormatter.FormatTrackBars(loads);

            // "▂ █ ▅   ▇ ▃ █ █"
            Assert.AreEqual("▂ █ ▅   ▇ ▃ █ █", result);

            int byteCount = Encoding.UTF8.GetByteCount(result);
            Assert.IsTrue(byteCount < 63, $"Byte count {byteCount} must fit in 63 bytes");
        }

        [TestMethod]
        public void FormatTrackBars_16Threads_FitsUnder63Bytes()
        {
            var loads = new double[16];
            for (int i = 0; i < 16; i++) loads[i] = i * 6.5;

            string result = CpuBarFormatter.FormatTrackBars(loads);
            int byteCount = Encoding.UTF8.GetByteCount(result);

            // 16 glyphs (16*3 = 48 bytes) + 15 spaces = 63 bytes
            Assert.AreEqual(63, byteCount);
            Assert.IsTrue(byteCount <= 63, $"Byte count {byteCount} must fit in 63 bytes");
        }

        [TestMethod]
        public void FormatHybridBars_PAndECores_PartitionedWithDivider()
        {
            // 8 P-threads + 4 E-threads
            var loads = new[] { 100.0, 10.0, 50.0, 10.0, 100.0, 10.0, 50.0, 10.0, 30.0, 40.0, 50.0, 60.0 };
            string result = CpuBarFormatter.FormatHybridBars(loads, pCoreThreads: 8, eCoreThreads: 4);

            StringAssert.Contains(result, "│");
            int byteCount = Encoding.UTF8.GetByteCount(result);
            Assert.IsTrue(byteCount <= 63, $"Byte count {byteCount} must fit in 63 bytes");
        }

        [TestMethod]
        public void FormatHorizontalBar_RendersCorrectly()
        {
            string bar0 = CpuBarFormatter.FormatHorizontalBar(0.0, 10);
            Assert.AreEqual("[----------] 0%", bar0);

            string bar50 = CpuBarFormatter.FormatHorizontalBar(50.0, 10);
            Assert.AreEqual("[█████-----] 50%", bar50);

            string bar100 = CpuBarFormatter.FormatHorizontalBar(100.0, 10);
            Assert.AreEqual("[██████████] 100%", bar100);
        }

        [TestMethod]
        public void FormatCoreBars_8Cores_SeparatorsEvery4()
        {
            var loads = new[] { 10.0, 25.0, 50.0, 75.0, 90.0, 95.0, 0.0, 50.0 };
            string result = CpuBarFormatter.FormatCoreBars(loads);

            // "▂ ▃ ▅ ▇ █ █   ▅"
            Assert.AreEqual("▂ ▃ ▅ ▇ █ █   ▅", result);

            int byteCount = Encoding.UTF8.GetByteCount(result);
            Assert.IsTrue(byteCount < 63, $"Byte count {byteCount} must fit in 63 bytes");
        }
    }
}
