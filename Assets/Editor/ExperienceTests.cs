using NUnit.Framework;
using SleepHealing.Core;
using SleepHealing.EEG;

namespace SleepHealing.Editor.Tests
{
    public sealed class ExperienceTests
    {
        [Test]
        public void TimelineFollowsFiveStageOrder()
        {
            var timeline = new ExperienceTimeline(ExperienceMode.Demo);
            timeline.Start();
            Assert.AreEqual(ExperienceStage.Preparation, timeline.Stage);
            timeline.Tick(10f);
            Assert.AreEqual(ExperienceStage.Baseline, timeline.Stage);
            timeline.Tick(15f);
            Assert.AreEqual(ExperienceStage.Breathing, timeline.Stage);
            timeline.Tick(20f);
            Assert.AreEqual(ExperienceStage.DeepHealing, timeline.Stage);
            timeline.Tick(35f);
            Assert.AreEqual(ExperienceStage.Awakening, timeline.Stage);
            timeline.Tick(10f);
            Assert.IsTrue(timeline.IsCompleted);
        }

        [Test]
        public void PauseAndResumeDoNotConsumeTimeWhilePaused()
        {
            var timeline = new ExperienceTimeline(ExperienceMode.Demo);
            timeline.Start();
            timeline.Tick(3f);
            timeline.Pause();
            timeline.Tick(20f);
            Assert.AreEqual(3f, timeline.TotalElapsed, 0.001f);
            timeline.Resume();
            timeline.Tick(2f);
            Assert.AreEqual(5f, timeline.TotalElapsed, 0.001f);
        }

        [Test]
        public void ExitAlwaysReturnsToSafePreparationState()
        {
            var timeline = new ExperienceTimeline(ExperienceMode.Demo);
            timeline.Start();
            timeline.Tick(52f);
            timeline.Exit();
            var elapsedAtExit = timeline.TotalElapsed;
            Assert.IsFalse(timeline.IsRunning);
            Assert.IsTrue(timeline.WasExited);
            Assert.AreEqual(ExperienceStage.Preparation, timeline.Stage);
            timeline.Tick(200f);
            Assert.AreEqual(elapsedAtExit, timeline.TotalElapsed, 0.001f);
            Assert.AreEqual(ExperienceStage.Preparation, timeline.Stage);
        }

        [Test]
        public void InvalidSignalFreezesAdaptiveValues()
        {
            var processor = new EEGSignalProcessor(requiredStableSeconds: 0f);
            var stable = State(1f, 0.4f, 0.5f, 0.3f);
            processor.Process(stable, 1f);
            var frozen = processor.Process(State(0f, 1f, 0f, 1f), 1f);
            Assert.AreEqual(0.4f, frozen.relaxation, 0.001f);
            Assert.AreEqual(0.5f, frozen.attention, 0.001f);
            Assert.IsFalse(processor.AdaptationEnabled);
        }

        [Test]
        public void SingleSpikeIsRateLimited()
        {
            var processor = new EEGSignalProcessor(requiredStableSeconds: 0f, maximumChangePerSecond: 0.1f);
            processor.Process(State(1f, 0.4f, 0.5f, 0.3f), 1f);
            var filtered = processor.Process(State(1f, 1f, 0f, 1f), 0.1f);
            Assert.LessOrEqual(filtered.relaxation, 0.411f);
            Assert.GreaterOrEqual(filtered.attention, 0.489f);
        }

        private static EEGState State(float quality, float relaxation, float attention, float fatigue)
        {
            return new EEGState
            {
                signalQuality = quality,
                relaxation = relaxation,
                attention = attention,
                fatigue = fatigue,
                timestamp = 1d
            };
        }
    }
}
