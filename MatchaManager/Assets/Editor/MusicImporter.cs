using UnityEditor;
using UnityEngine;

// Music in Assets/Audio/Music streams from disk as compressed Vorbis instead of being
// decoded into memory like the short sound effects.
public class MusicImporter : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(MatchaSceneBuilder.MusicDir + "/")) return;
        var importer = (AudioImporter)assetImporter;
        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.Streaming;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = 0.6f;
        settings.preloadAudioData = false;
        importer.defaultSampleSettings = settings;
    }
}
