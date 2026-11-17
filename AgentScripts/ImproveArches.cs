using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ImproveArches
{
    public static object Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var filter = GameObject.Find("Cube").GetComponent<MeshFilter>();
        var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
        mesh.name = "CourtyardWallsRaisedArches";
        var vertices = mesh.vertices;
        var loweredCeilingPositions = new HashSet<Vector3>();
        foreach (var sub in Enumerable.Range(0, mesh.subMeshCount))
        {
            var triangles = mesh.GetTriangles(sub);
            for (int i=0;i<triangles.Length;i+=3)
            {
                var a=filter.transform.TransformPoint(vertices[triangles[i]]);
                var b=filter.transform.TransformPoint(vertices[triangles[i+1]]);
                var c=filter.transform.TransformPoint(vertices[triangles[i+2]]);
                if (Mathf.Abs(a.y-1.98f)>.03f || Mathf.Abs(b.y-a.y)>.02f || Mathf.Abs(c.y-a.y)>.02f) continue;
                var normal=Vector3.Cross(b-a,c-a).normalized;
                if (normal.y > -.8f) continue;
                loweredCeilingPositions.Add(a);loweredCeilingPositions.Add(b);loweredCeilingPositions.Add(c);
            }
        }
        int changed=0;
        for(int i=0;i<vertices.Length;i++)
        {
            var world=filter.transform.TransformPoint(vertices[i]);
            if (!loweredCeilingPositions.Contains(world)) continue;
            world.y=4.02f;vertices[i]=filter.transform.InverseTransformPoint(world);changed++;
        }
        if(changed==0)throw new InvalidOperationException("No low ceiling vertices matched; no mesh was changed.");
        mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();
        const string path="Assets/_Project/Art/Generated/CourtyardWallsRaisedArches.asset";
        if(AssetDatabase.LoadAssetAtPath<Mesh>(path)!=null)throw new InvalidOperationException("Raised wall mesh already exists.");
        AssetDatabase.CreateAsset(mesh,path);filter.sharedMesh=mesh;filter.GetComponent<MeshCollider>().sharedMesh=mesh;
        EditorSceneManager.MarkSceneDirty(filter.gameObject.scene);EditorSceneManager.SaveScene(filter.gameObject.scene);AssetDatabase.SaveAssets();
        Physics.SyncTransforms();
        var samples=new[]{new Vector3(-1,1.7f,17),new Vector3(11,1.7f,-18)}.Select(p=>new{at=p.ToString(),ceiling=Physics.Raycast(p,Vector3.up,out var hit,30)?hit.point.y:-1}).ToArray();
        return new{changedVertices=changed,targetCeilingHeight=4.02f,samples,originalFBXUntouched=true};
    }
}
