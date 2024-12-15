using UnityEngine;

public class LevelBorder : MonoBehaviour {
    [SerializeField] private GameController gameController;

    private void OnTriggerEnter2D(Collider2D other) {
        if (!other.CompareTag("Player")) return;
        gameController.PlayerAtBorder(other.transform.position - transform.position);
    }
}