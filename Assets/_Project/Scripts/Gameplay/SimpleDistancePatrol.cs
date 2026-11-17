using System.Collections.Generic;
using UnityEngine;

namespace LyokhaVR
{
    public sealed class SimpleDistancePatrol : MonoBehaviour
    {
        public CourtyardSession Session;
        public CourtyardNavigation Navigation;
        public Vector3[] Waypoints;
        public float WalkSpeed=1.2f,ChaseSpeed=2.35f,DetectDistance=7,LoseDistance=13,CatchDistance=1.1f;
        public float GraceSeconds=12;
        public bool Chasing { get; private set; }
        public int Catches { get; private set; }
        public int Escapes { get; private set; }
        public float Travelled { get; private set; }
        List<Vector3> path=new List<Vector3>();int waypoint,pathIndex;
        float repath,lost,grace;
        Vector3 spawn;
        void Start(){spawn=transform.position;grace=GraceSeconds;Navigation.Scan();}
        void Update()
        {
            if(!Session||Session.Rules==null||!Session.PlayerRoot)return;
            grace-=Time.deltaTime;
            var delta=Session.PlayerRoot.position-transform.position;delta.y=0;
            if(!Chasing&&grace<=0&&delta.magnitude<DetectDistance)
            {
                var sees=!Physics.Linecast(transform.position+Vector3.up*1.45f,Session.View.transform.position,1,QueryTriggerInteraction.Ignore);
                if(sees){Chasing=true;repath=0;lost=0;Session.PoliceMessage="Патруль заметил тебя! Убегай; Shift — бег на компьютере.";}
            }
            if(Chasing)
            {
                lost=delta.magnitude>LoseDistance?lost+Time.deltaTime:0;
                if(lost>=2){Chasing=false;Escapes++;grace=8;repath=0;Session.PoliceMessage="Ты оторвался от патруля. Можно продолжать поиск.";}
                else if(delta.magnitude<CatchDistance)
                {
                    Catches++;Session.Arrest();Chasing=false;grace=20;transform.position=spawn;repath=0;path.Clear();return;
                }
            }
            repath-=Time.deltaTime;
            if(repath<=0)
            {
                var destination=Chasing?Session.PlayerRoot.position:Waypoints[waypoint];
                path=Navigation.FindPath(transform.position,destination);pathIndex=path.Count>1?1:0;repath=Chasing?.6f:2;
                if(!Chasing&&Vector3.Distance(transform.position,destination)<1.5f){waypoint=(waypoint+1)%Waypoints.Length;repath=0;}
            }
            if(pathIndex>=path.Count)return;
            var before=transform.position;var next=path[pathIndex];
            transform.position=Vector3.MoveTowards(before,next,(Chasing?ChaseSpeed:WalkSpeed)*Time.deltaTime);
            var direction=next-before;direction.y=0;
            if(direction.sqrMagnitude>.001f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(direction),Time.deltaTime*8);
            Travelled+=Vector3.Distance(before,transform.position);
            if(Vector3.Distance(transform.position,next)<.08f)pathIndex++;
        }
    }
}
