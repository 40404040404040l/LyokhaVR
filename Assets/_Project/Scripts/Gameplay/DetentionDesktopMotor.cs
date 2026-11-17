using UnityEngine;
using UnityEngine.InputSystem;

namespace LyokhaVR
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class DetentionDesktopMotor : MonoBehaviour
    {
        public DetentionRoom Room;
        public Camera View;
        public float Speed = 1.2f, Sensitivity = .11f;
        public bool ControlCaptured => Cursor.lockState == CursorLockMode.Locked;
        CharacterController body;
        float pitch, fallSpeed;
        void Awake() { body = GetComponent<CharacterController>(); }
        void Update()
        {
            var k = Keyboard.current; var mouse = Mouse.current;
            if (!Room || !View || k == null || mouse == null || GameSceneFlow.IsTransitioning) return;
            if (k.escapeKey.wasPressedThisFrame && !Room.IsMainMenu) Room.SetMenu(!Room.MenuOpen);
            if (k.rKey.wasPressedThisFrame && !Room.IsMainMenu) { Room.Restart(); return; }
            if (!Room.CanWalk) return;
            if (!ControlCaptured)
            {
                if (k.enterKey.wasPressedThisFrame || mouse.leftButton.wasPressedThisFrame)
                { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
                return;
            }
            var look = mouse.delta.ReadValue() * Sensitivity;
            transform.Rotate(0, look.x, 0); pitch = Mathf.Clamp(pitch - look.y, -80, 80);
            View.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            var input = Vector2.ClampMagnitude(new Vector2((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0), (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0)), 1);
            var movement = (transform.right * input.x + transform.forward * input.y) * Speed;
            if (body.isGrounded && fallSpeed < 0) fallSpeed = -2;
            fallSpeed += Physics.gravity.y * Time.deltaTime;
            body.Move((movement + Vector3.up * fallSpeed) * Time.deltaTime);
        }
        void OnApplicationFocus(bool focused) { if (!focused) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } }
    }
}
