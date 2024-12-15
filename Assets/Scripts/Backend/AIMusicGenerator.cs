using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Melanchall.DryWetMidi.Interaction;

namespace Backend.GETMusicInteractor {
// TODO: map to general midi programs, collection in processor
// TODO: GETMusic source comment
public enum GETMusicInstrument {
    Lead = 0,
    Bass = 1,
    Drums = 2,
    Guitar = 3,
    Piano = 4,
    String = 5,
    Chord = 6
}

public class Position {
    public Position(GETMusicInstrument instrument, MusicalTimeSpan start = null, MusicalTimeSpan end = null) {
        _instrument = instrument;
        _start = start ?? new MusicalTimeSpan();
        _end = end;
    }

    private readonly GETMusicInstrument _instrument;
    private readonly MusicalTimeSpan _start;
    private readonly MusicalTimeSpan _end;

    private static long TimeStampToNumber(MusicalTimeSpan musicalTimeSpan)
        => musicalTimeSpan.ChangeDenominator(16).Numerator;

    public override string ToString()
        => $"{(int)_instrument},{TimeStampToNumber(_start)},{(_end is not null ? TimeStampToNumber(_end) : null)}";
}

public static class AIMusicGenerator {
    // see create_pos_from_str in GETMusic
    private static string PositionToString(List<Position> positions) => string.Join(";",
        positions.Select(track => track.ToString()));

    private static (List<Position> ConditionPositions, List<Position> EmptyPositions) GeneratePositions(
        MusicalTimeSpan start, MusicalTimeSpan conditionLength, MusicalTimeSpan generationLength,
        List<GETMusicInstrument> instruments) {
        var allInstruments = Enum.GetValues(typeof(GETMusicInstrument)).Cast<GETMusicInstrument>().ToList();
        var emptyInstruments = allInstruments.Except(instruments);
        var conditionPositions = instruments.Select(instrument
            => new Position(instrument, start, start + conditionLength)).ToList();
        var emptyPositions = emptyInstruments.Select(inst => new Position(inst)).ToList();
        emptyPositions.AddRange(instruments.Select(inst
            => new Position(inst, start + conditionLength + generationLength)).ToList());
        return (conditionPositions, emptyPositions);
    }

    public static (ProcessStartInfo ProcessStartInfo, string OutputPath) GenerateMusic(string midiPath,
        MusicalTimeSpan start, MusicalTimeSpan conditionLength, MusicalTimeSpan generationLength,
        List<GETMusicInstrument> instruments) {
        var (conditionPositions, emptyPositions) =
            GeneratePositions(start, conditionLength, generationLength, instruments);
        return GenerateMusic(midiPath, conditionPositions, emptyPositions);
    }

    private static (ProcessStartInfo ProcessStartInfo, string OutputPath) GenerateMusic(string midiPath,
        List<Position> conditionPositions, List<Position> emptyPositions) {
        var getMusicFolderPath = CliOptions.GETMusicFolderPath;
        var pretrainedModelPath = CliOptions.GETMusicPretrainedModelPath;
        Trace.Assert(getMusicFolderPath is not null && pretrainedModelPath is not null);

        var commandArguments = "position_generation.py " + $"--load_path {pretrainedModelPath} " +
                               $"--file_path {midiPath} " + "--batch " +
                               $"--condition_positions \"{PositionToString(conditionPositions)}\" " +
                               $"--empty_positions \"{PositionToString(emptyPositions)}\"";

        var processStartInfo = new ProcessStartInfo {
            FileName = "python3.10" + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : ""),
            WorkingDirectory = getMusicFolderPath,
            Arguments = commandArguments,
            CreateNoWindow = true,
            UseShellExecute = false,
        };
        return (processStartInfo,
            Path.Combine(
                Path.GetDirectoryName(midiPath),
                "position-" + Path.GetFileName(midiPath)));
    }
}
}