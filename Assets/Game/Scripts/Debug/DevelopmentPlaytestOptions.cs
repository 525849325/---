using System;
using UnityEngine;

namespace ImmortalLoot.Debugging
{
    public static class DevelopmentPlaytestOptions
    {
        public static float Speed { get; }
        public static bool AutoQuit { get; }

        static DevelopmentPlaytestOptions()
        {
            Speed = 1f;
            if (!Debug.isDebugBuild) return;
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (argument.StartsWith("-playtestSpeed=", StringComparison.OrdinalIgnoreCase) &&
                    float.TryParse(argument.Substring("-playtestSpeed=".Length), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var speed)) Speed = Mathf.Clamp(speed, 1f, 240f);
                else if (string.Equals(argument, "-playtestAutoQuit", StringComparison.OrdinalIgnoreCase)) AutoQuit = true;
            }
        }
    }
}
