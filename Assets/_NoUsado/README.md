# _NoUsado — material legacy, no referenciado

Todo lo que hay aquí **no lo usa el juego** (0 referencias desde escenas, prefabs, assets o scripts).
Se conserva sólo por si alguna vez hace falta consultarlo.

**Para eliminar:** borrar la carpeta `Assets/_NoUsado` completa. No hay que tocar nada más
(las escenas ya no están en *Build Settings*).

## Contenido

| Ruta | Qué es | Por qué está aquí |
|---|---|---|
| `Scenes/InventoryMenu.unity` | Menú de inventario 3D antiguo | Sustituido por el inventario dentro de `StartMenu` (`InventoryManager`). Quitado de Build Settings. |
| `Scenes/PerkMenu.unity` | Menú de perks antiguo | Igual, sustituido por la UI de perks del menú. Quitado de Build Settings. |
| `Scenes/WeaponMenu.unity` | Menú de armas antiguo | Nunca estuvo en Build Settings. Sustituido por la armería (`LobbyArmeriaController` / `ArmeriaUI`). |
| `Scripts/ItemInfo.cs` | Alias legacy de `ItemDefinition` (marcado `[Obsolete]`) | Nadie lo referencia; el padre canónico es `ItemDefinition`. |
| `Scripts/PersistentesDebug.cs` | Volcado manual de objetos `DontDestroyOnLoad` | Herramienta puntual de auditoría; sustituida por `NetcodeTestFlow` (F5). |
| `Samples/Shader Graph` | Samples importados del paquete Shader Graph (11 MB) | 0 referencias; no los usa ningún material/escena del proyecto. |
| `_Recovery/` | Escenas de recuperación de Unity tras un crash (2 MB) | Basura generada por el editor. Además está en `.gitignore`. |
| `Temp/game_view.png` | Captura suelta | Artefacto de herramienta. |

## Qué NO está aquí (y por qué)

- `Assets/Editor/SteamUIBuilder.cs` → **herramienta en uso** (`Tools/Build Steam Main Menu UI`).
  Ojo: destruye y regenera los paneles del menú y hace `SaveOpenScenes()`.
- `Assets/Tests/` → los usa Unity Test Framework (por atributo, no por nombre).
- `Assets/Scripts/Debug/NetcodeTestFlow.cs` → arnés de validación multijugador (F1–F5).
- `Assets/Sherbbs Particle Collection` → **sí se usa** (muzzle flash / impactos en `Player.prefab` y `SampleScene`).
- `Assets/Adaptive Performance` → referenciado desde *Project Settings* → *Adaptive Performance*.
