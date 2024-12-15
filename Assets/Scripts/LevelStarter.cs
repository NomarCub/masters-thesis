using UnityEngine;

public class LevelStarter : MonoBehaviour {
    [SerializeField] private GameController gameController;
    [SerializeField] private LevelChooser levelChooser;

    private void OnTriggerEnter2D(Collider2D other) {
        gameController.StartLevel(levelChooser.CurrentLevel);
    }
}