using System.Collections.Generic;
using UnityEngine;

namespace LyokhaVR
{
    public sealed class PatrolAgent : MonoBehaviour
    {
        public GameSession Session;
        public CourtyardNavigation Navigation;
        public Vector3[] PatrolPoints;
        public float WalkSpeed = 1.25f, ChaseSpeed = 3f, ViewDistance = 12;
        public bool IsChasing { get; private set; }
        public float Suspicion { get; private set; }
        public int ArrestsPerformed { get; private set; }
        public float DistanceTravelled { get; private set; }
        private Vector3 start, lastSeen;
        private List<Vector3> path = new List<Vector3>();
        private int patrolIndex, pathIndex;
        private float repathTimer, lostTimer, immunity = 20, riskyTimer;
        private bool alerted;
        private bool riskyDiscovered;

        private void Start()
        {
            start = transform.position;
            if (!Session) Session = FindAnyObjectByType<GameSession>();
            if (!Navigation) Navigation = FindAnyObjectByType<CourtyardNavigation>();
            if (Navigation) Navigation.Scan();
        }

        public void Alert(Vector3 position)
        {
            if (!Session || Session.Rules.State.IsFinished) return;
            lastSeen = position; alerted = true; immunity = 0; lostTimer = 0;
            IsChasing = true; repathTimer = 0;
            var audio = Session.GetComponent<GameAudio>();
            if (audio) audio.PlayWorldCue("police", transform.position);
        }

        private void Update()
        {
            if (!Session || !Session.IsActive || !Session.PlayerRoot || !Navigation) return;
            immunity -= Time.deltaTime;
            if (!Session.IsHiding) riskyDiscovered = false;
            var target = Session.PlayerRoot.position;
            var delta = target - transform.position; delta.y = 0;
            bool sees = immunity <= 0 && !Session.IsSafelyHidden && !Session.Rules.State.IsInsideContainer && delta.magnitude < ViewDistance;
            if (sees && !IsChasing && Vector3.Angle(transform.forward, delta) > 65) sees = false;
            if (sees)
            {
                var origin = transform.position + Vector3.up * 1.5f;
                var end = Session.ViewCamera.transform.position;
                // Only environment layer 0 blocks perception; the player root uses layer 2.
                sees = !Physics.Linecast(origin, end, 1, QueryTriggerInteraction.Ignore);
            }
            if (sees && Session.IsHiding)
            {
                riskyTimer -= Time.deltaTime;
                if (!riskyDiscovered && riskyTimer <= 0)
                { riskyTimer = 5; riskyDiscovered = Random.value < .35f; }
                sees = riskyDiscovered;
            }
            if (sees)
            {
                Suspicion += Time.deltaTime;
                lastSeen = target; lostTimer = 0;
                if (Suspicion > 1.3f && !IsChasing) Alert(target);
            }
            else { Suspicion = Mathf.Max(0, Suspicion - Time.deltaTime); lostTimer += Time.deltaTime; }
            if (IsChasing && lostTimer > 8)
            {
                IsChasing = alerted = false; path.Clear(); repathTimer = 0;
            }
            if (IsChasing && sees && delta.magnitude < 1.25f)
            {
                ArrestsPerformed++;
                Session.ArrestPlayer();
                ResetPatrol();
                return;
            }
            repathTimer -= Time.deltaTime;
            if (repathTimer <= 0)
            {
                var destination = IsChasing || alerted ? lastSeen : PatrolPoints != null && PatrolPoints.Length > 0 ? PatrolPoints[patrolIndex] : start;
                path = Navigation.FindPath(transform.position, destination);
                pathIndex = path.Count > 1 ? 1 : 0;
                repathTimer = IsChasing ? 1.2f : 4;
                if (!IsChasing && Vector3.Distance(transform.position, destination) < 1.5f && PatrolPoints.Length > 0)
                { patrolIndex = (patrolIndex + 1) % PatrolPoints.Length; repathTimer = 0; }
            }
            if (pathIndex >= path.Count) return;
            var before = transform.position;
            var next = path[pathIndex];
            transform.position = Vector3.MoveTowards(before, next, (IsChasing ? ChaseSpeed : WalkSpeed) * Time.deltaTime);
            var direction = next - before; direction.y = 0;
            if (direction.sqrMagnitude > .001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 9);
            DistanceTravelled += Vector3.Distance(before, transform.position);
            if (Vector3.Distance(transform.position, next) < .08f) pathIndex++;
        }

        public void ResetPatrol()
        {
            transform.position = start;
            IsChasing = alerted = false; Suspicion = lostTimer = 0; immunity = 20;
            riskyDiscovered = false; riskyTimer = 0;
            path.Clear(); repathTimer = 0;
        }
    }
}
