using UnityEngine;

public class LevelCursor : MonoBehaviour {
    private Vector3 _startPos;
    private float _musicSpeed = 0;
    [SerializeField] private new Rigidbody2D rigidbody;

    private void Awake() {
        _startPos = transform.localPosition;
    }

    public void SetMusicSpeed(float speed) {
        _musicSpeed = speed;
    }

    public void Reset() {
        _musicSpeed = 0;
        transform.localPosition = _startPos;
    }

    private void LateUpdate() {
        rigidbody.velocity = Vector2.right * _musicSpeed;
    }
}