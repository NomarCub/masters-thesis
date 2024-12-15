using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Backend;
using Backend.GETMusicInteractor;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using NUnit.Framework;

namespace Tests.Backend {
public class BackendTest {
    // path segment of the NUnit test runner path when run from Rider
    private const string NunitRunnerSubPath = @"\\Temp\\Bin\\Debug\\.*";

    // unified base path for running with NUnit or Unity runner
    private static readonly string BasePath = Regex.Replace(Directory.GetCurrentDirectory(), NunitRunnerSubPath, "");

    private static readonly IEnumerable<string> MidiFilePaths = Directory
        .GetFiles(Path.Combine(BasePath, "Assets/Resources/songs/"))
        .Where(path => path.EndsWith(".bytes"));

    [Test]
    public void MidiProcessorTest() {
        var processors = MidiFilePaths.Select(path => new MidiProcessor(path)).ToList();
        Assert.IsTrue(processors.All(processor => processor.TrackDepths.Any()));
    }

    [Test]
    public void ProceduralMusicGeneratorTest() {
        new List<ProceduralMusicGenerator.GeneratorOptions> {
            new() { Seed = 1, Scale = new Scale(ScaleIntervals.Major, NoteName.C) },
            new() { Seed = 2, Scale = new Scale(ScaleIntervals.Minor, NoteName.A) },
            new() { Seed = 3, Scale = new Scale(ScaleIntervals.MajorBlues, NoteName.C) },
        }.ForEach(options => MidiProcessor.WriteMidiToFile(
            ProceduralMusicGenerator.GenerateMusicMidiFile(options),
            $"{options.Seed}-"));
    }

    [Test]
    public void AIMusicGeneratorTest() {
        var processor = new MidiProcessor(MidiFilePaths.First());

        var converted = processor.ProcessForGETMusic();
        TestContext.WriteLine("converted path: " + converted.Path);

        var aiMusicObject = AIMusicGenerator.GenerateMusic(converted.Path,
            new MusicalTimeSpan(), MusicalTimeSpan.Whole, MusicalTimeSpan.Whole * 2,
            new() { GETMusicInstrument.Lead, GETMusicInstrument.Drums });
        var process = Process.Start(aiMusicObject.ProcessStartInfo);
        process.WaitForExit();
        TestContext.WriteLine("AI gen path: " + aiMusicObject.OutputPath);

        var convertedBack = processor.RemapGETMusicFile(converted.Path, converted.Mapping);
        convertedBack.Write(Path.Combine(BasePath, MidiProcessor.OutputDir + "converted_back.mid"));
    }
}
}