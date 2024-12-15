using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

// from here: https://github.com/Brackeys/2D-Character-Controller
public class CharacterController2D : MonoBehaviour {
    [SerializeField] private float m_JumpForce = 400f; // Amount of force added when the player jumps.

    [Range(0, 1)] [SerializeField]
    private float m_CrouchSpeed = .36f; // Amount of maxSpeed applied to crouching movement. 1 = 100%

    [Range(0, .3f)] [SerializeField] private float m_MovementSmoothing = .05f; // How much to smooth out the movement
    [SerializeField] private bool m_AirControl = false; // Whether or not a player can steer while jumping;
    [SerializeField] private LayerMask m_WhatIsGround; // A mask determining what is ground to the character
    [SerializeField] private Transform m_GroundCheck; // A position marking where to check if the player is grounded.
    [SerializeField] private Transform m_CeilingCheck; // A position marking where to check for ceilings
    [SerializeField] private Collider2D m_CrouchDisableCollider; // A collider that will be disabled when crouching

    [SerializeField] private new Rigidbody2D rigidbody;
    private const float GroundedRadius = .2f; // Radius of the overlap circle to determine if grounded
    private bool _grounded; // Whether or not the player is grounded.
    private const float CeilingRadius = .2f; // Radius of the overlap circle to determine if the player can stand up
    private bool _facingRight = true; // For determining which way the player is currently facing.
    private Vector3 _velocity = Vector3.zero;

    [Header("Events")] [Space] public UnityEvent OnLandEvent;

    [Serializable]
    public class BoolEvent : UnityEvent<bool> {
    }

    public BoolEvent OnCrouchEvent;
    private bool _wasCrouching = false;

    private void Awake() {
        OnLandEvent ??= new UnityEvent();
        OnCrouchEvent ??= new BoolEvent();
    }

    private void FixedUpdate() {
        var wasGrounded = _grounded;
        _grounded = false;

        // The player is grounded if a circlecast to the groundcheck position hits anything designated as ground
        // This can be done using layers instead but Sample Assets will not overwrite your project settings.
        var colliders = Physics2D.OverlapCircleAll(m_GroundCheck.position, GroundedRadius, m_WhatIsGround);
        if (colliders.Any(c => c.gameObject != gameObject)) {
            _grounded = true;
            if (!wasGrounded)
                OnLandEvent.Invoke();
        }
    }

    public void Move(float move, bool crouch, bool jump) {
        // If crouching, check to see if the character can stand up
        if (_wasCrouching && !crouch) {
            // If the character has a ceiling preventing them from standing up, keep them crouching
            if (Physics2D.OverlapCircle(m_CeilingCheck.position, CeilingRadius, m_WhatIsGround)) {
                crouch = true;
            }
        }

        // only control the player if grounded or airControl is turned on
        if (_grounded || m_AirControl) {
            // If crouching
            if (crouch) {
                if (!_wasCrouching) {
                    _wasCrouching = true;
                    OnCrouchEvent.Invoke(true);
                }

                // Reduce the speed by the crouchSpeed multiplier
                move *= m_CrouchSpeed;

                // Disable one of the colliders when crouching
                if (m_CrouchDisableCollider != null)
                    m_CrouchDisableCollider.enabled = false;
            }
            else {
                // Enable the collider when not crouching
                if (m_CrouchDisableCollider != null)
                    m_CrouchDisableCollider.enabled = true;

                if (_wasCrouching) {
                    _wasCrouching = false;
                    OnCrouchEvent.Invoke(false);
                }
            }

            // Move the character by finding the target velocity
            Vector3 targetVelocity = new Vector2(move * 10f, rigidbody.velocity.y);
            // And then smoothing it out and applying it to the character
            rigidbody.velocity = Vector3.SmoothDamp(rigidbody.velocity, targetVelocity, ref _velocity,
                m_MovementSmoothing);

            switch (move) {
                // If the input is moving the player right and the player is facing left...
                case > 0 when !_facingRight:
                // Otherwise if the input is moving the player left and the player is facing right...
                // ... flip the player.
                case < 0 when _facingRight:
                    // ... flip the player.
                    Flip();
                    break;
            }
        }

        // If the player should jump...
        if (_grounded && jump) {
            // Add a vertical force to the player.
            rigidbody.AddForce(new Vector2(0f, m_JumpForce), ForceMode2D.Impulse);
        }
    }


    // Switch the way the player is labelled as facing.
    private void Flip() {
        _facingRight = !_facingRight;
        transform.Rotate(0f, 180f, 0f);
    }
}