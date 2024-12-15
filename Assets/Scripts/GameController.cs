using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Backend;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using Melanchall.DryWetMidi.MusicTheory;
using Melanchall.DryWetMidi.Standards;
using MidiPlayerTK;
using PimDeWitte.UnityMainThreadDispatcher;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MusicalNote = Melanchall.DryWetMidi.MusicTheory.Note;

public class GameController : MonoBehaviour {
    private MidiProcessor _midiProcessor;
    private MidiProcessor _generatedSongMidiProcessor;
    private Playback _playback;
    private Playback _generatedSongPlayback;
    private float _trackPosition = 0;
    private float _currentBpm;
    private float OneSecNoteWidth => _currentBpm / 100 * 4.5f;

    private Dictionary<int, GameObject> _trackDepthToTrackParentGameObject = new();
    private readonly List<NotePlatform> _platforms = new();

    private int _currentDepth = 0;

    private const int MinPlatformHeight = -12;
    private const int MaxPlatformHeight = 15;

    private GameObject _helperLayer;
    public const int HelperDepth = -5;
    private bool _isOnHelperLayer = false;

    // for current helper platform placement
    private int _currentHelperHeight = MinPlatformHeight;
    private float _lastHelperPlatformTime = -1;

    [SerializeField] private NotePlatform notePlatformPrefab;
    [SerializeField] private LayerDisplay layerDisplayPrefab;

    private GameObject _terrainParent;
    [SerializeField] private LevelCursor levelCursor;

    [SerializeField] private LayoutGroup layersDisplayLayoutGroup;
    private List<LayerDisplay> _layerDisplays = new();

    private double _score = 0;
    private int _lastTouchedDepth = -1;
    private readonly List<(float Time, int Depth)> _lastTouched = new();
    [SerializeField] private TextMeshProUGUI scoreText;

    [SerializeField] private bool playWithMaestro = false;
    [SerializeField] private MidiStreamPlayer midiStreamPlayer;
    [SerializeField] private PlayerMovement playerMovement;

    private readonly List<MPTKEvent> _playedMaestroEvents = new();
    private int _customMidiChannel;


    private enum State {
        Lobby,
        Level,
        PlaybackFinished
    }

    private State _state = State.Lobby;


    public void Reset() {
        _score = 0;
        _lastTouchedDepth = -1;
        _lastTouched.Clear();
        levelCursor.Reset();
        FindObjectOfType<NoteInputHandler>().ResetButtons();
        levelCursor.transform.SetParent(_terrainParent.transform);
        playerMovement.transform.SetParent(_terrainParent.transform);
        playerMovement.transform.localPosition = Vector3.zero;
        _isOnHelperLayer = false;
        _currentHelperHeight = MinPlatformHeight;

        _state = State.Lobby;

        DisposePlaybacks();
        foreach (var e in _playedMaestroEvents.Where(e => !e.IsOver)) {
            midiStreamPlayer.MPTK_StopEvent(e);
        }

        _playedMaestroEvents.Clear();

        _platforms.ForEach(p => Destroy(p.gameObject));
        _platforms.Clear();
        _layerDisplays.ForEach(d => Destroy(d.gameObject));
        _layerDisplays.Clear();
        _trackDepthToTrackParentGameObject.ToList().ForEach(x => Destroy(x.Value));
        _trackDepthToTrackParentGameObject.Clear();
    }

    public void StartLevel(Stream midi) {
        if (_state == State.Level) return;
        _state = State.Level;
        Debug.Log("Level started");
        new Thread(() => StartLevelInner(midi)).Start();
    }

    private void StartLevelInner(Stream midi) {
        _midiProcessor = new MidiProcessor(midi);
        InitializeFilePlayback(_midiProcessor.MidiFile);

        // 120 is the midi default
        _currentBpm = 120f;

        // for custom note playback default
        _customMidiChannel = midiStreamPlayer.MPTK_Channels.Count() - 1;
        midiStreamPlayer.MPTK_Channels[_customMidiChannel].PresetNum =
            GeneralMidiProgram.AcousticGrandPiano.AsSevenBitNumber();

        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            _trackDepthToTrackParentGameObject = _midiProcessor.TrackDepths
                .ToDictionary(track => track.Depth, track
                    => new GameObject($"track depth {track.Depth}") {
                        transform = { parent = _terrainParent.transform }
                    });
            _layerDisplays = _midiProcessor.TrackDepths.OrderBy(track => track.Depth).Select(track => {
                var layerDisplay = Instantiate(layerDisplayPrefab, layersDisplayLayoutGroup.transform);
                layerDisplay.trackDepth = track.Depth;
                layerDisplay.GetComponent<Image>().color = NotePlatform.AllColors[track.Depth];
                return layerDisplay;
            }).ToList();

            HopPlane(0);

            Debug.Log("Starting playback...");
            _playback.Start();
        });
    }

    // midiStreamPlayer can only be started in Start, not Awake
    private void Start() {
        _terrainParent = new GameObject("Dynamic terrain parent");
        _helperLayer = new GameObject("Helper layer") { transform = { parent = _terrainParent.transform } };
        midiStreamPlayer.MPTK_StartMidiStream();
    }

    public void PlayCustomNote(MusicalNote note, long durationMillis = 100) {
        var previousInstrument = midiStreamPlayer.MPTK_Channels[_customMidiChannel].PresetNum;
        midiStreamPlayer.MPTK_Channels[_customMidiChannel].PresetNum =
            GeneralMidiProgram.AcousticGrandPiano.AsSevenBitNumber();
        midiStreamPlayer.MPTK_PlayEvent(new MPTKEvent {
                Channel = _customMidiChannel,
                Duration = durationMillis,
                Value = note.NoteNumber,
                Velocity = 70
            }
        );
        midiStreamPlayer.MPTK_Channels[_customMidiChannel].PresetNum = previousInstrument;
    }

    public void InsertSong(string midiPath) {
        _playback.Stop();
        _generatedSongMidiProcessor = new MidiProcessor(midiPath);

        _generatedSongPlayback = _generatedSongMidiProcessor.MidiFile.GetPlayback();

        _generatedSongPlayback.NotesPlaybackStarted += (_, e)
            => OnNotesPlaybackStarted(e, true);

        _generatedSongPlayback.Finished += (_, _) => {
            _generatedSongPlayback.Dispose();
            _generatedSongPlayback = null;
            _generatedSongMidiProcessor = null;
            _playback.Start();
        };
        _generatedSongPlayback.Start();
    }

    private void Update() {
        scoreText.text = ((int)_score).ToString();
        if (_state == State.Level) {
            _score += Time.deltaTime;
            _trackPosition = levelCursor.transform.localPosition.x;
            _currentBpm = (float)(_generatedSongPlayback?.GetCurrentBpm() ?? _playback?.GetCurrentBpm() ?? 0f);
            levelCursor.SetMusicSpeed(OneSecNoteWidth);
            playerMovement.runSpeed = OneSecNoteWidth * 11;

            PlaceHelperNote();
        }
    }

    private void PlaceHelperNote() {
        var currentTime = Time.time;
        const float helperPlatformTimeWidth = 0.25f;
        if (currentTime < _lastHelperPlatformTime + helperPlatformTimeWidth) return;

        _lastHelperPlatformTime = currentTime;

        _lastHelperPlatformTime = currentTime;
        _currentHelperHeight = _currentHelperHeight >= MaxPlatformHeight
            ? MinPlatformHeight
            : _currentHelperHeight + 3;

        var (position, scale) = GetPlatformPlacement(_currentHelperHeight, helperPlatformTimeWidth);
        var platform = Instantiate(notePlatformPrefab);
        platform.Initialize(_helperLayer.transform,
            position, scale, HelperDepth, null,
            _currentDepth, _isOnHelperLayer,
            null);
        _platforms.Add(platform);
    }

    private void InitializeFilePlayback(MidiFile midiFile) {
        Debug.Log("Initializing playback...");

        _playback = midiFile.GetPlayback();
        _playback.NotesPlaybackStarted += OnNotesPlaybackStarted;
        _playback.EventCallback = (midiEvent, _, _) => {
            if (midiEvent.EventType == MidiEventType.ProgramChange) {
                var pce = (midiEvent as ProgramChangeEvent)!;
                midiStreamPlayer.MPTK_Channels[pce.Channel].PresetNum = pce.ProgramNumber;
            }

            return midiEvent;
        };
        _playback.Finished += (_, _) => UnityMainThreadDispatcher.Instance().Enqueue(()
            => StartCoroutine(Coroutines.DoLater(5, () => {
                _state = State.PlaybackFinished;
                _currentBpm = 0;
                levelCursor.SetMusicSpeed(OneSecNoteWidth);
            })));
    }

    private void OnApplicationQuit() {
        DisposePlaybacks();

        Debug.Log("Playback and device released.");

        if (Directory.Exists(MidiProcessor.OutputDir)) {
            try {
                Directory.Delete(MidiProcessor.OutputDir, true);
            }
            catch {
                // ignored
            }
        }
    }

    private void DisposePlaybacks() {
        Debug.Log("Releasing playback...");
        if (_playback is not null) {
            _playback.NotesPlaybackStarted -= OnNotesPlaybackStarted;
            _playback.Dispose();
            _playback = null;
        }

        if (_generatedSongPlayback is not null) {
            _generatedSongPlayback.NotesPlaybackStarted -= OnNotesPlaybackStarted;
            _generatedSongPlayback.Dispose();
            _generatedSongPlayback = null;
        }
    }

    private void OnNotesPlaybackStarted(object sender, NotesEventArgs e)
        => OnNotesPlaybackStarted(e, false);

    private void OnNotesPlaybackStarted(NotesEventArgs e, bool customMelody) {
        var noteDatas = e.Notes.Select(_midiProcessor.VisualizeNote).ToArray();

        // play with Maestro as soon as possible
        if (playWithMaestro) {
            PlayNotesWithMaestro(noteDatas, customMelody);
        }

        UnityMainThreadDispatcher.Instance().Enqueue(() =>
            PlaceNotes(noteDatas.Where(data => data.TrackDepth != -1), customMelody));
    }

    private void PlayNotesWithMaestro(IEnumerable<NoteViewData> noteDatas, bool customMelody = false) {
        foreach (var data in noteDatas) {
            var channel = (int)data.Note.Channel;
            if (customMelody && channel != GeneralMidi.PercussionChannel) {
                channel = _customMidiChannel;
            }

            var ev = new MPTKEvent {
                Channel = channel,
                Duration = (long)data.Length.TotalMilliseconds,
                Value = data.Note.NoteNumber,
                Velocity = data.Note.Velocity
            };
            _playedMaestroEvents.Add(ev);
            midiStreamPlayer.MPTK_PlayEvent(ev);
        }
    }

    // Unity main thread only
    private void PlaceNotes(IEnumerable<NoteViewData> noteDatas, bool customMelody = false) {
        // this method can be called even after a reset has destroyed all infrastructures, due to a delay
        if (_playback is null) return;

        foreach (var data in noteDatas) {
            var (position, scale) = TranslateNoteBox(data);
            var trackDepth = data.TrackDepth;
            if (customMelody && data.Note.Channel != GeneralMidi.PercussionChannel) {
                trackDepth = _currentDepth;
            }

            var platform = Instantiate(notePlatformPrefab);
            platform.Initialize(_trackDepthToTrackParentGameObject[trackDepth].transform,
                position, scale, trackDepth, data, _currentDepth, _isOnHelperLayer, this);
            _platforms.Add(platform);
        }
    }

    private (Vector2 Position, Vector2 Scale) TranslateNoteBox(NoteViewData data) {
        var relativeNoteHeight = data.Note.NoteNumber - _midiProcessor.MedianNoteNumber;

        // musically clamped to the nearest octave that's still in range
        var displayedNoteHeight = relativeNoteHeight;
        var noteHeightOctaveAdjustment = 1f;
        while (displayedNoteHeight > MaxPlatformHeight) {
            displayedNoteHeight -= Octave.OctaveSize;
            if (data.Note.Channel != GeneralMidi.PercussionChannel) {
                noteHeightOctaveAdjustment /= 1.2f;
            }
        }

        while (displayedNoteHeight < MinPlatformHeight) {
            displayedNoteHeight += Octave.OctaveSize;
            if (data.Note.Channel != GeneralMidi.PercussionChannel) {
                noteHeightOctaveAdjustment *= 1.2f;
            }
        }

        var (position, scale) = GetPlatformPlacement(displayedNoteHeight, data.Scale.Width);
        const float spriteGapCorrection = 1.05f;
        scale.x *= spriteGapCorrection;
        scale.y *= noteHeightOctaveAdjustment;
        return (position, scale);
    }

    private (Vector2 Position, Vector2 Scale) GetPlatformPlacement(int height, float timeWidth) {
        const float platformHeight = 0.8f;
        var length = timeWidth * OneSecNoteWidth;
        var position = new Vector2(
            _trackPosition + 15f + length / 2,
            height * platformHeight);
        var scale = new Vector2(
            length,
            platformHeight);
        return (position, scale);
    }

    public enum HopDirection {
        Forward,
        Backward,
        Helper
    }

    public void TryPlaneHop(HopDirection direction) {
        var desiredTrackDepth = direction switch {
            HopDirection.Forward => _currentDepth + 1,
            HopDirection.Backward => _currentDepth - 1,
            HopDirection.Helper => HelperDepth,
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };

        if (desiredTrackDepth == HelperDepth) {
            _isOnHelperLayer = !_isOnHelperLayer;
            HopPlane(_currentDepth);
        }
        else if (desiredTrackDepth >= 0 && desiredTrackDepth < _midiProcessor.TrackDepths.Count) {
            _isOnHelperLayer = false;
            HopPlane(desiredTrackDepth);
        }
    }

    public void PlayerAtBorder(Vector2 relativePosition) {
        Reset();
    }

    private void HopPlane(int desiredTrackDepth) {
        _currentDepth = desiredTrackDepth;
        _platforms.ForEach(platform => platform.SetCurrentDepth(_currentDepth, _isOnHelperLayer));
        const float planeSpacing = 1.6f;
        const float nonNeighbourGap = 2.4f;
        var helperExtraDistance = _isOnHelperLayer ? planeSpacing : 0;
        var currentTrackParent = _isOnHelperLayer
            ? _helperLayer
            : _trackDepthToTrackParentGameObject[_currentDepth];
        playerMovement.transform.SetParent(currentTrackParent.transform, false);
        levelCursor.transform.SetParent(currentTrackParent.transform, false);

        // neighbouring tracks should be closer
        // other tracks should be farther apart
        _trackDepthToTrackParentGameObject.ToList().ForEach(item => {
            var discreteDistance = Math.Abs(item.Key - _currentDepth);
            var direction = Math.Sign(item.Key - _currentDepth);
            var distance = discreteDistance * planeSpacing;
            if (discreteDistance > 1) distance += nonNeighbourGap + helperExtraDistance;
            item.Value.transform.localPosition = Vector3.forward * (distance * direction);
        });

        _layerDisplays.ForEach(display => display.SetCurrentTrack(_currentDepth));
    }

    public void OnNoteSteppedOn(NotePlatform notePlatform) {
        var currentTime = Time.time;
        _lastTouched.RemoveAll(item => item.Time < currentTime - 10);
        var timeSincePlatformCreation = currentTime - notePlatform.startTime;
        const int maxPoint = 20;
        const int minPoint = 5;
        var extraPoints = Math.Exp(-timeSincePlatformCreation / 5) * (maxPoint - minPoint) + minPoint;

        var recentOtherLayersCount = _lastTouched
            .Where(layer => layer.Depth != notePlatform.Data.TrackDepth)
            .Select(layer => layer.Depth)
            .Distinct().Count();
        if (_lastTouchedDepth != notePlatform.Data.TrackDepth) {
            recentOtherLayersCount = Math.Max(recentOtherLayersCount, 1);
        }

        extraPoints *= recentOtherLayersCount + 1;

        _score += extraPoints;
        _lastTouched.Add((Time: 0f, Depth: notePlatform.Data.TrackDepth));
        _lastTouchedDepth = notePlatform.Data.TrackDepth;
    }
}