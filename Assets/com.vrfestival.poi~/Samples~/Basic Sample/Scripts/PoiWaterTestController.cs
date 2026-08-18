using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Poi
{
    public sealed class PoiWaterTestController : MonoBehaviour
    {
        [SerializeField] private Rigidbody poiBody;
        [SerializeField] private PoiPaperDamageSystem damageSystem;
        [SerializeField] private Rigidbody rippleTestBody;
        [SerializeField] private float moveSpeed = 0.18f;
        [SerializeField] private float fastMultiplier = 5f;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private Vector3 initialTestBodyPosition;

        public Rigidbody PoiBody { set => poiBody = value; }
        public PoiPaperDamageSystem DamageSystem { set => damageSystem = value; }
        public Rigidbody RippleTestBody { set => rippleTestBody = value; }

        private void Awake()
        {
            if (poiBody == null) return;
            initialPosition = poiBody.position;
            initialRotation = poiBody.rotation;
            if (rippleTestBody != null) initialTestBodyPosition = rippleTestBody.position;
        }

        private void Update()
        {
            if (SampleInput.GetKeyDown(KeyCode.R)) ResetTest();
            if (SampleInput.GetKeyDown(KeyCode.C)) ResetRippleTestBody();
        }

        private void FixedUpdate()
        {
            if (poiBody == null) return;
            float horizontal = (SampleInput.GetKey(KeyCode.D) ? 1f : 0f) - (SampleInput.GetKey(KeyCode.A) ? 1f : 0f);
            float vertical = (SampleInput.GetKey(KeyCode.W) ? 1f : 0f) - (SampleInput.GetKey(KeyCode.S) ? 1f : 0f);
            Vector3 input = new Vector3(horizontal, SampleInput.GetKey(KeyCode.E) ? 1f : SampleInput.GetKey(KeyCode.Q) ? -1f : 0f, vertical);
            float speed = moveSpeed * ((SampleInput.GetKey(KeyCode.LeftShift) || SampleInput.GetKey(KeyCode.RightShift)) ? fastMultiplier : 1f);
            poiBody.MovePosition(poiBody.position + input.normalized * speed * Time.fixedDeltaTime);
            float rotation = 0f;
            if (SampleInput.GetKey(KeyCode.Z)) rotation += 1f;
            if (SampleInput.GetKey(KeyCode.X)) rotation -= 1f;
            if (!Mathf.Approximately(rotation, 0f)) poiBody.MoveRotation(Quaternion.AngleAxis(rotation * 150f * Time.fixedDeltaTime, Vector3.up) * poiBody.rotation);
        }

        private void ResetTest()
        {
            damageSystem.ResetPaper();
            poiBody.position = initialPosition;
            poiBody.rotation = initialRotation;
            poiBody.linearVelocity = Vector3.zero;
            poiBody.angularVelocity = Vector3.zero;
            ResetRippleTestBody();
        }

        private void ResetRippleTestBody()
        {
            if (rippleTestBody == null) return;
            rippleTestBody.position = initialTestBodyPosition;
            rippleTestBody.rotation = Quaternion.identity;
            rippleTestBody.linearVelocity = Vector3.zero;
            rippleTestBody.angularVelocity = Vector3.zero;
            rippleTestBody.WakeUp();
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(14, 14, 520, 132), "Poi Water / Wetness Test");
            GUI.Label(new Rect(28, 42, 490, 22), "WASD: horizontal move   Q / E: down / up");
            GUI.Label(new Rect(28, 64, 490, 22), "Shift: fast move (water load grows with speed squared)");
            GUI.Label(new Rect(28, 86, 490, 22), "Z / X: rotate in water   R: reset damage and wetness");
            GUI.Label(new Rect(28, 108, 490, 22), "C: drop cube again   R: reset all   Select Paper for wetness Gizmos");
        }

        /// <summary>
        /// Keeps the optional sample independent of the Input System package.
        /// Legacy input is compiled only when enabled; Input System-only projects
        /// use a cached reflection bridge to Keyboard.current.
        /// </summary>
        private static class SampleInput
        {
            private static readonly Dictionary<KeyCode, string> InputSystemKeyNames = new Dictionary<KeyCode, string>
            {
                { KeyCode.A, "aKey" }, { KeyCode.C, "cKey" }, { KeyCode.D, "dKey" },
                { KeyCode.E, "eKey" }, { KeyCode.Q, "qKey" }, { KeyCode.R, "rKey" },
                { KeyCode.S, "sKey" }, { KeyCode.W, "wKey" }, { KeyCode.X, "xKey" },
                { KeyCode.Z, "zKey" }, { KeyCode.LeftShift, "leftShiftKey" },
                { KeyCode.RightShift, "rightShiftKey" }
            };

            private static Type keyboardType;
            private static PropertyInfo currentKeyboardProperty;
            private static object cachedKeyboard;
            private static readonly Dictionary<KeyCode, object> Controls = new Dictionary<KeyCode, object>();
            private static readonly Dictionary<Type, PropertyInfo> PressedProperties = new Dictionary<Type, PropertyInfo>();
            private static readonly Dictionary<Type, PropertyInfo> PressedThisFrameProperties = new Dictionary<Type, PropertyInfo>();
            private static bool reflectionInitialized;

            public static bool GetKey(KeyCode key)
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKey(key);
#else
                return ReadInputSystemKey(key, false);
#endif
            }

            public static bool GetKeyDown(KeyCode key)
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKeyDown(key);
#else
                return ReadInputSystemKey(key, true);
#endif
            }

            private static bool ReadInputSystemKey(KeyCode key, bool pressedThisFrame)
            {
                EnsureReflection();
                object keyboard = currentKeyboardProperty?.GetValue(null);
                if (keyboard == null) return false;
                if (!ReferenceEquals(keyboard, cachedKeyboard))
                {
                    cachedKeyboard = keyboard;
                    Controls.Clear();
                }

                if (!Controls.TryGetValue(key, out object control))
                {
                    if (!InputSystemKeyNames.TryGetValue(key, out string propertyName)) return false;
                    control = keyboardType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)?.GetValue(keyboard);
                    if (control == null) return false;
                    Controls.Add(key, control);
                }

                Type controlType = control.GetType();
                Dictionary<Type, PropertyInfo> properties = pressedThisFrame ? PressedThisFrameProperties : PressedProperties;
                if (!properties.TryGetValue(controlType, out PropertyInfo property))
                {
                    property = controlType.GetProperty(pressedThisFrame ? "wasPressedThisFrame" : "isPressed", BindingFlags.Instance | BindingFlags.Public);
                    properties.Add(controlType, property);
                }
                return property != null && property.GetValue(control) is bool value && value;
            }

            private static void EnsureReflection()
            {
                if (reflectionInitialized) return;
                reflectionInitialized = true;
                keyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
                currentKeyboardProperty = keyboardType?.GetProperty("current", BindingFlags.Static | BindingFlags.Public);
            }
        }
    }
}
