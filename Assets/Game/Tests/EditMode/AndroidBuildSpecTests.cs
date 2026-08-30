using ImmortalLoot.Editor;
using NUnit.Framework;
using UnityEditor;

namespace ImmortalLoot.Tests
{
    public sealed class AndroidBuildSpecTests
    {
        [TestCase(AndroidBuildFlavor.DevelopmentApk, ".apk", true, false)]
        [TestCase(AndroidBuildFlavor.ReleaseCandidateApk, ".apk", false, false)]
        [TestCase(AndroidBuildFlavor.ReleaseCandidateAab, ".aab", false, true)]
        public void BuildSpec_UsesExpectedExtensionAndReleaseFlags(AndroidBuildFlavor flavor, string extension, bool development, bool appBundle)
        {
            var spec = AndroidBuildSpec.Create(flavor, "0.1.0");
            Assert.That(spec.OutputFileName, Does.EndWith(extension));
            Assert.That((spec.Options & BuildOptions.Development) != 0, Is.EqualTo(development));
            Assert.That(spec.BuildAppBundle, Is.EqualTo(appBundle));
            Assert.That(spec.OutputFileName, Does.Contain("0.1.0"));
        }
    }
}
