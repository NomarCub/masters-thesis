using System;
using System.Collections.Generic;
using System.Linq;
using Backend;
using UnityEngine;

public class NotePlatform : MonoBehaviour {
    public static readonly List<Color> AllColors = new List<Color32> {
        new(253, 077, 000, 255),
        new(213, 242, 000, 255),
        new(121, 074, 255, 255),
        new(219, 000, 114, 255),
        new(093, 255, 162, 255),
        new(103, 118, 000, 255),
        new(000, 148, 180, 255),
        new(210, 000, 143, 255),
        new(255, 224, 178, 255),
        new(000, 160, 090, 255),
    }.Select(i => (Color)i).ToList();

    private static readonly Color HelperColor = new Color32(255, 255, 255, 75);
    private static readonly Color HelperColorActive = new Color32(255, 255, 255, 155);

    [SerializeField] private new BoxCollider2D collider;
    [SerializeField] private SpriteRenderer spriteRenderer;
    
    // depth of logical / physical music track based layer, which is handled by GameController
    private int _depth;
    private bool _wasTouched = false;
    private GameController _gameController;
    public float startTime;
    private bool IsHelper => _depth == GameController.HelperDepth;

    public NoteViewData Data { get; private set; }

    private void Awake() {
        collider.size = spriteRenderer.size;
    }

    public void SetCurrentDepth(int currentDepth, bool isCurrentHelper) {
        var onCurrentDepth = isCurrentHelper && IsHelper || (!isCurrentHelper && currentDepth == _depth);
        var onCurrentButOnHelper = isCurrentHelper && currentDepth == _depth;
        collider.enabled = onCurrentDepth;

        // atmosphere is -10
        if (onCurrentDepth) {
            spriteRenderer.sortingOrder = 0;
        }
        else if (IsHelper || onCurrentButOnHelper) {
            spriteRenderer.sortingOrder = -15;
        }
        else {
            spriteRenderer.sortingOrder = -20;
        }

        if (IsHelper) {
            spriteRenderer.color = onCurrentDepth ? HelperColorActive : HelperColor;
        }
    }

    public void Initialize(Transform parent, Vector2 position, Vector2 scale, int depth, NoteViewData data,
        int currentDepth, bool isCurrentHelper, GameController gameController) {
        transform.parent = parent;
        transform.localPosition = position;
        spriteRenderer.size = scale;
        collider.size = scale;
        spriteRenderer.color = AllColors[Math.Abs(depth)];
        Data = data;
        _depth = depth;
        _gameController = gameController;
        startTime = Time.time;
        SetCurrentDepth(currentDepth, isCurrentHelper);
    }

    private void OnCollisionEnter2D(Collision2D collision) {
        // touching the platform only counts once, and only from above
        if (_wasTouched || collision.transform.position.y < transform.position.y) return;

        _wasTouched = true;
        _gameController?.OnNoteSteppedOn(this);
    }
}