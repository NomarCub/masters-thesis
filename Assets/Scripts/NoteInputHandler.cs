using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Backend;
using Backend.GETMusicInteractor;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;
using Melanchall.DryWetMidi.MusicTheory;
using Melanchall.DryWetMidi.Standards;
using PimDeWitte.UnityMainThreadDispatcher;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using MusicalNote = Melanchall.DryWetMidi.MusicTheory.Note;

public class NoteInputHandler : MonoBehaviour {
    [SerializeField] private NoteInputToggle noteInputTogglePrefab;

    [SerializeField] private Button playMyNotesButton;
    [SerializeField] private Button generateSongButton;
    [SerializeField] private GridLayoutGroup noteInputGridLayoutGroup;

    private readonly List<NoteInputToggle> _noteInputToggles = new();

    private const int TimePointsCount = 16;
    private readonly MusicalTimeSpan _inputNoteLength = MusicalTimeSpan.Eighth;

    [SerializeField] private GameController gameController;

    public readonly Scale Scale = new(ScaleIntervals.Major, NoteName.C);

    private void Awake() {
        if (CliOptions.GETMusicFolderPath is null || CliOptions.GETMusicPretrainedModelPath is null) {
            generateSongButton.gameObject.SetActive(false);
        }

        // setup note input grid
        var notes = Scale.GetAscendingNotes(MusicalNote.Get(Scale.RootNote, 4))
            .Take(Scale.Intervals.Count() * 2 + 1).Reverse().ToList();
        var timePoints = Enumerable.Range(0, TimePointsCount).ToList();
        noteInputGridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        noteInputGridLayoutGroup.constraintCount = timePoints.Count;
        foreach (var note in notes) {
            foreach (var timePoint in timePoints) {
                var noteInputButton = Instantiate(noteInputTogglePrefab, noteInputGridLayoutGroup.transform);
                noteInputButton.Initialize(this, note, timePoint);
                _noteInputToggles.Add(noteInputButton);
            }
        }

        playMyNotesButton.onClick.AddListener(() => {
            var playback = BuildNoteInputs(new PatternBuilder().ProgramChange(GeneralMidiProgram.AcousticGrandPiano))
                .Build().ToFile(TempoMap.Default).GetPlayback();
            // use an existing MIDI file later
            // use MidiFile.TakePart later, to cut only a part for performance
            playback.NotesPlaybackStarted += (_, notes) => {
                foreach (var note in notes.Notes) {
                    gameController.PlayCustomNote(note.GetMusicTheoryNote(),
                        (long)note.TimeAs<MetricTimeSpan>(TempoMap.Default).TotalMilliseconds);
                }
            };
            playback.Finished += (_, _) => playback.Dispose();
            playback.Start();
        });

        generateSongButton.onClick.AddListener(() => {
            var totalInputLength = _inputNoteLength * TimePointsCount;
            var builder = BuildNoteInputs(new PatternBuilder().ProgramChange(GeneralMidiProgram.AcousticGrandPiano));
            // padding after actual user input, that we later ignore with GETMusic
            builder.MoveToTime(totalInputLength);
            var generatedLength = totalInputLength;
            builder.Note(Notes.C0, generatedLength);

            var inputPath = MidiProcessor.WriteMidiToFile(builder.Build().ToFile(TempoMap.Default));

            var (processInfo, outputPath) = AIMusicGenerator.GenerateMusic(inputPath, new MusicalTimeSpan(),
                totalInputLength, generatedLength,
                // TODO: vary instruments
                new() { GETMusicInstrument.Piano, GETMusicInstrument.Drums });

            ResetButtons();
            EnableAllGUI(false);

            Debug.Log($"Starting GETMusic processing. args: {processInfo.Arguments}");
            StartCoroutine(Coroutines.FromAction(() => {
                var process = Process.Start(processInfo)!;
                process.EnableRaisingEvents = true;
                process.Exited += (_, _) => {
                    Debug.Log("Finished GETMusic processing");
                    EnableAllGUI(true);
                    gameController.InsertSong(outputPath);
                };
            }));
        });
    }

    public void ResetButtons() {
        _noteInputToggles.ForEach(toggle => toggle.IsOn = false);
    }

    private void EnableAllGUI(bool enable) {
        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            generateSongButton.gameObject.SetActive(enable);
            playMyNotesButton.gameObject.SetActive(enable);
            noteInputGridLayoutGroup.gameObject.SetActive(enable);
        });
    }

    public PatternBuilder BuildNoteInputs(PatternBuilder builder = null) {
        builder ??= new PatternBuilder();
        const string noteInputsAnchor = "NoteInputs";
        builder
            .SetNoteLength(_inputNoteLength)
            .SetStep(_inputNoteLength)
            .Anchor(noteInputsAnchor);
        _noteInputToggles.Where(toggle => toggle.IsOn).ToList().ForEach(toggle => builder
            .MoveToLastAnchor(noteInputsAnchor)
            .StepForward(_inputNoteLength * toggle.timePoint)
            .Note(toggle.Note));
        return builder;
    }

    public void OnNoteInputActivated(MusicalNote note, bool on) {
        var hasAnyNotes = _noteInputToggles.Any(toggle => toggle.IsOn);
        generateSongButton.interactable = hasAnyNotes;
        playMyNotesButton.interactable = hasAnyNotes;

        if (on) {
            gameController.PlayCustomNote(note);
        }
    }
}