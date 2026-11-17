using System;
using System.Collections.Generic;
using UnityEngine;

namespace LyokhaVR
{
    /// <summary>Original synthesized yard ambience and deliberately distinct event signatures.</summary>
    public sealed class GameAudio : MonoBehaviour
    {
        const int Rate = 22050;
        readonly Dictionary<string,AudioClip> clips = new Dictionary<string,AudioClip>(StringComparer.OrdinalIgnoreCase);
        AudioSource effects, ambience;
        AudioClip dayLoop, nightLoop;
        bool isNight, initialized;
        public string LastCue { get; private set; }

        void Awake() { EnsureReady(); }

        public void PlayCue(string cue)
        {
            EnsureReady();
            string key=Normalize(cue);
            if (!clips.TryGetValue(key,out var clip)) clip=clips["collect"];
            LastCue=key;
            effects.PlayOneShot(clip, key=="hurt" ? .65f : .5f);
        }

        public void PlayWorldCue(string cue, Vector3 position)
        {
            EnsureReady();
            string key=Normalize(cue);
            if (!clips.TryGetValue(key,out var clip)) clip=clips["collect"];
            LastCue=key;
            var sourceObject=new GameObject("Yard sound: "+key);
            sourceObject.transform.position=position;
            var source=sourceObject.AddComponent<AudioSource>();
            source.clip=clip;source.spatialBlend=1f;source.rolloffMode=AudioRolloffMode.Linear;
            source.minDistance=7f;source.maxDistance=70f;source.volume=.8f;source.dopplerLevel=0f;
            source.Play();Destroy(sourceObject,clip.length+.3f);
        }

        public void SetNight(bool night)
        {
            EnsureReady();
            if(isNight==night && ambience.isPlaying)return;
            isNight=night; ambience.clip=night ? nightLoop:dayLoop; ambience.Play();
        }

        public void SetPaused(bool paused)
        {
            EnsureReady();
            if(paused){effects.Pause();ambience.Pause();}
            else{effects.UnPause();ambience.UnPause();}
        }

        void EnsureReady()
        {
            if(initialized)return;initialized=true;
            effects=gameObject.AddComponent<AudioSource>();effects.spatialBlend=0;effects.playOnAwake=false;effects.volume=.65f;
            ambience=gameObject.AddComponent<AudioSource>();ambience.loop=true;ambience.spatialBlend=0;ambience.playOnAwake=false;ambience.volume=.11f;
            Add("police",4.2f,11); Add("ambulance",4.2f,12); Add("fire",4.2f,13);
            Add("truck",5f,14);Add("unload",3.5f,15);Add("search",.8f,16);
            Add("collect",.22f,17);Add("sale",.7f,18);Add("hurt",.35f,19);
            Add("ending",2.2f,20);Add("warning",.7f,21);Add("heal",.8f,22);
            Add("flashlight",.12f,23);Add("rat",.5f,24);Add("error",.35f,25);
            dayLoop=Ambient(false);nightLoop=Ambient(true);
            ambience.clip=dayLoop;ambience.Play();
        }

        static string Normalize(string cue)
        {
            switch((cue??"").ToLowerInvariant())
            {
                case "garbage":case "garbage_truck":case "garbagetruck":return "truck";
                case "shop":case "shop_unload":case "shopunload":case "delivery":return "unload";
                case "sell":case "sold":return "sale";
                case "pickup":case "take":return "collect";
                case "damage":return "hurt";
                case "medkit":return "heal";
                case "fireengine":case "fire_engine":return "fire";
                default:return (cue??"collect").ToLowerInvariant();
            }
        }

        void Add(string key,float seconds,int seed)
        {
            var clip=AudioClip.Create("Lyokha original "+key,Mathf.CeilToInt(seconds*Rate),1,Rate,false);
            var samples=new float[clip.samples];var noise=new System.Random(seed);
            double phase=0,phase2=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=(float)i/Rate;float n=(float)(noise.NextDouble()*2-1);float x=0;
                float tail=Mathf.Clamp01((seconds-t)*6)*Mathf.Clamp01(t*35);
                switch(key)
                {
                    case "police":
                        phase+=2*Math.PI*(Mathf.Repeat(t,.44f)<.22f?740:1040)/Rate;
                        x=(float)Math.Sin(phase)*.62f+(float)Math.Sin(phase*2)*.12f;break;
                    case "ambulance":
                        phase+=2*Math.PI*Mathf.Lerp(480,1050,.5f+.5f*Mathf.Sin(t*Mathf.PI*1.1f))/Rate;
                        x=(float)Math.Sin(phase)*.6f;break;
                    case "fire":
                        phase+=2*Math.PI*(Mathf.Repeat(t,.95f)<.48f?420:560)/Rate;
                        x=((float)Math.Sin(phase)*.5f+(float)Math.Sin(phase*1.5)*.25f)*(.86f+.14f*Mathf.Sin(t*32));break;
                    case "truck":
                        phase+=2*Math.PI*(58+5*Mathf.Sin(t*4))/Rate;
                        x=(float)Math.Sin(phase)*.25f+n*.1f;
                        x+=Mathf.Repeat(t,.75f)<.19f?Mathf.Sin(t*2*Mathf.PI*890)*.32f:0;
                        x+=Mathf.Repeat(t,.41f)<.025f?n*.35f:0;break;
                    case "unload":
                        x=n*.09f;float strike=Mathf.Repeat(t,.7f);
                        x+=Mathf.Sin(strike*2*Mathf.PI*88)*Mathf.Exp(-strike*24)*.7f;
                        x+=Mathf.Repeat(t,1.3f)<.15f?Mathf.Sin(t*2*Mathf.PI*1250)*.19f:0;break;
                    case "search":
                        x=n*(.08f+.3f*Mathf.Pow(Mathf.Sin(t*29),6))*Mathf.Sin(Mathf.Min(1,t/seconds)*Mathf.PI);break;
                    case "collect": x=Mathf.Sin(t*2*Mathf.PI*(t<.1f?660:990))*Mathf.Exp(-t*12)*.7f;break;
                    case "sale":
                        float step=Mathf.Floor(t/.14f);float f=step<1?523:step<2?659:step<3?784:1047;
                        phase+=2*Math.PI*f/Rate;x=(float)Math.Sin(phase)*Mathf.Exp(-Mathf.Repeat(t,.14f)*12)*.55f;break;
                    case "hurt":x=(n*.45f+Mathf.Sin(t*2*Mathf.PI*110)*.25f)*Mathf.Exp(-t*9);break;
                    case "ending":
                        phase+=2*Math.PI*(t<.65f?261.63:t<1.3f?329.63:392)/Rate;
                        phase2+=2*Math.PI*523.25/Rate;
                        x=((float)Math.Sin(phase)*.4f+(float)Math.Sin(phase2)*.16f)*Mathf.Exp(-t*.8f);break;
                    case "warning": x=Mathf.Sin(t*2*Mathf.PI*370)*(Mathf.Repeat(t,.3f)<.15f?.55f:0);break;
                    case "heal":phase+=2*Math.PI*Mathf.Lerp(440,880,t/seconds)/Rate;x=(float)Math.Sin(phase)*.4f*Mathf.Sin(t/seconds*Mathf.PI);break;
                    case "flashlight":x=n*Mathf.Exp(-t*60)*.7f;break;
                    case "rat":phase+=2*Math.PI*(2100+550*Mathf.Sin(t*48))/Rate;x=(float)Math.Sin(phase)*Mathf.Pow(Mathf.Sin(t*18),4)*.22f;break;
                    case "error":phase+=2*Math.PI*Mathf.Lerp(300,110,t/seconds)/Rate;x=(float)Math.Sin(phase)*.4f;break;
                }
                samples[i]=Mathf.Clamp(x*tail,-.9f,.9f);
            }
            clip.SetData(samples,0);clips.Add(key,clip);
        }

        static AudioClip Ambient(bool night)
        {
            int count=Rate*12;var samples=new float[count];var random=new System.Random(night?321:123);
            float smooth=0;
            for(int i=0;i<count;i++)
            {
                float t=(float)i/Rate;float n=(float)(random.NextDouble()*2-1);
                smooth=Mathf.Lerp(smooth,n,.015f);
                float x=smooth*.48f+Mathf.Sin(t*2*Mathf.PI*50)*.014f;
                if(night)
                {
                    float cricket=Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*7.4f)),12);
                    x+=Mathf.Sin(t*2*Mathf.PI*3200)*cricket*.065f;
                }
                else
                {
                    float bird=Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*1.57f)),32);
                    x+=Mathf.Sin(t*2*Mathf.PI*(1800+180*Mathf.Sin(t*33)))*bird*.07f;
                }
                // Fade at both ends makes the procedural loop seam inaudible.
                float envelope=Mathf.Min(1,Mathf.Min(t,(float)count/Rate-t)*2);
                samples[i]=x*envelope;
            }
            var clip=AudioClip.Create(night?"Night yard":"Day yard",count,1,Rate,false);clip.SetData(samples,0);return clip;
        }

        void OnDestroy()
        {
            foreach(var clip in clips.Values)if(clip!=null)Destroy(clip);
            if(dayLoop!=null)Destroy(dayLoop);if(nightLoop!=null)Destroy(nightLoop);
        }
    }
}
