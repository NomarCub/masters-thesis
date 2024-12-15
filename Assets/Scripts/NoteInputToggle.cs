using UnityEngine;
using UnityEngine.UI;
using MusicalNote = Melanchall.DryWetMidi.MusicTheory.Note;

public class NoteInputToggle : MonoBehaviour {
    [SerializeField] private Toggle toggle;
    private static readonly Color InactiveColor = Color.white;
    private static readonly Color ActiveColor = Color.black;

    public MusicalNote Note;
    public int timePoint;
    private NoteInputHandler _noteInputHandler;

    public bool IsOn {
        get => toggle.isOn;
        set => toggle.isOn = value;
    }

    private void Activate(bool changedTo) {
        var colors = toggle.colors;
        var color = changedTo ? ActiveColor : InactiveColor;
        colors.normalColor = new Color(color.r, color.g, color.b, 0.6f);
        colors.highlightedColor = new Color(color.r, color.g, color.b, 0.85f);
        colors.pressedColor = new Color(color.r, color.g, color.b, 0.97f);
        toggle.colors = colors;

        _noteInputHandler.OnNoteInputActivated(Note, changedTo);
    }

    public void Initialize(NoteInputHandler noteInputHandler, MusicalNote note, int timePoint) {
        _noteInputHandler = noteInputHandler;
        Note = note;
        this.timePoint = timePoint;

        toggle.onValueChanged.AddListener(Activate);
        toggle.isOn = false;
    }
}