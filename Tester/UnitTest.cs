using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mod;
using System;
using UnityEngine;

namespace Tester
{
    [TestClass]
    public class UnitTest
    {
        [TestMethod]

        [DataRow(11.905f, -386.700f, 0.863f, 0.505f, 1, -52.655f, -327.492f, 0.407f, 0.913f, 1, 4, false, -41.387f, -380.809f)]
        [DataRow(11.905f, -386.700f, 0.863f, 0.505f, 1, -52.655f, -327.492f, 0.407f, 0.913f, 1, 4, true, -180.910f, -536.606f)]

        [DataRow(11.905f, -386.700f, 0.863f, 0.505f, 0, -52.655f, -327.492f, 0.407f, 0.913f, 0, 25, false, 324.858f, 28.155f)]
        [DataRow(11.905f, -386.700f, 0.863f, 0.505f, 0, -52.655f, -327.492f, 0.407f, 0.913f, 0, 25, true, 324.858f, 28.155f)]

        [DataRow(11.905f, -386.700f, 0.863f, 0.505f, 1, -52.655f, -327.492f, 0.407f, 0.913f, 0, 60, false, -486.739f, -122.349f)]
        [DataRow(11.905f, -386.700f, 0.863f, 0.505f, 1, -52.655f, -327.492f, 0.407f, 0.913f, 0, 60, true, -486.739f, -122.349f)]

        [DataRow(11.905f, -386.700f, 0.863f, 0.505f, 0, -52.655f, -327.492f, 0.407f, 0.913f, 1, 60, false, 264.441f, -795.066f)]
        [DataRow(11.905f, -386.700f, 0.863f, 0.505f, 0, -52.655f, -327.492f, 0.407f, 0.913f, 1, 60, true, 264.441f, -795.066f)]

        [DataRow(11.905f, -386.700f, 0.863f, 0.505f, 1, -52.655f, -327.492f, 0.407f, 0.913f, 1, 25, false, -547.157f, -945.571f)]
        [DataRow(11.905f, -386.700f, 0.863f, 0.505f, 1, -52.655f, -327.492f, 0.407f, 0.913f, 1, 25, true, -547.157f, -945.571f)]
        public void TestMethod(float sx, float sz, float sdx, float sdz, int sm, float ex, float ez, float edx, float edz, int em, int r, bool cw, float rx, float rz)
        {
            var mod = new RoundRoadTools();
            var result = mod.FoundRound(new NodePoint(sx, sz, sdx, sdz, (NodeDir)sm), new NodePoint(ex, ez, edx, edz, (NodeDir)em), r, cw);

            Assert.AreEqual(rx, result.RoundCenterPos.x, 0.001);
            Assert.AreEqual(rz, result.RoundCenterPos.y, 0.001);
        }

        [TestMethod]

        [DataRow(11.905f, -386.700f, 0.863f, 0.505f, 0, -52.655f, -327.492f, 0.407f, 0.913f, 0, 4, false)]
        [DataRow(11.905f, -386.700f, 0.863f, 0.505f, 0, -52.655f, -327.492f, 0.407f, 0.913f, 0, 4, true)]

        [DataRow(11.905f, -386.700f, 0.863f, 0.505f, 1, -52.655f, -327.492f, 0.407f, 0.913f, 0, 4, false)]
        [DataRow(11.905f, -386.700f, 0.863f, 0.505f, 1, -52.655f, -327.492f, 0.407f, 0.913f, 0, 4, true)]

        [DataRow(11.905f, -386.700f, 0.863f, 0.505f, 0, -52.655f, -327.492f, 0.407f, 0.913f, 1, 4, false)]
        [DataRow(11.905f, -386.700f, 0.863f, 0.505f, 0, -52.655f, -327.492f, 0.407f, 0.913f, 1, 4, true)]
        public void SmallRadiusTestMethod(float sx, float sz, float sdx, float sdz, int sm, float ex, float ez, float edx, float edz, int em, int r, bool cw)
        {
            var mod = new RoundRoadTools();
            Assert.ThrowsException<RoadSmallRadiusException>(() => mod.FoundRound(new NodePoint(sx, sz, sdx, sdz, (NodeDir)sm), new NodePoint(ex, ez, edx, edz, (NodeDir)em), r, cw));
        }

        [TestMethod]

        [DataRow(11.905f, -386.700f, 0.863f, 0.505f, 0, -52.655f, -327.492f, 0.863f, 0.505f, 0, 4, false)]
        [DataRow(11.905f, -386.700f, 0.863f, 0.505f, 0, -52.655f, -327.492f, 0.863f, 0.505f, 1, 4, false)]
        [DataRow(11.905f, -386.700f, 0.863f, 0.505f, 1, -52.655f, -327.492f, 0.863f, 0.505f, 0, 4, false)]
        [DataRow(11.905f, -386.700f, 0.863f, 0.505f, 1, -52.655f, -327.492f, 0.863f, 0.505f, 1, 4, false)]

        [DataRow(11.905f, -386.700f, 0.863f, 0.505f, 0, 11.905f, -386.700f, 0.863f, 0.505f, 0, 4, false)]
        [DataRow(11.905f, -386.700f, 0.863f, 0.505f, 0, 11.905f, -386.700f, 0.863f, 0.505f, 1, 4, false)]
        [DataRow(11.905f, -386.700f, 0.863f, 0.505f, 1, 11.905f, -386.700f, 0.863f, 0.505f, 0, 4, false)]
        [DataRow(11.905f, -386.700f, 0.863f, 0.505f, 1, 11.905f, -386.700f, 0.863f, 0.505f, 1, 4, false)]

        public void ParallelLinesTestMethod(float sx, float sz, float sdx, float sdz, int sm, float ex, float ez, float edx, float edz, int em, int r, bool cw)
        {
            var mod = new RoundRoadTools();
            Assert.ThrowsException<RoadParallelLinesException>(() => mod.FoundRound(new NodePoint(sx, sz, sdx, sdz, (NodeDir)sm), new NodePoint(ex, ez, edx, edz, (NodeDir)em), r, cw));
        }

        [TestMethod]

        //[DataRow(0, 0, 0, 0, new int[0])]
        [DataRow(1, 1, 1, 1, new int[] { -1, 0, 0 })]
        [DataRow(1, 1, 2, 1, new int[] { -1, 0, 0, 0 })]
        [DataRow(1, 1, 3, 1, new int[] { -1, 0, 0, 0, 0 })]

        [DataRow(1, 2, 1, 2, new int[] { -1, -1, 0, 0, 0 })]
        [DataRow(1, 2, 2, 2, new int[] { -1, -1, 0, 0, 0, 0 })]
        [DataRow(1, 2, 3, 2, new int[] { -1, -1, 0, 0, 0, 0, 0 })]

        [DataRow(2, 1, 2, 1, new int[] { -1, 0, 1, 1 })]
        [DataRow(2, 1, 3, 1, new int[] { -1, 0, 1, 1, 1 })]

        [DataRow(2, 1, 1, 1, new int[] { 0, 1, 1 })]
        [DataRow(2, 2, 1, 1, new int[] { -1, 0, 1, 1 })]
        [DataRow(3, 2, 2, 1, new int[] { -1, 0, 1, 2, 2})]
        [DataRow(5, 3, 3, 1, new int[] { -1, 0, 1, 2, 3, 4, 4 })]

        [DataRow(7, 3, 3, 1, new int[] { 0, 1, 2, 3, 4, 5, 6 })]
        [DataRow(8, 3, 3, 2, new int[] { 0, 1, 2, 3, 4, 5, 6, 7 })]
        [DataRow(7, 3, 3, 2, new int[] { 0, 1, 2, 3, 4, 5, 6, 6 })]

        public void CalculateShiftsIndexTestMethod(int shifts, int starts, int rounds, int ends, int[] expected)
        {
            var mod = new RoundRoadTools();
            var result = mod.CalculateShiftsIndex(shifts, starts, rounds, ends);
            Assert.AreEqual(expected.Length, result.Length, "Различная длина");
            for (int i = 0; i < expected.Length; i += 1)
                Assert.AreEqual(expected[i], result[i], $"Элемент {i}");
        }
    }
}
