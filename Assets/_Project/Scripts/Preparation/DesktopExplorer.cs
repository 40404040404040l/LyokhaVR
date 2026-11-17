using UnityEngine;
using UnityEngine.InputSystem;

namespace LyokhaVR.Preparation
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class DesktopExplorer : MonoBehaviour
    {
        [SerializeField] private Transform view;
        [SerializeField] private float walkSpeed = 2.5f;
        [SerializeField] private float runSpeed = 4.5f;
        [SerializeField] private float mouseSensitivity = 0.12f;
        private CharacterController controller;
        private Vector3 spawn;
        private Quaternion spawnRotation;
        private float pitch;
        private float verticalSpeed;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            spawn = transform.position;
            spawnRotation = transform.rotation;
            if (view == null) view = GetComponentInChildren<Camera>().transform;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (mouse.leftButton.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Move(Vector3.zero);
                return;
            }
            var look = mouse.delta.ReadValue() * mouseSensitivity;
            transform.Rotate(0f, look.x, 0f);
            pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
            view.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            var input = new Vector2(
                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
            input = Vector2.ClampMagnitude(input, 1f);
            var move = transform.right * input.x + transform.forward * input.y;
            var speed = keyboard.leftShiftKey.isPressed ? runSpeed : walkSpeed;
            Move(move * speed);

            if (keyboard.eKey.wasPressedThisFrame &&
                Physics.Raycast(view.position, view.forward, out var hit, 3f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var target = hit.collider.GetComponentInParent<PreparationTarget>();
                if (target != null) target.Interact();
            }

            if (keyboard.rKey.wasPressedThisFrame || transform.position.y < -30f) ResetPosition();
        }

        private void Move(Vector3 horizontalVelocity)
        {
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed += Physics.gravity.y * Time.deltaTime;
            controller.Move((horizontalVelocity + Vector3.up * verticalSpeed) * Time.deltaTime);
        }

        private void ResetPosition()
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(spawn, spawnRotation);
            controller.enabled = true;
            verticalSpeed = 0f;
            pitch = 0f;
            view.localRotation = Quaternion.identity;
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
