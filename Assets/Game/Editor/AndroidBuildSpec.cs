using System;
using UnityEditor;

namespace ImmortalLoot.Editor
{
    public enum AndroidBuildFlavor { DevelopmentApk, ReleaseCandidateApk, ReleaseCandidateAab }

    public readonly struct AndroidBuildSpec
    {
        public string OutputFileName { get; }
        public BuildOptions Options { get; }
        public bool BuildAppBundle { get; }

        private AndroidBuildSpec(string outputFileName, BuildOptions options, bool buildAppBundle)
        {
            OutputFileName = outputFileName;
            Options = options;
            BuildAppBundle = buildAppBundle;
        }

        public static AndroidBuildSpec Create(AndroidBuildFlavor flavor, string version)
        {
            if (string.IsNullOrWhiteSpace(version)) throw new ArgumentException("A bundle version is required.", nameof(version));
            const string product = "Taichu-Endless-Reincarnation";
            switch (flavor)
            {
                case AndroidBuildFlavor.DevelopmentApk:
                    return new AndroidBuildSpec($"{product}-{version}-dev.apk", BuildOptions.Development, false);
                case AndroidBuildFlavor.ReleaseCandidateApk:
                    return new AndroidBuildSpec($"{product}-{version}-rc.apk", BuildOptions.None, false);
                case AndroidBuildFlavor.ReleaseCandidateAab:
                    return new AndroidBuildSpec($"{product}-{version}-rc.aab", BuildOptions.None, true);
                default: throw new ArgumentOutOfRangeException(nameof(flavor));
            }
        }
    }
}
