using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;

namespace Backend {
public static class DryWetMidiExtensions {
    // filter out tracks that won't map to any gameplay mechanism
    public static List<TrackChunk> GetRealTracks(this MidiFile midiFile) =>
        midiFile.GetTrackChunks()
            .Where(track => track.GetChannels().Any()).ToList();

    public static List<(TrackChunk Track, int Index)> GetRealTracksIndexed(this MidiFile midiFile) =>
        midiFile.GetRealTracks().Select((track, i) => (Track: track, Index: i)).ToList();

    public static double GetCurrentBpm(this Playback playback)
        => playback.TempoMap.GetTempoAtTime(playback.GetCurrentTime<MidiTimeSpan>()).BeatsPerMinute;
}
}