using System;
using System.IO;
using ImmortalLoot.Player;
using NUnit.Framework;

namespace ImmortalLoot.Tests
{
    public sealed class PlayerSaveAttemptTests
    {
        [Test]
        public void Execute_CatchesSaveFailureAndReportsTheOriginalException()
        {
            Exception observed = null;

            var succeeded = PlayerSaveAttempt.Execute(
                () => throw new IOException("simulated disk failure"),
                exception => observed = exception);

            Assert.That(succeeded, Is.False);
            Assert.That(observed, Is.TypeOf<IOException>());
        }

        [Test]
        public void Execute_ReturnsSuccessWithoutReportingFailure()
        {
            var writes = 0;
            var failureReports = 0;

            var succeeded = PlayerSaveAttempt.Execute(
                () => writes++,
                _ => failureReports++);

            Assert.That(succeeded, Is.True);
            Assert.That(writes, Is.EqualTo(1));
            Assert.That(failureReports, Is.Zero);
        }

        [Test]
        public void Execute_DoesNotHideUnexpectedProgrammingFailures()
        {
            var failureReports = 0;

            Assert.That(
                () => PlayerSaveAttempt.Execute(
                    () => throw new InvalidOperationException("unsupported save schema"),
                    _ => failureReports++),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(failureReports, Is.Zero);
        }

        [Test]
        public void Execute_SaveFailureStillReturnsFalseWhenBestEffortReporterFails()
        {
            Assert.That(
                PlayerSaveAttempt.Execute(
                    () => throw new IOException("simulated disk failure"),
                    _ => throw new InvalidOperationException("simulated UI reporting failure")),
                Is.False);
        }
    }
}
