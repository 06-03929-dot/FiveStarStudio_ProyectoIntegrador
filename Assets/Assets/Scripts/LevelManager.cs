using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Nivel 1 = 1 enemigo, nivel 2 = 2, nivel 3 = 3... (configurable).
// Los tipos se reparten en ciclo: deslizador, arquero, mordedor, deslizador, ...
// Guarda: nivel, vida, posición del jugador y qué enemigos siguen vivos.
public class LevelManager : MonoBehaviour
{
    [Header("Jugador")]
    public PlayerController3D player;
    public Transform playerSpawn;

    [Header("Prefabs de enemigos")]
    public EnemyBase sliderPrefab;
    public EnemyBase archerPrefab;
    public EnemyBase biterPrefab;

    [Header("Puntos de aparición (objetos vacíos en la escena)")]
    public Transform[] groundPoints;    // para el deslizador (sobre el suelo)
    public Transform[] airPoints;       // para el arquero (en el aire)
    public Transform[] holePoints;      // para el mordedor (en la superficie del agujero)

    [Header("Dificultad")]
    public int startLevel = 1;
    public int firstLevelEnemies = 1;
    public int enemiesAddedPerLevel = 1;
    public int maxEnemies = 12;
    public float nextLevelDelay = 2f;
    public bool restartFromLevelOneOnDeath = false;
    public float fallLimit = -15f;      // si el jugador cae más abajo, reaparece

    [Header("Progreso")]
    public bool saveProgress = true;
    public float autosaveEvery = 5f;

    const string K_Level = "NivelGuardado";
    const string K_Health = "VidaGuardada";
    const string K_PosX = "PosXGuardada";
    const string K_PosY = "PosYGuardada";
    const string K_Alive = "EnemigosVivos";

    public int Level { get; private set; }

    // enemigo -> número de enemigo dentro del nivel (para saber cuáles siguen vivos)
    readonly Dictionary<EnemyBase, int> alive = new Dictionary<EnemyBase, int>();
    PlayerHealth health;
    bool changingLevel;
    Coroutine nextLevelRoutine;
    float saveTimer;
    GUIStyle style;

    void Start()
    {
        health = player.GetComponent<PlayerHealth>();
        health.Died += OnPlayerDied;
        Level = startLevel;
        HashSet<int> only = null;

        if (saveProgress && PlayerPrefs.HasKey(K_Level))
        {
            Level = Mathf.Max(startLevel, PlayerPrefs.GetInt(K_Level));
            health.SetHealth(PlayerPrefs.GetInt(K_Health, health.maxHealth));

            if (PlayerPrefs.HasKey(K_PosX))
                player.Teleport(new Vector3(PlayerPrefs.GetFloat(K_PosX), PlayerPrefs.GetFloat(K_PosY), 0f));

            // "*" = no hay dato guardado (todos); "" = ya no quedaba ninguno
            string saved = PlayerPrefs.GetString(K_Alive, "*");
            if (saved != "*")
            {
                only = new HashSet<int>();
                foreach (string part in saved.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    if (int.TryParse(part, out int idx)) only.Add(idx);
            }
        }

        SpawnEnemies(only);

        // Se cerró el juego justo al completar el nivel: pasa al siguiente
        if (only != null && alive.Count == 0)
            nextLevelRoutine = StartCoroutine(NextLevel());
    }

    void Update()
    {
        if (player.transform.position.y < fallLimit)
            player.Teleport(playerSpawn != null ? playerSpawn.position : Vector3.zero);

        if (!saveProgress) return;
        saveTimer += Time.deltaTime;
        if (saveTimer >= autosaveEvery)
        {
            saveTimer = 0f;
            SaveProgress();
        }
    }

    public int EnemiesForLevel(int level)
    {
        return Mathf.Min(maxEnemies, firstLevelEnemies + (level - 1) * enemiesAddedPerLevel);
    }

    void SpawnEnemies(HashSet<int> only)
    {
        int total = EnemiesForLevel(Level);
        int[] used = new int[3];

        for (int i = 0; i < total; i++)
        {
            int type = i % 3;
            EnemyBase prefab = type == 0 ? sliderPrefab : type == 1 ? archerPrefab : biterPrefab;
            Transform[] points = type == 0 ? groundPoints : type == 1 ? airPoints : holePoints;

            if (prefab == null || points == null || points.Length == 0)
            {
                Debug.LogWarning("LevelManager: falta el prefab o los puntos de aparición del tipo " + type);
                continue;
            }

            Transform p = points[used[type]++ % points.Length];
            if (only != null && !only.Contains(i)) continue;   // este ya lo habías derrotado

            EnemyBase e = Instantiate(prefab, p.position, Quaternion.identity);
            e.Init(player.transform);
            e.Died += OnEnemyDied;
            alive[e] = i;
        }
    }

    void OnEnemyDied(EnemyBase e)
    {
        alive.Remove(e);
        SaveProgress();
        if (alive.Count == 0 && !changingLevel)
            nextLevelRoutine = StartCoroutine(NextLevel());
    }

    IEnumerator NextLevel()
    {
        changingLevel = true;
        yield return new WaitForSeconds(nextLevelDelay);
        Level++;
        health.Heal(1);
        SpawnEnemies(null);
        changingLevel = false;
        SaveProgress();
    }

    void OnPlayerDied() => StartCoroutine(RespawnRoutine());

    IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(1f);
        if (nextLevelRoutine != null) StopCoroutine(nextLevelRoutine);
        changingLevel = false;

        ClearEnemies();
        if (restartFromLevelOneOnDeath) Level = startLevel;

        player.Teleport(playerSpawn != null ? playerSpawn.position : Vector3.zero);
        health.ResetHealth();
        SpawnEnemies(null);
        SaveProgress();
    }

    void ClearEnemies()
    {
        foreach (var e in alive.Keys)
        {
            if (e == null) continue;
            e.Died -= OnEnemyDied;
            Destroy(e.gameObject);
        }
        alive.Clear();

        foreach (var a in FindObjectsByType<Arrow>(FindObjectsSortMode.None))
            Destroy(a.gameObject);
    }

    // ---------- GUARDADO ----------
    public void SaveProgress()
    {
        if (!saveProgress || health == null || health.Health <= 0) return;

        PlayerPrefs.SetInt(K_Level, Level);
        PlayerPrefs.SetInt(K_Health, health.Health);
        PlayerPrefs.SetFloat(K_PosX, player.transform.position.x);
        PlayerPrefs.SetFloat(K_PosY, player.transform.position.y);
        PlayerPrefs.SetString(K_Alive, string.Join(",", alive.Values));
        PlayerPrefs.Save();
    }

    // Clic derecho en el componente > Borrar progreso guardado
    [ContextMenu("Borrar progreso guardado")]
    public void ResetProgress()
    {
        PlayerPrefs.DeleteKey(K_Level);
        PlayerPrefs.DeleteKey(K_Health);
        PlayerPrefs.DeleteKey(K_PosX);
        PlayerPrefs.DeleteKey(K_PosY);
        PlayerPrefs.DeleteKey(K_Alive);
        PlayerPrefs.Save();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) SaveProgress();   // al minimizar o cerrar en el celular
    }

    void OnApplicationQuit() => SaveProgress();

    // Texto simple en pantalla (no necesitas crear UI todavía)
    void OnGUI()
    {
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            style.normal.textColor = Color.white;
        }

        GUI.Label(new Rect(15, 10, 700, 40), $"Nivel {Level}    Enemigos: {alive.Count}    Vida: {health.Health}", style);

        if (changingLevel)
            GUI.Label(new Rect(15, 50, 700, 40), "¡Nivel completado!", style);
    }
}
