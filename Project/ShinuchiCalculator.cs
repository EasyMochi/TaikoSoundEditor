using System;

namespace TaikoSoundEditor.Project
{
    internal readonly struct GeneratedShinuchiResult
    {
        public GeneratedShinuchiResult(int initial, int score)
        {
            Initial = initial;
            Score = score;
        }

        public int Initial { get; }
        public int Score { get; }
    }

    internal static class ShinuchiCalculator
    {
        private const double ExpectedRendaHitsPerSecond = 16.92008d;

        public static GeneratedShinuchiResult CalculateGenerated(
            int noteCount, double rendaTimeSeconds, int fuusenTotal)
        {
            if (noteCount <= 0)
                return new GeneratedShinuchiResult(0, 0);

            var expectedRendaHits = Math.Max(0,
                (int)Math.Round(Math.Max(0d, rendaTimeSeconds) * ExpectedRendaHitsPerSecond,
                    MidpointRounding.AwayFromZero));
            var bonusScoreLong = (long)Math.Max(0, fuusenTotal) * 100L +
                                 (long)expectedRendaHits * 100L;
            var bonusScore = (int)Math.Min(int.MaxValue, bonusScoreLong);
            var remaining = Math.Max(0d, 1_000_000d - bonusScore);
            var perNote = remaining / noteCount;
            var initial = (int)Math.Min(int.MaxValue,
                Math.Max(10d, Math.Ceiling(perNote / 10d) * 10d));
            var scoreLong = (long)initial * noteCount + bonusScore;
            var score = (int)Math.Min(int.MaxValue, Math.Max(0L, scoreLong));
            return new GeneratedShinuchiResult(initial, score);
        }

        public static int CalculateScoreFromInitial(
            int noteCount, double rendaTimeSeconds, int fuusenTotal, int initial)
        {
            if (noteCount <= 0 || initial <= 0) return 0;

            var expectedRendaHits = Math.Max(0,
                (int)Math.Round(Math.Max(0d, rendaTimeSeconds) * ExpectedRendaHitsPerSecond,
                    MidpointRounding.AwayFromZero));
            var bonusScore = (long)Math.Max(0, fuusenTotal) * 100L +
                             (long)expectedRendaHits * 100L;
            var score = (long)initial * noteCount + bonusScore;
            return (int)Math.Min(int.MaxValue, Math.Max(0L, score));
        }
    }
}
