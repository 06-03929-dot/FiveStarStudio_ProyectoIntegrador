using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Genera un mapa de normales a partir de la ilustración (solo del dibujo, sin modelos ni alturas a mano).
// La altura de cada punto combina tres lecturas del dibujo:
//  1. Volumen de las formas: los contornos de tinta y los huecos transparentes son el borde de cada piedra,
//     ladrillo o columna; lejos del borde la forma "sube" con un perfil redondeado (en dos escalas:
//     bisel corto para el canto y abombado largo para el volumen). Las manchas de tinta pequeñas
//     (el punteado de los muros) no cuentan como contorno: si contaran, el muro se vería acolchado.
//  2. Sombreado pintado: lo que el artista pintó más claro está más cerca (sigue sus propias luces).
//  3. Detalle: el punteado y el rayado de tinta quedan como pequeños surcos y poros.
// Con la altura se calculan las pendientes (diferencias centrales) y de ahí la normal.
// El mapa se guarda a la mitad de la resolución del dibujo: la luz es más suave que el trazo y así
// pesa una cuarta parte. Los archivos originales no se modifican.
public static class GeneradorNormalesFondo
{
    public const string Carpeta = "Assets/Fondos/Normales";

    public struct Ajustes
    {
        public float umbralTinta;     // luminancia bajo la cual el trazo cuenta como contorno
        public int areaMinimaTrazo;   // manchas de tinta más chicas (en píxeles del mapa) son punteado, no contorno
        public float bisel;           // radio del canto, en píxeles del mapa
        public float abombado;        // radio del volumen grande, en píxeles del mapa
        public float pesoBisel, pesoAbombado, pesoSombreado, pesoDetalle;
        public float fuerza;          // escala de las pendientes (más = relieve más marcado)

        public static Ajustes PorDefecto => new Ajustes
        {
            umbralTinta = 0.075f, areaMinimaTrazo = 320, bisel = 6f, abombado = 26f,
            pesoBisel = 0.6f, pesoAbombado = 1.6f, pesoSombreado = 1.6f, pesoDetalle = 0.3f,
            fuerza = 1.7f,
        };
    }

    public static string RutaNormal(string rutaIlustracion)
    {
        // Sin acentos ni espacios: el nombre generado no debe depender de cómo cada sistema guarda los acentos.
        var nombre = Path.GetFileNameWithoutExtension(rutaIlustracion).Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (char c in nombre)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        }
        return Carpeta + "/" + sb + "_Normal.png";
    }

    public static Texture2D Generar(string rutaIlustracion) { return Generar(rutaIlustracion, Ajustes.PorDefecto); }

    public static Texture2D Generar(string rutaIlustracion, Ajustes a)
    {
        // Se leen los bytes del PNG: no depende de la compresión ni de "Read/Write" del importador.
        var original = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        original.LoadImage(File.ReadAllBytes(rutaIlustracion));
        int W0 = original.width, H0 = original.height;
        var px = original.GetPixels32();
        Object.DestroyImmediate(original);

        // Mitad de resolución, promediando bloques de 2x2 (fila 0 = abajo, como en Unity).
        int W = W0 / 2, H = H0 / 2, N = W * H;
        var lum = new float[N];
        var alfa = new float[N];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float l = 0f, al = 0f;
                for (int dy = 0; dy < 2; dy++)
                    for (int dx = 0; dx < 2; dx++)
                    {
                        var c = px[(y * 2 + dy) * W0 + x * 2 + dx];
                        float ca = c.a / 255f;
                        l += (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f * ca;
                        al += ca;
                    }
                lum[y * W + x] = l * 0.25f;
                alfa[y * W + x] = al * 0.25f;
            }
        px = null;

        // 1. Volumen de las formas: distancia al contorno de tinta o al hueco transparente.
        //    El desenfoque corto evita que cada punto del punteado cuente como contorno.
        var lumSuave = Desenfocar(lum, W, H, 1);
        var tinta = new bool[N];
        for (int i = 0; i < N; i++) tinta[i] = alfa[i] > 0.5f && lumSuave[i] <= a.umbralTinta;
        var punteado = (bool[])tinta.Clone();
        QuitarManchasPequenas(tinta, W, H, a.areaMinimaTrazo);
        for (int i = 0; i < N; i++) punteado[i] &= !tinta[i];
        var dentro = new bool[N];
        for (int i = 0; i < N; i++) dentro[i] = alfa[i] > 0.5f && !tinta[i];
        var dist = DistanciaAlBorde(dentro, W, H);

        // 2. Sombreado pintado. Antes se rellenan los puntos del punteado con el tono de su alrededor
        //    (promedio ponderado que ignora los puntos): si no, cada punto deja un hoyo y el muro se ve martillado.
        var peso = new float[N];
        var conPeso = new float[N];
        for (int i = 0; i < N; i++) peso[i] = punteado[i] ? 0f : 1f;
        var cerca = Desenfocar(peso, W, H, 1);
        for (int i = 0; i < N; i++) { if (cerca[i] < 0.999f) peso[i] = 0f; conPeso[i] = lum[i] * peso[i]; }
        var num = Desenfocar(conPeso, W, H, 3);
        var den = Desenfocar(peso, W, H, 3);
        var lumLimpia = new float[N];
        for (int i = 0; i < N; i++) lumLimpia[i] = peso[i] > 0f ? lum[i] : (den[i] > 1e-4f ? num[i] / den[i] : lum[i]);
        peso = conPeso = cerca = num = den = null;
        var sombreado = Desenfocar(lumLimpia, W, H, 6);
        lumLimpia = null;
        // 3. Detalle del trazo (fino): aquí sí quedan los puntos, como poros pequeños.

        // El abombado se suaviza: la distancia por chaflán deja pliegues rectos en el eje de cada forma.
        var bisel = new float[N];
        var abombado = new float[N];
        for (int i = 0; i < N; i++) { bisel[i] = Perfil(dist[i] / a.bisel); abombado[i] = Perfil(dist[i] / a.abombado); }
        bisel = Desenfocar(bisel, W, H, 1);
        abombado = Desenfocar(abombado, W, H, 3);

        var altura = new float[N];
        for (int i = 0; i < N; i++)
            altura[i] = a.pesoBisel * bisel[i] + a.pesoAbombado * abombado[i] + a.pesoSombreado * sombreado[i] + a.pesoDetalle * lumSuave[i];
        bisel = abombado = null;
        lum = sombreado = lumSuave = null;
        dist = null;

        // Pendientes con diferencias centrales y normal en espacio tangente (+Y arriba, convención de Unity).
        var salida = new Color32[N];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                if (alfa[i] < 0.02f) { salida[i] = new Color32(128, 128, 255, 255); continue; }
                int xi = Mathf.Max(x - 1, 0), xd = Mathf.Min(x + 1, W - 1);
                int ya = Mathf.Max(y - 1, 0), yb = Mathf.Min(y + 1, H - 1);
                float gx = (altura[y * W + xd] - altura[y * W + xi]) * 0.5f * a.fuerza;
                float gy = (altura[yb * W + x] - altura[ya * W + x]) * 0.5f * a.fuerza;
                var n = new Vector3(-gx, -gy, 1f).normalized;
                salida[i] = new Color32((byte)Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f),
                                        (byte)Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f),
                                        (byte)Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f), 255);
            }

        if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/Fondos", "Normales");
        string ruta = RutaNormal(rutaIlustracion);
        var mapa = new Texture2D(W, H, TextureFormat.RGB24, false, true);
        mapa.SetPixels32(salida);
        File.WriteAllBytes(ruta, mapa.EncodeToPNG());
        Object.DestroyImmediate(mapa);

        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceSynchronousImport);
        var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
        imp.textureType = TextureImporterType.NormalMap;
        imp.mipmapEnabled = true;
        imp.filterMode = FilterMode.Trilinear;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.maxTextureSize = 2048;
        imp.textureCompression = TextureImporterCompression.CompressedHQ;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
    }

    // Subida redondeada: 0 en el borde, 1 a partir del radio (pendiente máxima en el borde, plana arriba).
    static float Perfil(float t)
    {
        t = Mathf.Clamp01(t);
        return t * (2f - t);
    }

    // Borra (marca como no tinta) las manchas conectadas con menos de "areaMinima" píxeles.
    static void QuitarManchasPequenas(bool[] tinta, int W, int H, int areaMinima)
    {
        var visto = new bool[tinta.Length];
        var pila = new int[tinta.Length];
        var mancha = new List<int>();
        for (int inicio = 0; inicio < tinta.Length; inicio++)
        {
            if (!tinta[inicio] || visto[inicio]) continue;
            mancha.Clear();
            int tope = 0;
            pila[tope++] = inicio;
            visto[inicio] = true;
            bool grande = false;
            while (tope > 0)
            {
                int i = pila[--tope];
                if (!grande) { mancha.Add(i); grande = mancha.Count >= areaMinima; }
                int x = i % W, y = i / W;
                if (x > 0 && tinta[i - 1] && !visto[i - 1]) { visto[i - 1] = true; pila[tope++] = i - 1; }
                if (x < W - 1 && tinta[i + 1] && !visto[i + 1]) { visto[i + 1] = true; pila[tope++] = i + 1; }
                if (y > 0 && tinta[i - W] && !visto[i - W]) { visto[i - W] = true; pila[tope++] = i - W; }
                if (y < H - 1 && tinta[i + W] && !visto[i + W]) { visto[i + W] = true; pila[tope++] = i + W; }
            }
            if (!grande) foreach (int i in mancha) tinta[i] = false;
        }
    }

    // Distancia (en píxeles) de cada punto "dentro" al punto "fuera" más cercano. Chaflán 3-4 en dos pasadas.
    static float[] DistanciaAlBorde(bool[] dentro, int W, int H)
    {
        const int Grande = 1 << 28;
        var d = new int[W * H];
        for (int i = 0; i < d.Length; i++) d[i] = dentro[i] ? Grande : 0;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                if (d[i] == 0) continue;
                int m = d[i];
                if (x > 0) m = Mathf.Min(m, d[i - 1] + 3);
                if (y > 0)
                {
                    m = Mathf.Min(m, d[i - W] + 3);
                    if (x > 0) m = Mathf.Min(m, d[i - W - 1] + 4);
                    if (x < W - 1) m = Mathf.Min(m, d[i - W + 1] + 4);
                }
                d[i] = m;
            }
        for (int y = H - 1; y >= 0; y--)
            for (int x = W - 1; x >= 0; x--)
            {
                int i = y * W + x;
                if (d[i] == 0) continue;
                int m = d[i];
                if (x < W - 1) m = Mathf.Min(m, d[i + 1] + 3);
                if (y < H - 1)
                {
                    m = Mathf.Min(m, d[i + W] + 3);
                    if (x < W - 1) m = Mathf.Min(m, d[i + W + 1] + 4);
                    if (x > 0) m = Mathf.Min(m, d[i + W - 1] + 4);
                }
                d[i] = m;
            }
        var r = new float[W * H];
        for (int i = 0; i < r.Length; i++) r[i] = d[i] / 3f;
        return r;
    }

    // Aproximación de un desenfoque gaussiano: tres pasadas de caja (horizontal y vertical) con suma corrida.
    static float[] Desenfocar(float[] src, int W, int H, int radio)
    {
        var a = (float[])src.Clone();
        var b = new float[a.Length];
        for (int k = 0; k < 3; k++)
        {
            CajaHorizontal(a, b, W, H, radio);
            CajaVertical(b, a, W, H, radio);
        }
        return a;
    }

    static void CajaHorizontal(float[] s, float[] d, int W, int H, int r)
    {
        float inv = 1f / (2 * r + 1);
        for (int y = 0; y < H; y++)
        {
            int f = y * W;
            float suma = 0f;
            for (int x = -r; x <= r; x++) suma += s[f + Mathf.Clamp(x, 0, W - 1)];
            for (int x = 0; x < W; x++)
            {
                d[f + x] = suma * inv;
                suma += s[f + Mathf.Min(x + r + 1, W - 1)] - s[f + Mathf.Max(x - r, 0)];
            }
        }
    }

    static void CajaVertical(float[] s, float[] d, int W, int H, int r)
    {
        float inv = 1f / (2 * r + 1);
        for (int x = 0; x < W; x++)
        {
            float suma = 0f;
            for (int y = -r; y <= r; y++) suma += s[Mathf.Clamp(y, 0, H - 1) * W + x];
            for (int y = 0; y < H; y++)
            {
                d[y * W + x] = suma * inv;
                suma += s[Mathf.Min(y + r + 1, H - 1) * W + x] - s[Mathf.Max(y - r, 0) * W + x];
            }
        }
    }
}
