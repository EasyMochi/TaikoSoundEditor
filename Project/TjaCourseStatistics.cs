using System;
using System.Collections.Generic;
using System.Linq;
using TaikoSoundEditor.Commons.IO;

namespace TaikoSoundEditor.Project
{
    internal sealed class TjaCourseStatistics
    {
        private const double ExpectedRendaHitsPerSecond = 16.92008d;

        public int NoteCount { get; private init; }
        public double RendaTimeSeconds { get; private init; }
        public int FuusenTotal { get; private init; }
        public int Shinuti { get; private init; }
        public int ShinutiScore { get; private init; }

        public static TjaCourseStatistics Calculate(TJA.Header songHeader, TJA.Course course)
        {
            if (course == null)
                return new TjaCourseStatistics();

            var bpm = songHeader?.Bpm > 0 ? songHeader.Bpm : 120d;
            var elapsed = 0d;
            var noteCount = 0;
            var rendaTime = 0d;
            var fuusenTotal = 0;
            var balloonIndex = 0;
            var rollStart = 0d;
            var activeRoll = RollKind.None;

            foreach (var measure in course.Measures ?? Enumerable.Empty<TJA.Measure>())
            {
                var data = measure?.MeasureData ?? string.Empty;
                var numerator = measure?.Length?.Length > 0 ? measure.Length[0] : 4;
                var denominator = measure?.Length?.Length > 1 && measure.Length[1] != 0 ? measure.Length[1] : 4;
                var measureBeats = (double)numerator / denominator * 4d;
                var slotCount = Math.Max(1, data.Length);
                var beatPerSlot = measureBeats / slotCount;

                var eventsByPosition = (measure?.Events ?? new List<TJA.MeasureEvent>())
                    .Select((value, index) => new { Value = value, Index = index })
                    .Where(item => item.Value != null)
                    .GroupBy(item => Math.Clamp(item.Value.Position, 0, data.Length))
                    .ToDictionary(group => group.Key, group => group.OrderBy(item => item.Index).Select(item => item.Value).ToList());

                if (data.Length == 0)
                {
                    ApplyEvents(eventsByPosition, 0, ref bpm, ref elapsed);
                    elapsed += BeatsToSeconds(measureBeats, bpm);
                    continue;
                }

                for (var position = 0; position <= data.Length; position++)
                {
                    ApplyEvents(eventsByPosition, position, ref bpm, ref elapsed);
                    if (position == data.Length) break;

                    var symbol = data[position];
                    if ("1234AB".Contains(symbol))
                    {
                        noteCount++;
                    }
                    else if (symbol == '5' || symbol == '6')
                    {
                        CloseRoll(elapsed, ref activeRoll, ref rollStart, ref rendaTime);
                        activeRoll = RollKind.Renda;
                        rollStart = elapsed;
                    }
                    else if (symbol == '7' || symbol == '9')
                    {
                        CloseRoll(elapsed, ref activeRoll, ref rollStart, ref rendaTime);
                        activeRoll = RollKind.Balloon;
                        rollStart = elapsed;

                        var balloons = course.Headers?.Balloon ?? Array.Empty<int>();
                        if (balloonIndex < balloons.Length)
                            fuusenTotal += Math.Max(0, balloons[balloonIndex]);
                        balloonIndex++;
                    }
                    else if (symbol == '8')
                    {
                        CloseRoll(elapsed, ref activeRoll, ref rollStart, ref rendaTime);
                    }

                    elapsed += BeatsToSeconds(beatPerSlot, bpm);
                }
            }

            CloseRoll(elapsed, ref activeRoll, ref rollStart, ref rendaTime);
            rendaTime = Math.Max(0d, rendaTime);

            var expectedRendaHits = Math.Max(0,
                (int)Math.Round(rendaTime * ExpectedRendaHitsPerSecond, MidpointRounding.AwayFromZero));
            var bonusScoreLong = (long)fuusenTotal * 100L + (long)expectedRendaHits * 100L;
            var bonusScore = (int)Math.Min(int.MaxValue, Math.Max(0L, bonusScoreLong));
            var shinuti = CalculateShinuti(noteCount, bonusScore);
            var shinutiScoreLong = (long)shinuti * noteCount + bonusScore;
            var shinutiScore = noteCount <= 0 ? 0 : (int)Math.Min(int.MaxValue, shinutiScoreLong);

            return new TjaCourseStatistics
            {
                NoteCount = noteCount,
                RendaTimeSeconds = Math.Round(rendaTime, 6, MidpointRounding.AwayFromZero),
                FuusenTotal = fuusenTotal,
                Shinuti = shinuti,
                ShinutiScore = shinutiScore
            };
        }

        private static int CalculateShinuti(int noteCount, int bonusScore)
        {
            if (noteCount <= 0) return 0;
            var remaining = Math.Max(0d, 1_000_000d - bonusScore);
            var perNote = remaining / noteCount;
            var rounded = Math.Ceiling(perNote / 10d) * 10d;
            return (int)Math.Min(int.MaxValue, Math.Max(10d, rounded));
        }

        private static void ApplyEvents(Dictionary<int, List<TJA.MeasureEvent>> eventsByPosition, int position,
            ref double bpm, ref double elapsed)
        {
            if (!eventsByPosition.TryGetValue(position, out var events)) return;

            foreach (var measureEvent in events)
            {
                if ((string.Equals(measureEvent.Name, "bpm", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(measureEvent.Name, "bmp", StringComparison.OrdinalIgnoreCase)) &&
                    measureEvent.Value > 0)
                {
                    bpm = measureEvent.Value;
                }
                else if (string.Equals(measureEvent.Name, "delay", StringComparison.OrdinalIgnoreCase))
                {
                    elapsed += measureEvent.Value;
                }
            }
        }

        private static double BeatsToSeconds(double beats, double bpm) =>
            bpm <= 0 ? 0d : beats * 60d / bpm;

        private static void CloseRoll(double elapsed, ref RollKind activeRoll, ref double rollStart,
            ref double rendaTime)
        {
            if (activeRoll == RollKind.Renda)
                rendaTime += Math.Max(0d, elapsed - rollStart);
            activeRoll = RollKind.None;
            rollStart = 0d;
        }

        private enum RollKind
        {
            None,
            Renda,
            Balloon
        }
    }
}
