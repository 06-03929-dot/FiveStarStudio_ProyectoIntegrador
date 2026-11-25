using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Al importar los FBX de Assets/Murther/Modelos, promedia las normales de los vértices que comparten
// posición y guarda el resultado en los tangentes (xyz = normal suave, w = 2 como marca).
// El contorno del shader "Five Star/Murther Tinta" se infla siguiendo esa normal: con las normales
// originales, en las esquinas duras del cuerpo y de los pies el contorno se abría en huecos.
// Se usan los tangentes porque se deforman con los huesos (los UV no) y el shader de tinta no usa mapas
// de normales. Los archivos FBX no se modifican: solo cambian los datos importados.
public class NormalesSuavesContorno : AssetPostprocessor
{
    const string Carpeta = "Assets/Murther/Modelos/";

    public override uint GetVersion() { return 1; }

    void OnPostprocessModel(GameObject raiz)
    {
        if (!assetPath.StartsWith(Carpeta)) return;
        foreach (var smr in raiz.GetComponentsInChildren<SkinnedMeshRenderer>(true)) Suavizar(smr.sharedMesh);
        foreach (var mf in raiz.GetComponentsInChildren<MeshFilter>(true)) Suavizar(mf.sharedMesh);
    }

    static void Suavizar(Mesh malla)
    {
        if (malla == null) return;
        var posiciones = malla.vertices;
        var normales = malla.normals;
        if (normales == null || normales.Length != posiciones.Length) return;

        var suma = new Dictionary<Vector3Int, Vector3>();
        var claves = new Vector3Int[posiciones.Length];
        for (int i = 0; i < posiciones.Length; i++)
        {
            var p = posiciones[i] * 10000f;      // agrupa vértices a menos de 0.1 mm (en unidades del FBX)
            var k = new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
            claves[i] = k;
            suma.TryGetValue(k, out var s);
            suma[k] = s + normales[i];
        }

        var tangentes = new Vector4[posiciones.Length];
        for (int i = 0; i < posiciones.Length; i++)
        {
            var n = suma[claves[i]];
            n = n.sqrMagnitude > 1e-12f ? n.normalized : normales[i];
            tangentes[i] = new Vector4(n.x, n.y, n.z, 2f);
        }
        malla.tangents = tangentes;
    }
}
