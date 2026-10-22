using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Partida FFA offline (sin Netcode): countdown 5s, HUD visible y
/// muerte → respawn con vida restaurada. Corre en la escena vacía del
/// Test Runner, sin depender de SampleScene ni de Steam.
/// </summary>
public class PartidaOfflineTests
{
    private GameObject managerGo;
    private GameObject jugadorGo;

    [UnityTearDown]
    public IEnumerator Limpiar()
    {
        if (jugadorGo != null) Object.Destroy(jugadorGo);
        if (managerGo != null) Object.Destroy(managerGo);
        // Limpia overlays de countdown/HUD que dejan los sistemas
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go.name == "MatchCountdownOverlay") Object.Destroy(go);
        }
        yield return null;
        jugadorGo = null;
        managerGo = null;
    }

    [UnityTest]
    public IEnumerator PartidaOffline_CuentaAtras_HUD_y_Respawn()
    {
        managerGo = new GameObject("GameModeManager_Test");
        var manager = managerGo.AddComponent<GameModeManager>();
        Assert.IsNotNull(manager);

        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        Assert.IsNotNull(prefab, "Falta el prefab Player en Assets/Prefabs/Player.prefab");
        jugadorGo = Object.Instantiate(prefab);
        var pc = jugadorGo.GetComponent<PlayerController>();
        Assert.IsNotNull(pc, "El prefab Player no trae PlayerController");

        // 1. Countdown 5s → EnCurso (timeout 20s)
        float t = 0f;
        while (manager.estado.Value != (int)GameModeManager.EstadoPartida.EnCurso && t < 20f)
        {
            t += Time.deltaTime;
            yield return null;
        }
        Assert.AreEqual((int)GameModeManager.EstadoPartida.EnCurso, manager.estado.Value,
            "La partida offline no entró en curso tras el countdown");

        // 2. HUD de puntuación visible
        GameModeHUD.EnsureExists();
        yield return null;
        Assert.IsNotNull(GameModeHUD.Instance, "GameModeHUD no se creó");

        // 3. Daño letal → muerto
        Assert.IsNotNull(pc.Vida, "El jugador no tiene Health");
        pc.Vida.RecibirDanio(9999f, pc.transform.position, Vector3.up, jugadorGo);
        Assert.IsTrue(pc.Vida.Muerto, "El jugador debería estar muerto tras 9999 de daño");

        // 4. Respawn (delay 3s por defecto) restaura la vida (timeout 10s)
        float t2 = 0f;
        while (pc.Vida.Muerto && t2 < 10f)
        {
            t2 += Time.deltaTime;
            yield return null;
        }
        Assert.IsFalse(pc.Vida.Muerto, "El jugador debería haber respawneado con vida restaurada");
        Assert.Greater(pc.Vida.Vida, 0f);
    }
}
