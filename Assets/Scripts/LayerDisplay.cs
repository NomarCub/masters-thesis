using UnityEngine;

public class LayerDisplay : MonoBehaviour {
    public int trackDepth;
    [SerializeField] private RectTransform rectTransform;

    public void SetCurrentTrack(int currentTrackDepth) {
        rectTransform.sizeDelta = currentTrackDepth == trackDepth
            ? new Vector2(rectTransform.sizeDelta.x, rectTransform.sizeDelta.x * 1.8f)
            : new Vector2(rectTransform.sizeDelta.x, rectTransform.sizeDelta.x);
    }
}