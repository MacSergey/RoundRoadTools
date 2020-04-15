using AdvancedRoadTools.Tools;
using ColossalFramework.Math;
using ColossalFramework.PlatformServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            TestMethod(11.905f, -386.700f, 0.863f, 0.505f, 0, -52.655f, -327.492f, 0.407f, 0.913f, 0, 25, false, 324.858f, 28.155f);
        }
        static void TestMethod(float sx, float sz, float sdx, float sdz, int sm, float ex, float ez, float edx, float edz, int em, int r, bool cw, float? rx = null, float? rz = null)
        {
            //Utility.CalculateShiftsIndex(1, 2, 1, 2);

            var startNode = new NodePoint(sx, sz, sdx, sdz, (NodeDir)sm);
            var endNode = new NodePoint(ex, ez, edx, edz, (NodeDir)em);
            var foundRoundResult = Utility.FoundRound(startNode, endNode, r, cw, (s) => Console.WriteLine(s));
            var calcRoadResult = Utility.CalculateRoad(startNode, endNode, r, 5 * Utility.U, new float[] { 3,6,9}, foundRoundResult);
        }
    }
}
