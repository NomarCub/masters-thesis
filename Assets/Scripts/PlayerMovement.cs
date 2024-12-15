using UnityEngine;

// from here: https://github.com/Brackeys/2D-Character-Controller
public class PlayerMovement : MonoBehaviour {
    [SerializeField] private GameController gameController;
    public CharacterController2D controller;
    public Animator animator;

    private float _horizontalMove = 0;
    public float runSpeed = 40;
    private bool _jump = false;
    private bool _crouch = false;

    private void Update() {
        _horizontalMove = Input.GetAxisRaw("Horizontal") * runSpeed;

        animator.SetFloat("Speed", Mathf.Abs(_horizontalMove));

        if (Input.GetButtonDown("Jump")) {
            _jump = true;
            animator.SetBool("IsJumping", true);
        }

        if (Input.GetButtonDown("Plane Hop Forward")) {
            gameController.TryPlaneHop(GameController.HopDirection.Forward);
        }

        if (Input.GetButtonDown("Plane Hop Backward")) {
            gameController.TryPlaneHop(GameController.HopDirection.Backward);
        }

        if (Input.GetButtonDown("Plane Hop Helper")) {
            gameController.TryPlaneHop(GameController.HopDirection.Helper);
        }

        if (Input.GetButtonDown("Crouch")) {
            _crouch = true;
        }
        else if (Input.GetButtonUp("Crouch")) {
            _crouch = false;
        }
    }

    public void OnLanding() => animator.SetBool("IsJumping", false);

    public void OnCrouching(bool isCrouching) => animator.SetBool("IsCrouching", isCrouching);

    private void FixedUpdate() {
        // Move our character
        controller.Move(_horizontalMove * Time.fixedDeltaTime, _crouch, _jump);
        _jump = false;
    }
}