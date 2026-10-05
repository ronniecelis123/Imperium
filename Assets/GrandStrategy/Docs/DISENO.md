# Diseño – Juego de Grand Strategy (inspirado en Age of History II + Hearts of Iron IV)

## 1. Investigación: cómo funcionan los juegos de referencia

### Hearts of Iron IV (Paradox, motor Clausewitz)
**Mapa (datos):** todo el mapa se define con *archivos de datos* editables, no con escenas:

| Archivo | Contenido |
|---|---|
| `provinces.bmp` | Imagen donde **cada provincia tiene un color RGB único**. Es la "verdad" de qué píxel pertenece a qué provincia. |
| `definition.csv` | `id;r;g;b;tipo(land/sea/lake);costera;terreno;continente`. Une color ⇄ id de provincia. |
| `heightmap.bmp` | Escala de grises. Relieve real usado para desplazar la malla 3D. Nivel del mar ≈ 95/255. |
| `terrain.bmp`, `trees.bmp`, `rivers.bmp` | Tipo de terreno, bosques y ríos. |
| `colormap_*.dds`, `normal.bmp` | Color base del terreno pintado y mapa de normales. |
| `states/*.txt` | Las provincias se agrupan en **estados** (población, fábricas, recursos, núcleos). |
| `history/countries/*.txt` | Países: TAG de 3 letras (GER, ENG…), color, ideología, líderes. |

**Render:** malla del mundo desplazada por el heightmap, texturas de terreno mezcladas, agua con
shader propio, y encima una capa política: el shader mira el id de provincia de cada píxel, consulta
una *textura lookup* (id → color del dueño) y tiñe el terreno. Las fronteras se calculan comparando
provincias vecinas (frontera de provincia fina, frontera nacional gruesa). Al alejarse mucho la
cámara pasa a un **"mapa de papel"** plano y muy legible; al acercarse se ve el relieve 3D.
Los "map modes" (político, terreno, recursos, suministro…) solo cambian el color que devuelve la lookup.

**Mecánicas:** tiempo real con pausa y 5 velocidades (días/horas), producción con fábricas
civiles/militares, investigación, focos nacionales (árboles de decisiones), división de tropas
con plantillas, frentes y planes de batalla, suministro, diplomacia/facciones, ideologías.

### Age of History II (Łukasz Jakowski)
**Mapa:** provincias como polígonos (contorno de puntos) agrupadas en civilizaciones; hay muchos
mapas y escenarios, y un **editor** para crear mapas, provincias y escenarios. Cada provincia
tiene población, economía, felicidad, tipo de terreno, y edificios.
**Mecánicas:** **por turnos**; cada turno un país tiene puntos de movimiento y dinero; se reclutan
y mueven ejércitos entre provincias, se anexa por conquista, diplomacia sencilla (alianzas,
pactos, guerras, vasallos), tecnología por niveles, revoluciones, ideologías/gobiernos y eventos.
Jugar con cualquier civilización del mapa y empezar desde un escenario (año histórico).

### Lo que tomamos de cada uno
* **De AoH2:** escala y sencillez de reglas (provincia = unidad básica, por turnos o tiempo simple,
  cualquier país jugable, editor/escenarios).
* **De HOI4:** arquitectura de datos del mapa (imagen de provincias + CSV), estética del render
  (relieve 3D, fronteras suaves, tinte político sobre terreno, modo papel al alejar) y cámara.

## 2. Arquitectura implementada (fase 1: mapa + menú + selección de país)

```
Assets/GrandStrategy/
├─ Scripts/
│  ├─ Core/         GameBootstrap (arranque), GameManager (estados), GameState
│  ├─ Map/Data/     MapData, Province, Country, TerrainType, WorldData
│  ├─ Map/Generation/ WorldGenerator (procedural), Noise, NameGenerator
│  ├─ Map/IO/       MapFileIO (cargar/exportar formato estilo HOI4)
│  ├─ Map/Rendering/ MapRenderer (malla + texturas + shader), MapModes
│  ├─ Map/Interaction/ MapPicker (rayo→provincia), MapInteraction (hover/click)
│  ├─ CameraControl/ StrategyCamera (zoom rueda, pan WASD/arrastre, inclinación)
│  └─ UI/           UIFactory, LoadingScreen, MainMenuUI, CountrySelectUI, GameHUD, CountryInfoPanel
├─ Editor/          Menú "Grand Strategy" (crear escena, exportar mapa)
└─ Resources/Shaders/MapTerrain.shader
```

### Flujo de datos
1. `GameBootstrap` busca un mapa en `StreamingAssets/Maps/<nombre>/`. Si existe lo carga
   (`provinces.png`, `definition.csv`, `heightmap.png`, `countries.json`); si no, `WorldGenerator`
   crea un mundo procedural (continentes, relieve, biomas, ~1500 provincias, ~70 países).
2. `MapRenderer` sube texturas a la GPU:
   * `_ProvinceIdTex` – id de provincia por píxel (RG = 16 bits, filtro *point*).
   * `_ProvinceDataTex` – 256×N, color de map-mode + flags (agua, hover, selección).
   * `_ProvinceOwnerTex` – 256×N, id del país dueño (para fronteras nacionales independientes del map mode).
   * `_HeightTex` – relieve; `_ColorMap` – color del terreno pintado.
3. El shader desplaza los vértices con el heightmap, calcula normales por píxel, pinta agua,
   tiñe por país y dibuja fronteras suaves con anchura constante en pantalla.
4. `MapInteraction` lanza el rayo (`MapPicker`) y emite eventos `ProvinceHovered/ProvinceClicked`.
   `GameManager` decide qué hacer según el estado (selección de país, partida…).

### Formato de mapa propio (editable, estilo HOI4)
* `provinces.png` – color único por provincia.
* `definition.csv` – `id;r;g;b;type;coastal;terrain;continent` (mismo formato que HOI4).
* `heightmap.png` – gris; `map.json` guarda `seaLevel` y tamaño.
* `countries.json` – países (tag, nombre, color, gobierno, capital, provincias).
Menú **Grand Strategy ▸ Exportar mapa generado** escribe estos archivos para poder editarlos
en un editor de imágenes o sustituirlos por un mapa real de la Tierra.

## 3. Hoja de ruta
- [x] Fase 1: mapa con relieve, texturas, provincias, fronteras, zoom, menú, elección de país, panel de info.
- [ ] Fase 2: ríos, nombres de países sobre el mapa (texto curvado), estados/regiones, más map modes.
- [ ] Fase 3: tiempo (fecha + velocidades) o turnos, economía básica (oro, población, producción).
- [ ] Fase 4: ejércitos y movimiento entre provincias, guerra y ocupación.
- [ ] Fase 5: diplomacia, IA, tecnología, eventos/focos, guardado/carga, editor de escenarios.
