using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Backend {
public static class CliOptions {
    private const string GETMusicFolderPathDevFallback = "../muzic/getmusic";
    private const string GETMusicPretrainedModelPathDevFallback = "../checkpoint.pth";

    private const string CustomMidiPathDevFallback = "Assets/Resources/songs/05. János, legyen.mid.bytes";

    public static string GETMusicFolderPath
        => GetPathIfExists("--getmusic-folder", GETMusicFolderPathDevFallback);

    public static string GETMusicPretrainedModelPath
        => GetPathIfExists("--getmusic-checkpoint", GETMusicPretrainedModelPathDevFallback);

    public static string CustomMidiFilePath
        => GetPathIfExists("--custom-song", CustomMidiPathDevFallback);

    private static string GetPathIfExists(string args, string fallback) {
        var cliOverride = GetArg(args);
        if (File.Exists(cliOverride) || File.Exists(cliOverride))
            return Path.GetFullPath(cliOverride);
        if (File.Exists(fallback) || Directory.Exists(fallback))
            return Path.GetFullPath(fallback);
        return null;
    }

    private static readonly List<string> CliArgs = Environment.GetCommandLineArgs().ToList();

    private static string GetArg(string flag) {
        var pathArgIdx = CliArgs.IndexOf(flag);
        return pathArgIdx > 0 ? CliArgs[pathArgIdx + 1] : null;
    }
}
}