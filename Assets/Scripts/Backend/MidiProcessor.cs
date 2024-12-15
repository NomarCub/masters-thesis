using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Standards;
using MidiChannelNumber = Melanchall.DryWetMidi.Common.FourBitNumber;
using GETMusicMapping = System.Collections.Generic.Dictionary<int, Melanchall.DryWetMidi.Standards.GeneralMidiProgram>;
using TracksInfo = System.Collections.Generic.List<(
    int Index,
    // List<MidiChannelNumber>
    System.Collections.Generic.List<Melanchall.DryWetMidi.Common.FourBitNumber> Channels,
    Melanchall.DryWetMidi.Standards.GeneralMidiProgram MostCommonInstrument)>;
using InstrumentEventsStats = System.Collections.Generic.List<(
    int Track,
    // MidiChannelNumber
    Melanchall.DryWetMidi.Common.FourBitNumber Channel,
    Melanchall.DryWetMidi.Standards.GeneralMidiProgram Instrument,
    int Count)>;


namespace Backend {
public class NoteViewData {
    public int TrackDepth;
    public RectangleF Box;
    public Note Note;
    public MetricTimeSpan Length;

    public PointF NoteCenter => Box.Location + new SizeF(Box.Size.Width, -Box.Size.Height) / 2;
    public SizeF Scale => Box.Size;

    public override string ToString() => $"{{ch:{Note.Channel},note:{Note}-{Note.NoteNumber},box:{Box}}}";
}

public class MidiProcessor {
    public static readonly string OutputDir = Path.Combine(Path.GetTempPath(), "music-sidescroller");

    private static readonly ReadingSettings ReadingSettings = new() {
        EndOfTrackStoringPolicy = EndOfTrackStoringPolicy.Store,
        MissedEndOfTrackPolicy = MissedEndOfTrackPolicy.Abort,
    };

    public readonly MidiFile MidiFile;
    private readonly TempoMap _tempoMap;
    public readonly SevenBitNumber MedianNoteNumber;

    // each list item represents a track as a list of its channels
    // track indexes might have gaps (non instrument tracks)
    private readonly TracksInfo _tracksInfo;

    // number of events, grouped by track, then channel and instrument
    private readonly InstrumentEventsStats _instrumentEventsStats;

    // list of depths as they are used in gameplay
    // the higher the depth, the less important the layer is it is, and the more hops the player needs to get to it
    public readonly List<(int Depth, MidiChannelNumber Channel)> TrackDepths;

    public MidiProcessor(string path) : this(MidiFile.Read(path, ReadingSettings)) {
    }

    public MidiProcessor(Stream stream) : this(MidiFile.Read(stream, ReadingSettings)) {
    }

    private MidiProcessor(MidiFile midiFile) {
        MidiFile = midiFile;
        _tempoMap = MidiFile.GetTempoMap();

        _instrumentEventsStats = BuildInstrumentChannelTrackStatistics();
        _tracksInfo = BuildTracksInfo();
        MedianNoteNumber = GetMedianNoteNumber();

        TrackDepths = GetTrackDepths();
    }

    private int GetTrackDepth(Note note) {
        var matchingDepthIndex = TrackDepths.FindIndex(item => item.Channel == note.Channel);
        return matchingDepthIndex == -1 ? -1 : TrackDepths[matchingDepthIndex].Depth;
    }

    private List<(int Depth, MidiChannelNumber Channel)> GetTrackDepths() {
        var firstTrackChannel = MidiFile.GetNotes().First().Channel;
        var notesByChannel = _instrumentEventsStats
            .Where(item => item.Channel != firstTrackChannel)
            .GroupBy(item => item.Channel)
            .Select(group => (Channel: group.Key, Count: group.Sum(i => i.Count)))
            .OrderByDescending(item => item.Count)
            // limit to 10 layers
            .Take(9)
            .Select((item, order) => (Depth: order + 1, item.Channel));
        var depths = new List<(int Depth, MidiChannelNumber Channel)> { (0, firstTrackChannel) };
        depths.AddRange(notesByChannel);
        return depths;
    }

    // not real median, but suffices here
    // see: https://stackoverflow.com/a/4134390/8838027
    private SevenBitNumber GetMedianNoteNumber() {
        var noteNumbers = MidiFile.GetNotes()
            .Where(note => note.Channel != GeneralMidi.PercussionChannel)
            .Select(note => note.NoteNumber)
            .ToList();
        return noteNumbers.OrderBy(x => x).Skip(noteNumbers.Count / 2).FirstOrDefault();
    }

    private TracksInfo BuildTracksInfo() {
        var channelsByTrack = MidiFile.GetRealTracksIndexed()
            .Select(item => {
                var channels = item.Track.GetChannels().ToList();
                var instrumentsByEventCount = _instrumentEventsStats
                    .Where(events => events.Track == item.Index)
                    .OrderByDescending(events => events.Count)
                    .Take(1).ToList();
                if (!instrumentsByEventCount.Any()) channels.Clear();
                var instrument = instrumentsByEventCount.FirstOrDefault().Instrument;
                return (item.Index, Channels: channels, MostCommonInstrument: instrument);
            })
            .Where(item => item.Channels.Any())
            .ToList();

        Trace.Assert(channelsByTrack.Any());
        return channelsByTrack;
    }

    // see inst_to_row in GETMusic
    // ordered here based on precedence for converting tracks first
    private static readonly List<GeneralMidiProgram> GETMusicInstruments = new() {
        GeneralMidiProgram.Lead1, // 80 - lead
        GeneralMidiProgram.AcousticGuitar2, // 25 - guitar
        GeneralMidiProgram.StringEnsemble1, // 48 - string
        GeneralMidiProgram.AcousticGrandPiano, // 0 - piano
        GeneralMidiProgram.AcousticBass, // 32 - bass
    };

    private GETMusicMapping TrackToGETMusicInstrumentsMapping() {
        // calls ToList to make a copy to avoid removing elements from tracksToProcess while iterating on it
        // phase 0 - any track potentially needs mapping
        var tracksToGETMusicInstrument = new GETMusicMapping();
        var tracksToProcess = _tracksInfo.ToList();

        // phase 1 - drums are marked by channel, not instrument and are left in place
        tracksToProcess.RemoveAll(track => track.Channels.Contains(GeneralMidi.PercussionChannel));

        // phase 2 - map tracks to their most common instruments when the instrument is already GETMusic compatible
        tracksToProcess
            .Where(track => GETMusicInstruments.Contains(track.MostCommonInstrument))
            .ToList().ForEach(track => {
                tracksToProcess.Remove(track);
                tracksToGETMusicInstrument[track.Index] = track.MostCommonInstrument;
            });

        // phase 3 - manual instrument group based heuristic
        tracksToProcess.ToList().ForEach(track => {
            var instrument = track.MostCommonInstrument;
            GeneralMidiProgram? replacementInstrument = instrument switch {
                <= GeneralMidiProgram.TangoAccordion => GeneralMidiProgram.AcousticGrandPiano,
                <= GeneralMidiProgram.GuitarHarmonics => GeneralMidiProgram.AcousticGuitar2,
                <= GeneralMidiProgram.SynthBass2 => GeneralMidiProgram.AcousticBass,
                <= GeneralMidiProgram.SynthStrings2 => GeneralMidiProgram.StringEnsemble1,
                _ => null
            };

            if (replacementInstrument is not null &&
                !tracksToGETMusicInstrument.ContainsValue(replacementInstrument.Value)) {
                tracksToProcess.RemoveAll(innerTrack => innerTrack.Index == track.Index);
                tracksToGETMusicInstrument[track.Index] = replacementInstrument.Value;
            }
        });

        // phase 4 - greedily assign all remaining instruments until either runs out:
        //    - the assignable GETMusic instruments
        //    - the unassigned instruments in the track
        // the current metric assigns the track with the most events first
        var remainingGetMusicInstruments = GETMusicInstruments
            .Where(instrument => !tracksToGETMusicInstrument.ContainsValue(instrument)).ToList();
        remainingGetMusicInstruments.ForEach(instrument => {
            var trackToMap = tracksToProcess
                .OrderByDescending(track => _instrumentEventsStats
                    .Where(events => events.Track == track.Index)
                    .Sum(item => item.Count)
                ).FirstOrDefault();
            if (trackToMap != default) {
                tracksToProcess.Remove(trackToMap);
                tracksToGETMusicInstrument[trackToMap.Index] = instrument;
            }
        });

        return tracksToGETMusicInstrument;
    }

    public MidiFile RemapGETMusicFile(string path, GETMusicMapping mapping) {
        var reverseMapping = mapping.Select(item => (
                GETMusicInstrument: item.Value,
                NormalInstrument: _tracksInfo.Find(track => track.Index == item.Key).MostCommonInstrument))
            .ToList();
        var midiFile = MidiFile.Read(path);

        midiFile.ProcessTimedEvents(
            ev => {
                var change = (ev.Event as ProgramChangeEvent)!;
                if (change.Channel != GeneralMidi.PercussionChannel) {
                    var map = reverseMapping
                        .Find(item => item.GETMusicInstrument.AsSevenBitNumber() == change.ProgramNumber);
                    if (map != default) {
                        change.ProgramNumber = map.NormalInstrument.AsSevenBitNumber();
                    }
                }
            },
            ev => ev.Event is ProgramChangeEvent,
            hint: TimedEventProcessingHint.None);

        return midiFile;
    }

    // sometimes the first track contains the tempo and other information
    // so we merge its non-note events, instead of fully removing it
    private static void RemoveTracks(MidiFile midiFile, List<int> indexes) {
        var toRemove = midiFile.GetRealTracksIndexed()
            .Where(item => indexes.Contains(item.Index))
            .Select(item => item.Track).ToList();
        while (toRemove.Any()) {
            var track1 = toRemove.First();
            track1.Events.RemoveAll(@event => @event is ChannelEvent);
            var tracks = midiFile.GetRealTracks().ToList();
            var track1Idx = tracks.IndexOf(track1);
            var track2 = track1 == tracks.Last()
                ? tracks[track1Idx - 1]
                : tracks[track1Idx + 1];
            var chunk2Idx = midiFile.Chunks.IndexOf(track2);
            var merged = new[] { track1, track2 }.Merge();
            midiFile.Chunks[chunk2Idx] = merged;
            if (toRemove.Contains(track2)) {
                toRemove.Remove(track2);
                toRemove.Add(merged);
            }

            midiFile.Chunks.Remove(track1);
            toRemove.Remove(track1);
        }
    }

    public (string Path, GETMusicMapping Mapping) ProcessForGETMusic() {
        var mapping = TrackToGETMusicInstrumentsMapping();
        var clone = MidiFile.Clone();
        clone.GetRealTracksIndexed()
            .Where(item => mapping.ContainsKey(item.Index))
            .ToList().ForEach(item => item.Track.ProcessTimedEvents(
                ev => {
                    var change = (ev.Event as ProgramChangeEvent)!;
                    change.ProgramNumber = mapping[item.Index].AsSevenBitNumber();
                },
                ev => ev.Event is ProgramChangeEvent,
                hint: TimedEventProcessingHint.None));

        // phase 5 - remove tracks for performance
        // RemoveTracks(clone, new List<int> { 1, 2, 3, 5 });

        return (WriteMidiToFile(clone), mapping);
    }

    public static string WriteMidiToFile(MidiFile midiFile, string namePrefix = "") {
        var appDir = Path.Combine(Path.GetTempPath(), "music-sidescroller");
        Directory.CreateDirectory(appDir);
        var fileName = $"{namePrefix}{DateTime.Now:yyyy_MM_dd-HH_mm_ss}-{Guid.NewGuid()}.midi";
        var filePath = Path.Combine(appDir, fileName);
        midiFile.Write(filePath);
        return filePath;
    }

    private InstrumentEventsStats BuildInstrumentChannelTrackStatistics() {
        // to keep the processing more separated for by tracks, we could use a rolling map of what instrument
        // belongs to each note, but the instrument change can also be on another track
        var allInstrumentChanges = MidiFile.GetTimedEvents()
            .Where(ev => ev.Event.EventType == MidiEventType.ProgramChange)
            .Select(ev => {
                var pce = (ProgramChangeEvent)ev.Event;
                return (Instrument: (GeneralMidiProgram)(byte)pce.ProgramNumber, pce.Channel, ev.Time);
            }).ToList();
        var numOfInstrumentEventsByChannel = new InstrumentEventsStats();
        foreach (var item in MidiFile.GetRealTracksIndexed()) {
            var eventsWithInstruments = item.Track.GetTimedEvents()
                .Where(ev => ev.Event.EventType == MidiEventType.NoteOn)
                .Select(@event => {
                    var noteEvent = (NoteOnEvent)@event.Event;
                    var noteInstrument = allInstrumentChanges
                        .Where(change => change.Channel == noteEvent.Channel && change.Time <= @event.Time)
                        .OrderBy(change => change.Time)
                        .FirstOrDefault().Instrument;
                    return (Instrument: noteInstrument, noteEvent.Channel);
                });
            numOfInstrumentEventsByChannel.AddRange(
                eventsWithInstruments.GroupBy(x => x).Select(group
                    => (item.Index, group.Key.Channel, group.Key.Instrument, Count: group.Count())).ToList());
        }

        return numOfInstrumentEventsByChannel;
    }

    public IEnumerable<NoteViewData> BuildMap() =>
        MidiFile.GetNotes().Select(VisualizeNote);

    public NoteViewData VisualizeNote(Note note) {
        var noteLength = note.LengthAs<MetricTimeSpan>(_tempoMap);
        var noteHeight = note.NoteNumber;
        var noteStart = note.TimeAs<MetricTimeSpan>(_tempoMap).TotalSeconds;

        return new NoteViewData {
            TrackDepth = GetTrackDepth(note),
            Note = note,
            Length = noteLength,
            Box = new RectangleF(
                (float)noteStart, noteHeight - 1,
                (float)noteLength.TotalSeconds, 1),
        };
    }
}
}