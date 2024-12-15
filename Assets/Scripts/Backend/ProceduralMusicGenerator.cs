using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using Melanchall.DryWetMidi.Standards;
using Chord = Melanchall.DryWetMidi.MusicTheory.Chord;
using Note = Melanchall.DryWetMidi.MusicTheory.Note;

namespace Backend {
public static class ProceduralMusicGenerator {
    public class GeneratorOptions {
        public int Seed = 0;
        public PatternBuilder StarterMelody = null;
        public Scale Scale = null;
    }

    public static Stream GenerateMusicStream(GeneratorOptions options) {
        var ms = new MemoryStream();
        GenerateMusicMidiFile(options).Write(ms);
        ms.Position = 0;
        return ms;
    }

    public static MidiFile GenerateMusicMidiFile(GeneratorOptions options) {
        var patterns = GenerateMusic(options);
        var trackChunks = patterns.Select(track =>
            track.Builder.Build().ToTrackChunk(TempoMap.Default, track.Channel));
        return new MidiFile(trackChunks);
    }

    private static List<(PatternBuilder Builder, FourBitNumber Channel)> GenerateMusic(GeneratorOptions options) {
        // invariants for the whole composition
        var random = new Random(options.Seed);
        var scale = options.Scale ?? new Scale(
            GetRandomElement(AllScales.Where(scale => scale.Count() > 6), random),
            (NoteName)GetRandomElement(Enumerable.Range(0, Octave.OctaveSize), random));
        var chordLength = MusicalTimeSpan.Whole;
        var melodyNoteLength = MusicalTimeSpan.Eighth;
        var percussionNoteLength = MusicalTimeSpan.Eighth;

        // parts in the sheet music, corresponding to different roles and instrument groups
        // with a separate pattern builder and later track for each
        var chordPart = new PatternBuilder().ProgramChange(GeneralMidiProgram.ElectricBass1)
            .SetNoteLength(chordLength).SetStep(chordLength);
        var melodyPart = (options.StarterMelody ?? new PatternBuilder()).ProgramChange(GeneralMidiProgram.SopranoSax)
            .SetNoteLength(melodyNoteLength).SetStep(melodyNoteLength);
        var percussionPart = new PatternBuilder()
            .SetNoteLength(percussionNoteLength).SetStep(percussionNoteLength);

        // manage whether a starter melody was specified
        var starterMelodyTrack = options.StarterMelody?.Build().ToTrackChunk(TempoMap.Default);
        var starterMelodyLength = starterMelodyTrack?
            .GetDuration<MusicalTimeSpan>(TempoMap.Default) ?? new MusicalTimeSpan();

        var chordProgression = GenerateChords(scale, random, 32);

        // variables that track the current measure in the loop
        // time 0 
        var currentStartTime = new MusicalTimeSpan();
        // the starter melody or empty
        var currentMelody = starterMelodyTrack?.GetNotes().Select(note => note.GetMusicTheoryNote()).ToList()
                            ?? new List<Note>();
        Dictionary<Note, List<int>> currentPercussionPattern = null;

        for (var i = 0; i < chordProgression.Count; i++) {
            // arrange chords
            var chord = chordProgression[i];
            chordPart.MoveToTime(currentStartTime);
            chord.ResolveNotes(Octave.Get(Octave.Middle.Number - 2)).ToList().ForEach(note => {
                chordPart.Note(note, chordLength);
                chordPart.StepBack(chordLength);
            });

            // arrange melody
            // don't generate melody if we still have notes from the starter
            if (currentStartTime >= starterMelodyLength) {
                GenerateMelody(melodyPart, currentStartTime,
                    chordLength, melodyNoteLength, scale, chord,
                    currentMelody, random);
            }

            // arrange percussion
            if (i % 4 == 0)
                currentPercussionPattern = GeneratePercussionPatterns(percussionNoteLength, chordLength, random);
            foreach (var instrument in currentPercussionPattern!) {
                instrument.Value.ForEach(time => percussionPart
                    .MoveToTime(currentStartTime + percussionNoteLength * time)
                    .Note(instrument.Key, percussionNoteLength));
            }

            currentStartTime += chordLength;
        }

        return new() {
            (melodyPart, new FourBitNumber(0)),
            (chordPart, new FourBitNumber(1)),
            (percussionPart, GeneralMidi.PercussionChannel)
        };
    }

    private static List<Chord> GenerateChords(Scale scale, Random random, int count) {
        const int rootNote = 1;
        const int secondNote = 3;
        const int thirdNote = 5;
        var currentChord = new Chord(
            scale.GetDegree((ScaleDegree)(rootNote - 1)),
            scale.GetDegree((ScaleDegree)(secondNote - 1)),
            scale.GetDegree((ScaleDegree)(thirdNote - 1)));
        var chords = new List<Chord> { currentChord };
        foreach (var _ in Enumerable.Range(2, count - 1)) {
            var commonNote = GetRandomElement(currentChord.NotesNames, random);
            var concreteNote = Note.Get(commonNote, Octave.Middle.Number);
            // a 1, 3, 5 chord
            var nextRootNote = scale.GetNextNote(concreteNote);
            var nextSecondaryNote = scale.GetAscendingNotes(nextRootNote).Skip(secondNote - 1).First();
            var nextThirdNote = scale.GetAscendingNotes(nextRootNote).Skip(thirdNote - 1).First();

            var newChordNotes = new[] { nextRootNote, nextSecondaryNote, nextThirdNote };
            var newChord = new Chord(newChordNotes.Select(n => n.NoteName).ToArray());
            newChord = GetRandomElement(newChord.GetInversions(), random);
            chords.Add(newChord);
            currentChord = newChord;
        }

        return chords;
    }

    // writes the melody to the builder, and changes previousMelody to the current one
    private static void GenerateMelody(PatternBuilder melodyPart, MusicalTimeSpan currentStartTime,
        MusicalTimeSpan chordLength, MusicalTimeSpan melodyNoteLength, Scale scale, Chord chord,
        List<Note> previousMelody, Random random) {
        var maxMelodyNoteCount = (int)chordLength.Divide(melodyNoteLength);
        var currentMelodyNoteCount = random.Next(maxMelodyNoteCount / 2 - 1, maxMelodyNoteCount);
        var currentMelody = GenerateMelodyNotes(scale, chord, previousMelody, currentMelodyNoteCount, random);
        currentMelody.AddRange(Enumerable.Range(0, maxMelodyNoteCount - currentMelodyNoteCount)
            .Select(_ => (Note)null));
        Shuffle(currentMelody, random);
        previousMelody.Clear();
        previousMelody.AddRange(currentMelody);

        melodyPart.MoveToTime(currentStartTime);
        for (var index = 0; index < currentMelody.Count - 1; index++) {
            var note = currentMelody[index];
            var hasNextNote = currentMelody[index + 1] is not null;
            if (hasNextNote) {
                if (note is null)
                    melodyPart.StepForward(melodyNoteLength);
                else
                    melodyPart.Note(note, melodyNoteLength);
            }
            else if (note is not null) {
                melodyPart.Note(note, melodyNoteLength * 2);
                index++;
            }
            else
                melodyPart.StepForward(melodyNoteLength);
        }

        if (currentMelody.Last() is not null) {
            melodyPart.Note(currentMelody.Last(), melodyNoteLength);
        }
    }

    private static List<Note> GenerateMelodyNotes(Scale scale, Chord chord, List<Note> previousMelody,
        int noteCount, Random random) {
        var melodyOctaveNumber = Octave.Middle.Number;
        var previousNote = previousMelody?.LastOrDefault(note => note is not null);
        var noteFromChord = Note.Get(GetRandomElement(chord.NotesNames, random),
            previousNote?.Octave ?? melodyOctaveNumber);
        var firstNote = previousNote ?? noteFromChord;

        var melodyDirectionUp = random.Next(2) == 1;
        if (firstNote.CompareTo(Note.Get(NoteName.F, melodyOctaveNumber + 1)) > 0) melodyDirectionUp = false;
        if (firstNote.CompareTo(Note.Get(NoteName.C, melodyOctaveNumber)) < 0) melodyDirectionUp = true;

        var melody = new List<Note> { firstNote, noteFromChord };
        melody.AddRange((melodyDirectionUp
                ? scale.GetAscendingNotes(firstNote)
                : scale.GetDescendingNotes(firstNote))
            .Skip(1).Take(noteCount - 2));
        Shuffle(melody, random);
        return melody;
    }

    private static Dictionary<Note, List<int>> GeneratePercussionPatterns(MusicalTimeSpan percussionNoteLength,
        MusicalTimeSpan chordLength, Random random) {
        var bassNote = Note.Get(GeneralMidiPercussion.BassDrum1.AsSevenBitNumber());
        var snareNote = Note.Get(GeneralMidiPercussion.AcousticSnare.AsSevenBitNumber());
        var hiHatNote = Note.Get(GeneralMidiPercussion.ClosedHiHat.AsSevenBitNumber());

        var maxPercussionNoteCount = (int)chordLength.Divide(percussionNoteLength);
        var bassAndSnareTimings = Enumerable.Range(0, maxPercussionNoteCount).ToList();
        var hiHatTimings = Enumerable.Range(0, maxPercussionNoteCount).ToList();
        Shuffle(bassAndSnareTimings, random);
        Shuffle(hiHatTimings, random);
        var bassCount = random.Next(1, maxPercussionNoteCount / 2);
        var snareCount = random.Next(1, maxPercussionNoteCount / 2);
        var hiHatCount = random.Next(maxPercussionNoteCount / 2, maxPercussionNoteCount);

        return new Dictionary<Note, List<int>> {
            { bassNote, bassAndSnareTimings.GetRange(0, bassCount) },
            { snareNote, bassAndSnareTimings.GetRange(bassCount, snareCount) },
            { hiHatNote, hiHatTimings.GetRange(0, hiHatCount) }
        };
    }

    private static readonly List<IEnumerable<Interval>> AllScales = new() {
        ScaleIntervals.Aeolian, ScaleIntervals.Augmented, ScaleIntervals.Bebop,
        ScaleIntervals.BebopDominant, ScaleIntervals.BebopMajor, ScaleIntervals.BebopMinor,
        ScaleIntervals.Blues, ScaleIntervals.Dominant, ScaleIntervals.Dorian,
        ScaleIntervals.DoubleHarmonicMajor, ScaleIntervals.HarmonicMajor, ScaleIntervals.HarmonicMinor,
        ScaleIntervals.HungarianMajor, ScaleIntervals.HungarianMinor
    };


    /// Performs an in-place shuffle
    /// based on this: https://github.com/dotnet/runtime/blob/v9.0.0/src/libraries/System.Private.CoreLib/src/System/Random.cs#L316
    private static void Shuffle<T>(List<T> values, Random random) {
        var n = values.Count;
        for (var i = 0; i < n - 1; i++) {
            var j = random.Next(i, n);
            if (j != i) {
                (values[i], values[j]) = (values[j], values[i]);
            }
        }
    }

    private static T GetRandomElement<T>(IEnumerable<T> collection, Random random) {
        var list = collection.ToList();
        return list[random.Next(list.Count)];
    }
}
}