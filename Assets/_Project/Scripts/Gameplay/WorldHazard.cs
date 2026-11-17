using UnityEngine;

namespace LyokhaVR
{
    public sealed class WorldHazard : MonoBehaviour
    {
        public HazardKind Kind;
        public bool Rat;
        public Vector3 Home;
        private GameSession session;
        private float cooldown;
        private void Start()
        {
            session = FindAnyObjectByType<GameSession>();
            Home = transform.position;
            var action = GetComponent<WorldAction>();
            if (action) action.Triggered += _ => { if (cooldown <= 0) Bite(); };
        }
        private void Update()
        {
            if (!session || !session.IsActive) return;
            cooldown -= Time.deltaTime;
            if (Rat)
            {
                transform.position = Home + new Vector3(Mathf.Sin(Time.time * 1.2f) * .7f, 0, Mathf.Cos(Time.time) * .35f);
                if (cooldown <= 0 && Vector3.Distance(session.PlayerRoot.position, transform.position) < 1.1f) Bite();
            }
        }
        private void Bite() { cooldown = 8; session.Hurt(Kind); }
        public void ResetHazard() { cooldown = 0; transform.position = Home; }
    }
}
