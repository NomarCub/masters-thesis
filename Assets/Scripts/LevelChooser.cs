using System.Collections.Generic;
using System.IO;
using System.Linq;
using Backend;
using UnityEngine;
using UnityEngine.UI;

public class LevelChooser : MonoBehaviour {
    [SerializeField] private LevelToggle levelTogglePrefab;

    [SerializeField] private LayoutGroup layoutGroup;

    private readonly List<LevelToggle> _levelToggles = new();
    private LevelToggle _currentLevelToggle;

    public Stream CurrentLevel
        => _currentLevelToggle.GetMidi();

    private void Awake() {
        if (CliOptions.CustomMidiFilePath is not null) {
            var customLevelToggle = Instantiate(levelTogglePrefab, layoutGroup.transform);
            customLevelToggle.Initialize(this, CliOptions.CustomMidiFilePath, LevelToggle.Kind.FileBased);
            _levelToggles.Add(customLevelToggle);
        }

        _levelToggles.AddRange(Resources.LoadAll("songs", typeof(TextAsset))
            .Select(obj => {
                var levelToggle = Instantiate(levelTogglePrefab, layoutGroup.transform);
                levelToggle.Initialize(this, Path.Combine("songs", obj.name));
                return levelToggle;
            }));

        var generatedLevelToggle = Instantiate(levelTogglePrefab, layoutGroup.transform);
        generatedLevelToggle.Initialize(this, CliOptions.CustomMidiFilePath, LevelToggle.Kind.Generated);
        _levelToggles.Add(generatedLevelToggle);

        TryChooseLevel(_levelToggles.First());
    }

    public void TryChooseLevel(LevelToggle levelToggle) {
        if (_currentLevelToggle == levelToggle) return;

        _currentLevelToggle = levelToggle;

        _levelToggles.ForEach(toggle => toggle.IsOn = false);
        _currentLevelToggle.IsOn = true;
    }
}