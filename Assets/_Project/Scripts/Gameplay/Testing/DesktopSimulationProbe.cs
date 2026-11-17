#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace LyokhaVR.Testing
{
    // Test-only: drives the real Input System state before DesktopMotor.Update.
    [DefaultExecutionOrder(-29990)]
    public sealed class DesktopSimulationProbe : MonoBehaviour
    {
        public Key[] Keys = new Key[0];
        public Vector2 Look;
        public bool LeftButton, RightButton;
        public bool QueuedEvents, OverrideMousePosition;
        public Vector2 MousePosition;
        private InputSettings.BackgroundBehavior previousBackground;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
        private void OnEnable()
        {
            previousBackground = InputSystem.settings.backgroundBehavior;
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.onBeforeUpdate += QueueInput;
        }
        private void QueueInput()
        {
            if(!QueuedEvents||InputState.currentUpdateType!=InputUpdateType.Dynamic)return;
            if(Keyboard.current!=null)InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Keys));
            if(Mouse.current!=null)InputSystem.QueueStateEvent(Mouse.current,MouseState());
        }
        private MouseState MouseState()
        {
            var state=new MouseState{delta=Look,position=OverrideMousePosition?MousePosition:Mouse.current.position.ReadValue()};
            return state.WithButton(MouseButton.Left,LeftButton).WithButton(MouseButton.Right,RightButton);
        }
        private void Update()
        {
            if(QueuedEvents)return;
            if (Keyboard.current != null) InputState.Change(Keyboard.current, new KeyboardState(Keys));
            if (Mouse.current != null)
            {
                InputState.Change(Mouse.current, MouseState());
            }
        }
        private void OnDisable()
        {
            InputSystem.onBeforeUpdate -= QueueInput;
            if (Keyboard.current != null) InputState.Change(Keyboard.current, new KeyboardState());
            if (Mouse.current != null) InputState.Change(Mouse.current, new MouseState());
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
        }
    }
}
#endif
