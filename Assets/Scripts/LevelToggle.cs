using System;
using System.IO;
using Backend;
using UnityEngine;
using UnityEngine.UI;

public class LevelToggle : MonoBehaviour {
    [SerializeField] private Toggle toggle;
    [SerializeField] private Text text;
    private LevelChooser _levelChooser;

    public enum Kind {
        BuiltIn,
        FileBased,
        Generated
    }

    private Kind _kind;
    private string _path;

    public bool IsOn {
        set {
            toggle.interactable = !value;
            if (toggle.isOn != value) {
                toggle.isOn = value;
            }
        }
    }

    private static int GetAndLogTime() {
        var time = (int)(DateTime.Now.Ticks & 0xFFFFFFFF);
        Debug.Log($"time requested: {time}");
        return time;
    }

    public Stream GetMidi() => _kind switch {
        Kind.BuiltIn => new MemoryStream(((TextAsset)Resources.Load(_path)).bytes),
        Kind.FileBased => new FileStream(_path, FileMode.Open, FileAccess.Read),
        Kind.Generated => ProceduralMusicGenerator.GenerateMusicStream(new() {
            Scale = FindObjectOfType<NoteInputHandler>().Scale,
            StarterMelody = FindObjectOfType<NoteInputHandler>().BuildNoteInputs(),
            Seed = GetAndLogTime()
        }),
        _ => throw new ArgumentOutOfRangeException()
    };

    private void Awake() =>
        toggle.onValueChanged.AddListener(value => {
            if (value) _levelChooser.TryChooseLevel(this);
        });

    public void Initialize(LevelChooser levelChooser, string path, Kind kind = Kind.BuiltIn) {
        _levelChooser = levelChooser;
        _path = path;
        _kind = kind;

        text.text = _kind switch {
            Kind.BuiltIn => Path.GetFileNameWithoutExtension(path),
            Kind.FileBased => "Custom file: " + Path.GetFileNameWithoutExtension(path),
            Kind.Generated => "Procedurally generated song",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }
}