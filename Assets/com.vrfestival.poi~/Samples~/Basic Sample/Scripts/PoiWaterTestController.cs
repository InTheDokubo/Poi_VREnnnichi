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
            if (Input.GetKeyDown(KeyCode.R)) ResetTest();
            if (Input.GetKeyDown(KeyCode.C)) ResetRippleTestBody();
        }

        private void FixedUpdate()
        {
            if (poiBody == null) return;
            Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), Input.GetKey(KeyCode.E) ? 1f : Input.GetKey(KeyCode.Q) ? -1f : 0f, Input.GetAxisRaw("Vertical"));
            float speed = moveSpeed * ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? fastMultiplier : 1f);
            poiBody.MovePosition(poiBody.position + input.normalized * speed * Time.fixedDeltaTime);
            float rotation = 0f;
            if (Input.GetKey(KeyCode.Z)) rotation += 1f;
            if (Input.GetKey(KeyCode.X)) rotation -= 1f;
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
    }
}
