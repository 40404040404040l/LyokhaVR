using UnityEngine;
using UnityEngine.InputSystem;

namespace LyokhaVR
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class DesktopMotor : MonoBehaviour
    {
        public Camera View;
        public float WalkSpeed = 2.7f;
        public float RunSpeed = 4.1f;
        public float Sensitivity = .11f;
        public GameSession Session;
        public PhysicalRummageDemo Rummage;
        public PhysicalRummageItem AimedRummageItem { get; private set; }
        public WorldAction AimedAction { get; private set; }
        public Rigidbody CarriedBody { get; private set; }
        private CharacterController body;
        private float pitch, verticalSpeed, carryDistance = 1.2f;
        private bool carriedGravity;
        private Vector3 eyeRestLocalPosition;
        public bool ControlCaptured => Cursor.lockState == CursorLockMode.Locked;
        public bool Leaning { get; private set; }

        private void Awake()
        {
            body = GetComponent<CharacterController>();
            if (!View) View = GetComponentInChildren<Camera>();
            if (View) eyeRestLocalPosition = View.transform.localPosition;
            if (!Session) Session = FindAnyObjectByType<GameSession>();
        }

        private void Update()
        {
            var k = Keyboard.current;
            var m = Mouse.current;
            if (Rummage) { UpdateRummage(k, m); return; }
            if (!Session || k == null || m == null) return;
            if (k.escapeKey.wasPressedThisFrame) Session.HandleCommand("pause");
            if (k.enterKey.wasPressedThisFrame) Session.HandleCommand(Session.Started ? "resume" : "start");
            if (k.hKey.wasPressedThisFrame) Session.HandleCommand("help");
            if (k.tabKey.wasPressedThisFrame || k.iKey.wasPressedThisFrame) Session.HandleCommand("inventory");
            if (k.fKey.wasPressedThisFrame) Session.HandleCommand("flashlight");
            if (k.qKey.wasPressedThisFrame) Session.HandleCommand("use");
            if (k.gKey.wasPressedThisFrame) Session.HandleCommand("drop");
            if (k.rKey.wasPressedThisFrame && Session.CanRestart) Session.HandleCommand("restart");
            var digits = new[] { k.digit1Key, k.digit2Key, k.digit3Key, k.digit4Key, k.digit5Key, k.digit6Key };
            for (var i = 0; i < digits.Length; i++)
                if (digits[i].wasPressedThisFrame) Session.HandleCommand("select:" + i);
            if (Session.HasDecision)
            {
                if (k.yKey.wasPressedThisFrame) Session.HandleCommand("decision:0");
                if (k.nKey.wasPressedThisFrame) Session.HandleCommand("decision:1");
            }
            var leavingHide = Session.IsHiding && k.eKey.wasPressedThisFrame;
            if (leavingHide) Session.HandleCommand("leavehide");

            var locked = Cursor.lockState == CursorLockMode.Locked && Session.IsMovementAllowed;
            if (locked)
            {
                var look = m.delta.ReadValue() * Sensitivity;
                if (CarriedBody && m.rightButton.isPressed)
                    CarriedBody.transform.Rotate(View.transform.up, look.x * 2f, Space.World);
                else
                {
                    transform.Rotate(0, look.x, 0);
                    pitch = Mathf.Clamp(pitch - look.y, -80, 80);
                    View.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
                }
                var direction = new Vector2((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0), (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0));
                direction = Vector2.ClampMagnitude(direction, 1);
                Move((transform.right * direction.x + transform.forward * direction.y) * (k.leftShiftKey.isPressed ? RunSpeed : WalkSpeed));
                Aim();
                if (m.leftButton.wasPressedThisFrame) ToggleCarry();
                if (k.eKey.wasPressedThisFrame && !leavingHide)
                {
                    var action = CarriedBody ? CarriedBody.GetComponent<WorldAction>() : AimedAction;
                    if (action) action.Activate();
                }
                if (CarriedBody)
                    carryDistance = Mathf.Clamp(carryDistance + m.scroll.ReadValue().y * .001f, .7f, 1.8f);
            }
            else { Move(Vector3.zero); AimedAction = null; }
            if (!Session.IsMovementAllowed && CarriedBody) ReleaseCarry();
            if (transform.position.y < -15) Session.RecoverPlayer();
        }

        private void FixedUpdate()
        {
            if (!CarriedBody || !View) return;
            var target = View.transform.position + View.transform.forward * carryDistance;
            CarriedBody.linearVelocity = Vector3.ClampMagnitude((target - CarriedBody.position) * 12f, 7f);
            CarriedBody.angularVelocity = Vector3.zero;
        }

        private void Aim()
        {
            AimedAction = null;
            var hits = Physics.RaycastAll(View.transform.position, View.transform.forward, 3.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (CarriedBody && hit.collider.attachedRigidbody == CarriedBody) continue;
                var action = hit.collider.GetComponentInParent<WorldAction>();
                if (action && action.Available) AimedAction = action;
                break;
            }
            Session.SetAim(AimedAction, CarriedBody != null);
        }

        private void ToggleCarry()
        {
            if (CarriedBody) { ReleaseCarry(); return; }
            if (!AimedAction) return;
            var rb = AimedAction.GetComponent<Rigidbody>();
            if (!rb || rb.isKinematic) return;
            CarriedBody = rb;
            carriedGravity = rb.useGravity;
            rb.useGravity = false;
            carryDistance = Mathf.Clamp(Vector3.Distance(rb.position, View.transform.position), .7f, 1.8f);
        }

        public void ReleaseCarry()
        {
            if (CarriedBody) { CarriedBody.useGravity = carriedGravity; if (!Rummage) CarriedBody.linearVelocity = Vector3.zero; }
            CarriedBody = null;
        }

        // Same rigidbodies as VR; no timed search or random reward in this mode.
        private void UpdateRummage(Keyboard k, Mouse m)
        {
            if (k == null || m == null) return;
            if (Rummage.Session)
            {
                if(Rummage.Session.Rules.State.IsFinished)
                {
                    ReleaseCarry(); AimedRummageItem = null;
                    if(k.rKey.wasPressedThisFrame)Rummage.Session.RestartGame();
                    return;
                }
                if(k.iKey.wasPressedThisFrame||k.tabKey.wasPressedThisFrame)Rummage.Session.ToggleMenu();
                if(k.fKey.wasPressedThisFrame)Rummage.Session.ToggleFlashlight();
                if(k.bKey.wasPressedThisFrame)Rummage.Session.OpenShop();
                if(Rummage.Session.MenuOpen)
                {
                    if(k.escapeKey.wasPressedThisFrame)Rummage.Session.SetMenu(false);
                    Move(Vector3.zero);return;
                }
            }
            if (k.escapeKey.wasPressedThisFrame) { ReleaseControl(); Move(Vector3.zero); return; }
            if (!ControlCaptured)
            {
                Move(Vector3.zero);
                if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || m.leftButton.wasPressedThisFrame)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                // A click which captures the cursor must not also pick up or release junk.
                return;
            }
            var look = m.delta.ReadValue() * Sensitivity;
            if (CarriedBody && m.rightButton.isPressed)
                CarriedBody.transform.Rotate(View.transform.up, look.x * 2, Space.World);
            else
            {
                transform.Rotate(0, look.x, 0);
                pitch = Mathf.Clamp(pitch - look.y, -80, 80);
                View.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            }
            var direction = Vector2.ClampMagnitude(new Vector2((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0),
                (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0)), 1);
            Move((transform.right * direction.x + transform.forward * direction.y) * (k.leftShiftKey.isPressed ? RunSpeed : WalkSpeed));
            SetLean(k.qKey.isPressed);
            AimedRummageItem = null;
            if (Physics.Raycast(View.transform.position, View.transform.forward, out var hit, 2.5f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                AimedRummageItem = hit.collider.GetComponentInParent<PhysicalRummageItem>();
            if (m.leftButton.wasPressedThisFrame)
            {
                if (CarriedBody) ReleaseCarry();
                else if (AimedRummageItem && (!AimedRummageItem.Valuable || Rummage.IsExposed(AimedRummageItem)))
                {
                    CarriedBody = AimedRummageItem.Body;
                    carriedGravity = CarriedBody.useGravity;
                    CarriedBody.useGravity = false;
                    carryDistance = Mathf.Clamp(Vector3.Distance(CarriedBody.position, View.transform.position), .7f, 1.8f);
                }
            }
            if (k.eKey.wasPressedThisFrame && CarriedBody) Rummage.TryStore(CarriedBody.GetComponent<PhysicalRummageItem>());
            if (CarriedBody) carryDistance = Mathf.Clamp(carryDistance + m.scroll.ReadValue().y * .001f, .7f, 1.8f);
        }

        private void SetLean(bool lean)
        {
            if (!View) return;
            var parent = View.transform.parent;
            var normal = parent.TransformPoint(eyeRestLocalPosition);
            var desired = parent.TransformPoint(eyeRestLocalPosition + (lean ? new Vector3(0, .22f, .35f) : Vector3.zero));
            var offset = desired - normal;
            if (offset.sqrMagnitude > .001f && Physics.SphereCast(normal, .06f, offset.normalized, out var hit, offset.magnitude, ~(1 << 2), QueryTriggerInteraction.Ignore))
                desired = normal + offset.normalized * Mathf.Max(0, hit.distance - .015f);
            View.transform.position = desired;
            Leaning = lean;
        }

        private void ReleaseControl()
        {
            ReleaseCarry(); SetLean(false);
            AimedRummageItem = null;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        private void OnApplicationFocus(bool focused) { if (!focused && Rummage) ReleaseControl(); }

        private void Move(Vector3 velocity)
        {
            if (!body.enabled) return;
            if (body.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            verticalSpeed += Physics.gravity.y * Time.deltaTime;
            body.Move((velocity + Vector3.up * verticalSpeed) * Time.deltaTime);
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            ReleaseCarry();
            body.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            body.enabled = true;
            pitch = 0;
            verticalSpeed = 0;
            if (View) View.transform.localRotation = Quaternion.identity;
        }

        private void OnDisable() { ReleaseCarry(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var item = hit.collider.attachedRigidbody;
            if (item && !item.isKinematic && item.gameObject.layer == 6 && item != CarriedBody)
                item.AddForce(new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z) * 1.2f, ForceMode.VelocityChange);
        }
    }
}
