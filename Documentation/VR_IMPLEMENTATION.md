# VR implementation

## Scene and controls

`AgentScripts/BuildGameXR.cs` creates `Assets/_Project/Scenes/LyokhaGameVR.unity` from the current desktop game scene. It retains the game state and world, disables the desktop player, and instantiates the official XR Interaction Toolkit 3.7 Starter Assets rig at the same spawn and yaw. The rig has continuous joystick movement, snap turn, near/far controller interaction, collision and gravity. Nominal device-mode camera height is 1.65 m; a physical floor-tracked headset supplies its own tracked height.

World actions use `WorldAction` in both modes:

- Static object: point at it and press grip to interact, within 3.3 m of its collider surface.
- Loose loot: grip to pick up and inspect; keep holding grip and press trigger to put it into inventory. Release grip to leave it in the world.
- Menu/inventory: aim a controller ray and press trigger. The HUD uses world-space uGUI and `TrackedDeviceGraphicRaycaster` through one `XRUIInputModule`; it exposes inventory, use, drop, decisions, pause and restart without requiring a keyboard.

`VRIntegration.Register(action, true)` registers newly spawned loose loot. The rig also registers preexisting world actions in Awake. `VRWorldAction` routes static selection and held-item activation separately, checks availability and reach, and debounces repeated callbacks. UI `Button` objects stay on the UI input path to avoid firing the same command through both UI and physics.

## Editor simulator

The official XR Interaction Simulator prefab is tagged `EditorOnly`. It and its on-screen help do not appear in a player build. `VRSimulatorAvailability` additionally prevents simulated input when Unity reports an active physical headset in the Editor.

The exact imported sample bindings are:

- WASD / Q,E: simulated body/device translation; arrow keys or mouse: rotate.
- Tab: cycle targeted devices; H: target head; `[` / `]`: toggle left/right controller.
- G: grip; T: trigger for the currently targeted controller.
- I/J/K/L: controller thumbstick; Shift: left-device modifier.
- X / Y: simulator action/input-selection menus. The simulator UI shows the current targeting mode and bindings.

For gameplay locomotion tests, use simulated controller thumbstick input. Simulator body translation alone does not prove the game's collision-based locomotion provider works.

## Automated simulation method

`XRSimulationProbe` exists only under `UNITY_EDITOR && ENABLE_VR`. Add it at runtime after the simulator's three devices exist; do not save it into any scene. It disables only `XRInteractionSimulator`, retaining `SimulatedDeviceLifecycleManager` and the official `XRSimulatedHMD` / `XRSimulatedController` devices. Removing the probe restores the simulator.

The first diagnostic used `InputState.Change` in Update, which delivered poses and held values but did not reliably deliver performed/completed action edges. The revised probe disables the simulator component, retains its device lifecycle manager, and queues official state events from `InputSystem.onBeforeUpdate` only for Dynamic updates. Temporary IgnoreFocus/AllDeviceInputAlwaysGoesToGameView settings are restored on disable. No gameplay `Activate`, UI click callback, or interaction-manager `SelectEnter` is called by this driver.

Set `LeftMove` to test the actual continuous-move provider; set tracking-space controller position/rotation plus `RightGrip` / `RightTrigger` to test the actual selection and activation input paths. For a world point, compute tracking-space position with `XROrigin.CameraFloorOffsetObject.transform.InverseTransformPoint(worldPoint)`. Record advancing frames, physical controller poses, selection lists, action effects and movement distance. For a held object, record its transform changing while the simulated hand moves and confirm that grip alone did not stash it; trigger must perform the stash once.

## Hardware boundary

Headset model and target VR platform are not supplied. The project includes XR Management and OpenXR, but no platform-specific real-device loader or interaction profiles can be claimed tested. macOS desktop testing and the Editor simulator do not prove real headset tracking, stereo rendering, controller compatibility, comfort or headset performance. The standalone macOS deliverable runs desktop mode; physical VR requires configuring a supported target/runtime once the headset is known.

## Verification status

Both gameplay scenes compile. VR frames and the three official simulated devices are confirmed. The authored preview offset is explicitly set to 1.65 m because an Unknown tracking origin without a native XR subsystem did not apply CameraYOffset itself.

Live diagnostics confirmed Start through XRUIInputModule, thumbstick locomotion, physical grip selection/movement and trigger stashing. Diagnostics found and fixed a production problem: Starter Assets ray/near masks omitted physical layer 6 (Finds). VRIntegration now includes this layer for both hands. A fresh `XRPlaytestRunner` run is required to confirm the persisted fix and remaining UI actions; its actual report is `Verification/xr-controller-test.json`. Intermediate live diagnostics in Logs are not final passing evidence.
