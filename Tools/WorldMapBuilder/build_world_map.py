"""
Conversor del mapa mundial al formato de mapa del juego (estilo HOI4), con la situación política
del 1 de enero de 1936 (ver history_1936.py).

Fuentes:
  - Natural Earth 1:10m (dominio público): provincias (admin-1), países (admin-0), ciudades, mares.
  - NASA Visible Earth (dominio público): elevación y batimetría GEBCO, color Blue Marble.
  - Wikimedia Commons (licencias libres, ver CREDITS.txt): banderas de la época y retratos.
  - flagcdn.com (dominio público): banderas actuales como respaldo.

Salida (Assets/StreamingAssets/Maps/world/):
  map.json, provinces.png, definition.csv, heightmap.png, heightmap.r16, colormap.jpg, citylights.png,
  provinces.json, countries.json, flags/<TAG>.png, leaders/<TAG>.png, CREDITS.txt

Uso:  python build_world_map.py            (descarga los datos en ./cache si faltan)
Requisitos: Python 3.9+, numpy, pillow.
"""
import array
import colorsys
import html
import json
import math
import random
import re
import time
import urllib.parse
import urllib.error
import urllib.request
import zlib
from collections import Counter, defaultdict
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageOps

import history_1936 as H

Image.MAX_IMAGE_PIXELS = None

ROOT = Path(__file__).resolve().parent
CACHE = ROOT / "cache"
OUT = ROOT.parents[1] / "Assets" / "StreamingAssets" / "Maps" / "world"

# ----------------------------------------------------------------------------- Parámetros
WIDTH = 4608                 # ancho del mapa en píxeles
LAT_TOP, LAT_BOTTOM = 84.0, -58.0   # recorte (sin la Antártida, como HOI4)
SEA_LEVEL = 0.4
MIN_PROVINCE_PX = 20         # provincias más pequeñas se fusionan con una vecina del mismo dueño
TARGET_PROVINCE_PX = 1700    # las provincias enormes se dividen en trozos de este tamaño
SPLIT_THRESHOLD = 2.3        # dividir si el tamaño supera TARGET * este factor
SEA_SPACING_OPEN = 190
SEA_SPACING_COAST = 95
COAST_BAND_PX = 28
MIN_SEA_COMPONENT_PX = 6000  # masas de agua cerradas más pequeñas son lagos
MIN_LAKE_PX = 30             # lagos más pequeños se rellenan con tierra
MICROSTATE_PX = 6            # países más pequeños se absorben en su vecino
MAX_RIVER_RANK = 8           # ríos exportados (scalerank de Natural Earth); el juego filtra según la calidad
BIG_CITY_POP = 1_500_000     # ciudades grandes que siempre llevan edificios
CITIES_PER_NATION = 3        # además, las N mayores de cada nación (si superan MIN_CITY_POP)
MIN_CITY_POP = 250_000
SEED = 1936

NE = "https://raw.githubusercontent.com/nvkelso/natural-earth-vector/master/geojson/"
SOURCES = {
    "ne_10m_admin_1_states_provinces_lakes.geojson": NE + "ne_10m_admin_1_states_provinces_lakes.geojson",
    "ne_10m_admin_0_countries_lakes.geojson": NE + "ne_10m_admin_0_countries_lakes.geojson",
    "ne_10m_populated_places_simple.geojson": NE + "ne_10m_populated_places_simple.geojson",
    "ne_10m_geography_marine_polys.geojson": NE + "ne_10m_geography_marine_polys.geojson",
    "ne_10m_rivers_lake_centerlines.geojson": NE + "ne_10m_rivers_lake_centerlines.geojson",
    "elevation.png": "https://eoimages.gsfc.nasa.gov/images/imagerecords/73000/73934/gebco_08_rev_elev_21600x10800.png",
    "bathymetry.png": "https://eoimages.gsfc.nasa.gov/images/imagerecords/73000/73963/gebco_08_rev_bath_21600x10800.png",
    # Julio: vegetación de verano sin nieve en las llanuras (como el mapa de HOI4).
    "bluemarble_july.jpg": "https://eoimages.gsfc.nasa.gov/images/imagerecords/73000/73751/world.topo.bathy.200407.3x5400x2700.jpg",
}
USER_AGENT = "GrandStrategyMapBuilder/1.0 (prototipo de juego; uso educativo)"

TERRAIN_NAMES = ["ocean", "lake", "plains", "forest", "hills", "mountains", "desert", "jungle", "marsh", "tundra", "arctic"]
T_OCEAN, T_LAKE, T_PLAINS, T_FOREST, T_HILLS, T_MOUNTAINS, T_DESERT, T_JUNGLE, T_MARSH, T_TUNDRA, T_ARCTIC = range(11)
TERRAIN_DENSITY = [0, 0, 1.0, 0.55, 0.6, 0.25, 0.1, 0.4, 0.3, 0.06, 0.01]


def log(msg):
    print(f"[{time.strftime('%H:%M:%S')}] {msg}", flush=True)


# ----------------------------------------------------------------------------- Proyección Miller
def miller_y(lat):
    lat = max(min(lat, 89.5), -89.5)
    return 1.25 * math.log(math.tan(math.pi / 4 + 0.4 * math.radians(lat)))


Y_TOP, Y_BOTTOM = miller_y(LAT_TOP), miller_y(LAT_BOTTOM)
HEIGHT = round(WIDTH * (Y_TOP - Y_BOTTOM) / (2 * math.pi))


def project(lon, lat):
    """lon/lat → píxel (fila 0 = norte)."""
    return (lon + 180.0) / 360.0 * WIDTH, (Y_TOP - miller_y(lat)) / (Y_TOP - Y_BOTTOM) * HEIGHT


def row_latitudes():
    y = Y_TOP - (np.arange(HEIGHT) + 0.5) / HEIGHT * (Y_TOP - Y_BOTTOM)
    return np.degrees(2.5 * np.arctan(np.exp(0.8 * y)) - 0.625 * math.pi)


ROW_LAT = row_latitudes()


def pixel_lonlat(pix):
    return (pix % WIDTH + 0.5) / WIDTH * 360.0 - 180.0, ROW_LAT[pix // WIDTH]


def lonlat_to_pixel(lon, lat):
    x, y = project(lon, lat)
    return min(WIDTH - 1, max(0, int(x))), min(HEIGHT - 1, max(0, int(y)))


# ----------------------------------------------------------------------------- Utilidades
def http_get(url, params=None):
    """GET con reintentos: Wikimedia limita la frecuencia (HTTP 429), así que se espera y se repite."""
    if params:
        url += "?" + urllib.parse.urlencode(params)
    delay = 3.0
    for attempt in range(7):
        req = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
        try:
            with urllib.request.urlopen(req, timeout=60) as r:
                data = r.read()
            if "wikimedia" in url or "wikipedia" in url:
                time.sleep(1.0)  # ritmo amable con la API
            return data
        except urllib.error.HTTPError as e:
            if e.code not in (429, 503) or attempt == 6:
                raise
            retry_after = e.headers.get("Retry-After")
            time.sleep(float(retry_after) if retry_after and retry_after.isdigit() else delay)
            delay *= 2


def download_sources():
    CACHE.mkdir(parents=True, exist_ok=True)
    for name, url in SOURCES.items():
        path = CACHE / name
        if not path.exists():
            log(f"Descargando {name}...")
            path.write_bytes(http_get(url))


def load_geojson(name):
    with open(CACHE / name, encoding="utf-8") as f:
        return json.load(f)["features"]


def polygons_of(geometry):
    if geometry is None:
        return []
    if geometry["type"] == "Polygon":
        return [geometry["coordinates"]]
    if geometry["type"] == "MultiPolygon":
        return geometry["coordinates"]
    return []


def capitalize(s):
    return s[:1].upper() + s[1:] if s else s


def resample_equirect(img, mode):
    """Reproyecta una imagen equirectangular global a Miller (WIDTH x HEIGHT)."""
    img = img.convert(mode).resize((WIDTH, max(HEIGHT, img.height * WIDTH // img.width)), Image.BOX)
    arr = np.asarray(img)
    rows = np.clip(((90.0 - ROW_LAT) / 180.0 * arr.shape[0]).astype(int), 0, arr.shape[0] - 1)
    return arr[rows]


def neighbor_pairs(ids):
    """Pares (a, b) de ids distintos adyacentes (incluido el cruce del antimeridiano) y su frontera."""
    codes = []
    wrapped = np.concatenate([ids, ids[:, :1]], axis=1)
    for a, b in ((wrapped[:, :-1], wrapped[:, 1:]), (ids[:-1, :], ids[1:, :])):
        m = (a != b) & (a > 0) & (b > 0)
        lo = np.minimum(a[m], b[m]).astype(np.int64)
        hi = np.maximum(a[m], b[m]).astype(np.int64)
        codes.append(lo * 1_000_000 + hi)
    codes = np.concatenate(codes)
    uniq, counts = np.unique(codes, return_counts=True)
    return [(int(c // 1_000_000), int(c % 1_000_000), int(n)) for c, n in zip(uniq, counts)]


def label_components(mask):
    """Componentes 4-conexas (con continuidad este-oeste). Devuelve (labels int32, tamaños)."""
    h, w = mask.shape
    idx = np.flatnonzero(mask.ravel()).tolist()
    flat = mask.ravel().astype(np.uint8).tobytes()
    labels = array.array("i", [0]) * (h * w)
    sizes = [0]
    current = 0
    total = h * w
    for start in idx:
        if labels[start]:
            continue
        current += 1
        labels[start] = current
        stack = [start]
        size = 0
        while stack:
            p = stack.pop()
            size += 1
            x = p % w
            for q in (p - 1 if x > 0 else p + w - 1, p + 1 if x < w - 1 else p - w + 1, p - w, p + w):
                if 0 <= q < total and flat[q] and not labels[q]:
                    labels[q] = current
                    stack.append(q)
        sizes.append(size)
    return np.frombuffer(labels, dtype=np.int32).reshape(h, w).copy(), sizes


def box_blur(arr, radius):
    """Desenfoque de caja separable (con continuidad este-oeste)."""
    out = arr.astype(np.float32)
    k = 2 * radius + 1
    for axis in (0, 1):
        pad = [(0, 0), (0, 0)]
        pad[axis] = (radius, radius)
        padded = np.pad(out, pad, mode="wrap" if axis == 1 else "edge")
        c = np.cumsum(padded, axis=axis, dtype=np.float64)
        c = np.insert(c, 0, 0, axis=axis)
        hi = np.take(c, np.arange(k, c.shape[axis]), axis=axis)
        lo = np.take(c, np.arange(0, c.shape[axis] - k), axis=axis)
        out = ((hi - lo) / k).astype(np.float32)
    return out


def douglas_peucker(points, tolerance):
    """Simplifica una polilínea (iterativo)."""
    if len(points) < 3:
        return points
    keep = [False] * len(points)
    keep[0] = keep[-1] = True
    stack = [(0, len(points) - 1)]
    while stack:
        a, b = stack.pop()
        ax, ay = points[a]
        bx, by = points[b]
        dx, dy = bx - ax, by - ay
        norm = math.hypot(dx, dy) or 1e-9
        best, idx = -1.0, -1
        for i in range(a + 1, b):
            px, py = points[i]
            d = abs(dy * (px - ax) - dx * (py - ay)) / norm
            if d > best:
                best, idx = d, i
        if best > tolerance:
            keep[idx] = True
            stack += [(a, idx), (idx, b)]
    return [p for p, k in zip(points, keep) if k]


def pixels_by_id(ids, count):
    sizes = np.bincount(ids.ravel(), minlength=count)
    order = np.argsort(ids.ravel(), kind="stable")
    starts = np.concatenate([[0], np.cumsum(sizes)])
    return sizes, order, starts


# ----------------------------------------------------------------------------- Wikimedia
CREDITS = []


def commons_thumb(file_title, width):
    """URL de miniatura y licencia de un archivo de Commons (None si no existe allí)."""
    data = json.loads(http_get("https://commons.wikimedia.org/w/api.php", {
        "action": "query", "titles": "File:" + file_title, "prop": "imageinfo",
        "iiprop": "url|extmetadata", "iiurlwidth": width, "format": "json"}))
    for page in data.get("query", {}).get("pages", {}).values():
        if "missing" in page or "imageinfo" not in page:
            return None
        ii = page["imageinfo"][0]
        meta = ii.get("extmetadata", {})
        artist = re.sub("<[^>]+>", "", html.unescape(meta.get("Artist", {}).get("value", ""))).strip()
        license_name = meta.get("LicenseShortName", {}).get("value", "desconocida")
        return ii.get("thumburl") or ii.get("url"), license_name, artist, ii.get("descriptionurl", "")
    return None


def wiki_page_image(title):
    data = json.loads(http_get("https://en.wikipedia.org/w/api.php", {
        "action": "query", "titles": title, "prop": "pageimages", "piprop": "name", "redirects": 1, "format": "json"}))
    for page in data.get("query", {}).get("pages", {}).values():
        return page.get("pageimage")
    return None


def fetch_commons_image(cache_path, candidates, width, what):
    """Descarga la primera candidata existente en Commons; guarda la atribución."""
    meta_path = cache_path.with_suffix(".json")
    if cache_path.exists() and meta_path.exists():
        CREDITS.append(json.loads(meta_path.read_text(encoding="utf-8")))
        return True
    for title in candidates:
        if not title:
            continue
        try:
            info = commons_thumb(title, width)
            time.sleep(0.15)
            if info is None:
                continue
            url, lic, artist, page = info
            cache_path.parent.mkdir(parents=True, exist_ok=True)
            cache_path.write_bytes(http_get(url))
            credit = {"what": what, "file": title, "license": lic, "author": artist, "source": page}
            meta_path.write_text(json.dumps(credit, ensure_ascii=False), encoding="utf-8")
            CREDITS.append(credit)
            return True
        except Exception as e:  # red caída, archivo raro... se prueba la siguiente
            log(f"    aviso: {what}: {title}: {e}")
    return False


def make_portrait(src, dst):
    """Recorta a 3:4 (centrado arriba, donde suele estar la cara) con tono de época."""
    im = Image.open(src).convert("RGB")
    w, h = im.size
    tw, th = (w, int(w * 4 / 3)) if h >= w * 4 / 3 else (int(h * 3 / 4), h)
    left = (w - tw) // 2
    top = int(max(0, min(h - th, (h - th) * 0.15)))
    im = im.crop((left, top, left + tw, top + th)).resize((168, 224), Image.LANCZOS)
    gray = ImageOps.grayscale(im)
    sepia = ImageOps.colorize(gray, black=(28, 22, 18), white=(236, 222, 196))
    Image.blend(im, sepia, 0.55).save(dst)


# ----------------------------------------------------------------------------- Principal
def main():
    t0 = time.time()
    random.seed(SEED)
    download_sources()
    log(f"Mapa {WIDTH}x{HEIGHT} (Miller, lat {LAT_TOP}..{LAT_BOTTOM}), situación política de 1936")

    admin0 = {f["properties"]["ADM0_A3"]: f["properties"] for f in load_geojson("ne_10m_admin_0_countries_lakes.geojson")}
    homeland = {}
    for p in admin0.values():
        if p.get("HOMEPART") == 1:
            homeland.setdefault(p["SOV_A3"], p["ADM0_A3"])

    def modern_key(adm0):
        """País actual al que pertenece una entidad (agrupa dependencias); se usa para repartir población."""
        p = admin0.get(adm0)
        if p is None:
            return adm0
        if p["TYPE"] in ("Dependency", "Lease") or (p["TYPE"] in ("Country", "Disputed") and p.get("HOMEPART") != 1):
            return homeland.get(p["SOV_A3"], adm0)
        return adm0

    # ---------------------------------------------------------------- 1. Rasterizar provincias
    log("Rasterizando provincias (admin-1)...")
    admin1 = [f for f in load_geojson("ne_10m_admin_1_states_provinces_lakes.geojson")
              if f["properties"]["adm0_a3"] != "ATA"]
    admin1.sort(key=lambda f: -(f["properties"].get("area_sqkm") or 0))
    # Datos por provincia "cruda" (índice = id - 1)
    raw_name, raw_adm0, raw_iso, raw_label = [], [], [], []
    img = Image.new("I", (WIDTH, HEIGHT), 0)
    draw = ImageDraw.Draw(img)
    for i, f in enumerate(admin1):
        p = f["properties"]
        raw_name.append(p.get("name_es") or p.get("name") or p.get("gn_name") or f"Provincia {i + 1}")
        raw_adm0.append(p["adm0_a3"])
        raw_iso.append(p.get("iso_3166_2") or "")
        raw_label.append((p.get("longitude"), p.get("latitude")))
        for poly in polygons_of(f["geometry"]):
            rings = [[project(lon, lat) for lon, lat in ring] for ring in poly]
            if len(rings[0]) >= 3:
                draw.polygon(rings[0], fill=i + 1)
            for hole in rings[1:]:
                if len(hole) >= 3:
                    draw.polygon(hole, fill=0)
    ids = np.array(img, dtype=np.int32)
    del img, draw

    present = np.bincount(ids.ravel(), minlength=len(raw_name) + 1)
    for i, (lon, lat) in enumerate(raw_label):
        if present[i + 1] == 0 and lon is not None and lat is not None and LAT_BOTTOM < lat < LAT_TOP:
            x, y = lonlat_to_pixel(lon, lat)
            if ids[y, x] == 0:
                ids[y, x] = i + 1

    # ---------------------------------------------------------------- 2. Agua: océano, mares, lagos
    log("Clasificando masas de agua...")
    water_labels, water_sizes = label_components(ids == 0)
    ocean_label = int(np.argmax(water_sizes))
    cx, cy = lonlat_to_pixel(50.5, 42.0)  # mar Caspio
    caspian = int(water_labels[cy, cx])
    tiny_lakes = [l for l, s in enumerate(water_sizes) if l > 0 and s < MIN_LAKE_PX]
    if tiny_lakes:
        tiny_mask = np.isin(water_labels, tiny_lakes)
        for _ in range(12):
            if not tiny_mask.any():
                break
            for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
                shifted = np.roll(ids, (dy, dx), axis=(0, 1))
                fill = tiny_mask & (shifted > 0)
                ids[fill] = shifted[fill]
                tiny_mask &= ~fill
        water_labels[np.isin(water_labels, tiny_lakes)] = 0
    lake_labels = {l for l, s in enumerate(water_sizes)
                   if l > 0 and l != ocean_label and s >= MIN_LAKE_PX and (s < MIN_SEA_COMPONENT_PX or l == caspian)}

    # ---------------------------------------------------------------- 3. Dueños en 1936
    log("Asignando dueños de 1936...")
    unknown = set()

    def base_owner(adm0, iso):
        if iso in H.OWNER_BY_ISO:
            return H.OWNER_BY_ISO[iso]
        if adm0 in H.OWNER_BY_ADM0:
            return H.OWNER_BY_ADM0[adm0]
        mk = modern_key(adm0)
        if mk in H.OWNER_BY_ADM0:
            return H.OWNER_BY_ADM0[mk]
        unknown.add(adm0)
        return mk

    raw_owner = [base_owner(a, i) for a, i in zip(raw_adm0, raw_iso)]
    sizes, order, starts = pixels_by_id(ids, len(raw_name) + 1)
    flat_ids = ids.ravel()
    for adm0, iso, cond, owner in H.PIXEL_RULES:
        for r in range(len(sizes) - 1):
            if r >= len(raw_adm0) or raw_adm0[r] != adm0 or (iso and raw_iso[r] != iso):
                continue
            pix = order[starts[r + 1]:starts[r + 2]]
            pix = pix[flat_ids[pix] == r + 1]  # puede haber cambiado por una regla anterior
            if len(pix) == 0:
                continue
            lon, lat = pixel_lonlat(pix)
            mask = cond(lon, lat)
            if not mask.any():
                continue
            if mask.all():
                raw_owner[r] = owner
                continue
            raw_name.append(raw_name[r])
            raw_adm0.append(raw_adm0[r])
            raw_iso.append(raw_iso[r])
            raw_owner.append(owner)
            flat_ids[pix[mask]] = len(raw_name)
    ids = flat_ids.reshape(HEIGHT, WIDTH)
    if unknown:
        log(f"  sin regla de 1936 (se usa el país actual): {sorted(unknown)}")

    prov_owner = [None] + raw_owner
    prov_name = [None] + raw_name
    prov_adm0 = [None] + raw_adm0

    def remap(mapping):
        lut = np.arange(len(prov_owner), dtype=np.int32)
        for a, b in mapping.items():
            lut[a] = b
        for a in list(mapping):
            b = lut[a]
            while lut[b] != b:
                b = lut[b]
            lut[a] = b
        return lut[ids]

    # ---------------------------------------------------------------- 4. Normalizar provincias
    log("Fusionando provincias diminutas...")
    for _ in range(4):
        sizes = np.bincount(ids.ravel(), minlength=len(prov_owner))
        best = {}
        for a, b, n in neighbor_pairs(ids):
            for s, o in ((a, b), (b, a)):
                if prov_owner[s] == prov_owner[o] and (s not in best or best[s][1] < n):
                    best[s] = (o, n)
        mapping = {}
        for pid in np.argsort(sizes):
            pid = int(pid)
            if pid == 0 or sizes[pid] == 0 or sizes[pid] >= MIN_PROVINCE_PX or pid not in best:
                continue
            target = best[pid][0]
            if target in mapping or mapping.get(target) == pid:
                continue
            mapping[pid] = target
        if not mapping:
            break
        ids = remap(mapping)

    log("Absorbiendo microestados...")
    sizes = np.bincount(ids.ravel(), minlength=len(prov_owner))
    owner_px = Counter()
    for pid in np.flatnonzero(sizes):
        if pid:
            owner_px[prov_owner[pid]] += int(sizes[pid])
    border = defaultdict(Counter)
    for a, b, n in neighbor_pairs(ids):
        oa, ob = prov_owner[a], prov_owner[b]
        if oa != ob:
            border[oa][ob] += n
            border[ob][oa] += n
    for tag, px in owner_px.items():
        if px < MICROSTATE_PX and border[tag] and tag not in H.N:
            heir = border[tag].most_common(1)[0][0]
            log(f"  {tag} → {heir}")
            for pid in range(1, len(prov_owner)):
                if prov_owner[pid] == tag:
                    prov_owner[pid] = heir

    log("Dividiendo provincias enormes...")
    sizes, order, starts = pixels_by_id(ids, len(prov_owner))
    flat_ids = ids.ravel().copy()
    split_count = 0
    for pid in range(1, len(sizes)):
        n = int(sizes[pid])
        if n <= TARGET_PROVINCE_PX * SPLIT_THRESHOLD:
            continue
        k = max(2, round(n / TARGET_PROVINCE_PX))
        pix = order[starts[pid]:starts[pid + 1]]
        ys, xs = pix // WIDTH, pix % WIDTH
        pts = np.stack([xs, ys], axis=1).astype(np.float32)
        centers = [pts[random.randrange(n)]]
        d2 = ((pts - centers[0]) ** 2).sum(1)
        for _ in range(1, k):
            centers.append(pts[int(np.argmax(d2))])
            d2 = np.minimum(d2, ((pts - centers[-1]) ** 2).sum(1))
        centers = np.array(centers)
        for _ in range(6):
            assign = np.argmin(((pts[:, None, :] - centers[None, :, :]) ** 2).sum(2), axis=1)
            for c in range(k):
                sel = pts[assign == c]
                if len(sel):
                    centers[c] = sel.mean(0)
        seeds = [int(pix[int(np.argmin(((pts - c) ** 2).sum(1)))]) for c in centers]
        new_ids = [pid] + [len(prov_owner) + j for j in range(k - 1)]
        for _ in range(1, k):
            prov_owner.append(prov_owner[pid])
            prov_name.append(None)
            prov_adm0.append(prov_adm0[pid])
        part = {int(p): -1 for p in pix}
        frontier = []
        for j, s in enumerate(seeds):
            if part[s] == -1:
                part[s] = j
                frontier.append(s)
        head = 0
        while head < len(frontier):
            p = frontier[head]
            head += 1
            j = part[p]
            x = p % WIDTH
            for q in (p - 1 if x > 0 else -1, p + 1 if x < WIDTH - 1 else -1, p - WIDTH, p + WIDTH):
                if q in part and part[q] == -1:
                    part[q] = j
                    frontier.append(q)
        unreached = [p for p, j in part.items() if j == -1]
        if unreached:
            up = np.array(unreached)
            upts = np.stack([up % WIDTH, up // WIDTH], axis=1).astype(np.float32)
            near = np.argmin(((upts[:, None, :] - centers[None, :, :]) ** 2).sum(2), axis=1)
            for p, j in zip(unreached, near):
                part[p] = int(j)
        base = prov_name[pid]
        cxm, cym = pts.mean(0)
        dirs = []
        for j in range(k):
            sel = np.array([p for p, jj in part.items() if jj == j])
            px_, py_ = (sel % WIDTH).mean() - cxm, (sel // WIDTH).mean() - cym
            if math.hypot(px_, py_) < math.sqrt(n) * 0.12:
                dirs.append("Centro")
            else:
                ang = (math.degrees(math.atan2(-py_, px_)) + 360) % 360
                dirs.append(["Este", "Noreste", "Norte", "Noroeste", "Oeste", "Suroeste", "Sur", "Sureste"][int((ang + 22.5) // 45) % 8])
        counts = Counter(dirs)
        seen = Counter()
        for j in range(k):
            d = dirs[j]
            seen[d] += 1
            prov_name[new_ids[j]] = f"{base} {d if counts[d] == 1 else f'{d} {seen[d]}'}"
        for p, j in part.items():
            flat_ids[p] = new_ids[j]
        split_count += 1
    ids = flat_ids.reshape(HEIGHT, WIDTH)
    log(f"  {split_count} provincias divididas")

    # ---------------------------------------------------------------- 5. Zonas marítimas
    log("Creando zonas marítimas...")
    land = ids > 0
    coast = land.copy()
    for _ in range(COAST_BAND_PX):
        coast = coast | np.roll(coast, 1, 0) | np.roll(coast, -1, 0) | np.roll(coast, 1, 1) | np.roll(coast, -1, 1)
    coast &= ~land
    water = ~land
    wl = water_labels
    seeds = []
    has_seed = set()
    rng = random.Random(SEED)
    for spacing, band in ((SEA_SPACING_OPEN, None), (SEA_SPACING_COAST, coast)):
        for gy in range(0, HEIGHT, spacing):
            for gx in range(0, WIDTH, spacing):
                x = min(WIDTH - 1, gx + rng.randrange(spacing))
                y = min(HEIGHT - 1, gy + rng.randrange(spacing))
                if not water[y, x] or (band is not None and not band[y, x]):
                    continue
                lbl = int(wl[y, x])
                if lbl == 0 or (lbl in lake_labels and water_sizes[lbl] < 2500 and lbl in has_seed):
                    continue
                seeds.append((y * WIDTH + x, lbl in lake_labels))
                has_seed.add(lbl)
    for lbl, size in enumerate(water_sizes):
        if lbl and size >= MIN_LAKE_PX and lbl not in has_seed:
            first = int(np.flatnonzero(wl.ravel() == lbl)[0])
            seeds.append((first, lbl in lake_labels))
            has_seed.add(lbl)

    noise = np.random.default_rng(SEED).random((HEIGHT // 24 + 2, WIDTH // 24 + 2)).astype(np.float32)
    noise = np.asarray(Image.fromarray(noise).resize((WIDTH, HEIGHT), Image.BICUBIC))
    cost = (1 + np.clip(noise, 0, 0.999) * 4).astype(np.int8).ravel().tolist()
    wflat = wl.ravel().tolist()
    sea_ids = array.array("i", [0]) * (WIDTH * HEIGHT)
    dist = array.array("i", [1 << 30]) * (WIDTH * HEIGHT)
    first_sea_id = len(prov_owner)
    sea_is_lake = []
    buckets = [[] for _ in range(8)]
    for j, (p, is_lake) in enumerate(seeds):
        sea_is_lake.append(is_lake)
        if sea_ids[p] == 0:
            sea_ids[p] = first_sea_id + j
            dist[p] = 0
            buckets[0].append(p)
    pending = sum(len(b) for b in buckets)
    current = 0
    total = WIDTH * HEIGHT
    while pending:
        bucket = buckets[current & 7]
        for p in bucket:
            pending -= 1
            if dist[p] != current:
                continue
            sid = sea_ids[p]
            lbl = wflat[p]
            x = p % WIDTH
            for q in (p - 1 if x > 0 else p + WIDTH - 1, p + 1 if x < WIDTH - 1 else p - WIDTH + 1, p - WIDTH, p + WIDTH):
                if 0 <= q < total and wflat[q] == lbl and lbl:
                    nd = current + cost[q]
                    if nd < dist[q]:
                        dist[q] = nd
                        sea_ids[q] = sid
                        buckets[nd & 7].append(q)
                        pending += 1
        bucket.clear()
        current += 1
    sea_arr = np.frombuffer(sea_ids, dtype=np.int32).reshape(HEIGHT, WIDTH)
    ids = np.where(land, ids, sea_arr)
    for _ in seeds:
        prov_owner.append(None)
        prov_name.append(None)
        prov_adm0.append(None)

    # ---------------------------------------------------------------- 6. Compactar ids
    sizes = np.bincount(ids.ravel(), minlength=len(prov_owner))
    used = [pid for pid in range(1, len(prov_owner)) if sizes[pid] > 0]
    lut = np.zeros(len(prov_owner), dtype=np.int32)
    for new, old in enumerate(used):
        lut[old] = new + 1
    ids = lut[ids]
    if (ids == 0).any():
        log(f"  AVISO: {(ids == 0).sum()} píxeles sin provincia (se asignan a la 1)")
        ids[ids == 0] = 1
    P = len(used)
    is_sea_old = lambda old: old >= first_sea_id
    sea_type = {}
    owner = [None] * (P + 1)
    names = [None] * (P + 1)
    adm0_of = [None] * (P + 1)
    for new, old in enumerate(used):
        pid = new + 1
        owner[pid] = prov_owner[old]
        names[pid] = prov_name[old]
        adm0_of[pid] = prov_adm0[old]
        if is_sea_old(old):
            sea_type[pid] = "lake" if sea_is_lake[old - first_sea_id] else "sea"
    land = ~np.isin(ids, list(sea_type.keys()))
    log(f"  {P} provincias finales ({P - len(sea_type)} terrestres, {len(sea_type)} marítimas)")

    # ---------------------------------------------------------------- 7. Relieve y color
    log("Reproyectando relieve y colores...")
    elev = resample_equirect(Image.open(CACHE / "elevation.png"), "L").astype(np.float32)
    bath = resample_equirect(Image.open(CACHE / "bathymetry.png"), "L").astype(np.float32)
    # La elevación de origen va en escalones de 25 m: se suaviza para que las llanuras no salgan rugosas.
    elev = box_blur(elev, 2)
    e_norm = np.clip(elev / 255.0, 0, 1) ** 0.85
    land_h = SEA_LEVEL + 0.004 + (1 - SEA_LEVEL - 0.004) * e_norm
    depth = np.clip(1 - bath / 255.0, 0, 1)
    sea_h = SEA_LEVEL * (1 - 0.95 * np.sqrt(depth)) - 0.003
    heights = np.where(land, land_h, np.minimum(sea_h, SEA_LEVEL - 0.003))

    color = resample_equirect(Image.open(CACHE / "bluemarble_july.jpg"), "RGB").astype(np.float32) / 255.0
    color = color ** 0.8
    gray = color.mean(axis=2, keepdims=True)
    color = gray + (color - gray) * 0.85
    color *= np.array([1.04, 1.0, 0.93], dtype=np.float32)
    color = np.clip(color * 255, 0, 255).astype(np.uint8)

    # ---------------------------------------------------------------- 8. Terreno
    log("Clasificando terreno...")
    lat = ROW_LAT[:, None].repeat(WIDTH, 1)
    meters = elev * 25.0
    c = color.astype(np.float32) / 255.0
    r, g, b = c[..., 0], c[..., 1], c[..., 2]
    bright = c.mean(2)
    terr = np.full(ids.shape, T_PLAINS, dtype=np.int64)
    terr[(g >= r * 0.97) & (bright < 0.36)] = T_FOREST
    terr[(g >= r * 0.97) & (bright < 0.36) & (np.abs(lat) < 18)] = T_JUNGLE
    terr[(r > g * 1.02) & (r > b * 1.25) & (bright > 0.45)] = T_DESERT
    terr[(np.abs(lat) > 58) & (terr != T_FOREST)] = T_TUNDRA
    terr[meters > 700] = T_HILLS
    terr[meters > 1800] = T_MOUNTAINS
    terr[((bright > 0.78) & ((r - b) < 0.06)) | (lat > 74)] = T_ARCTIC
    votes = np.bincount((ids.astype(np.int64) * 11 + terr).ravel(), minlength=(P + 1) * 11).reshape(P + 1, 11)
    terrain = votes.argmax(1)
    del lat, meters, c, r, g, b, bright, terr

    # ---------------------------------------------------------------- 9. Población, PIB, desarrollo
    log("Repartiendo población (1936) y PIB...")
    places = load_geojson("ne_10m_populated_places_simple.geojson")
    city_pop = np.zeros(P + 1)
    city_list = []
    city_info = []
    for f in places:
        p = f["properties"]
        la, lo = p["latitude"], p["longitude"]
        if not (LAT_BOTTOM < la < LAT_TOP):
            continue
        x, y = lonlat_to_pixel(lo, la)
        if not land[y, x]:
            continue
        pop = max(p.get("pop_max") or 0, 0)
        city_pop[ids[y, x]] += pop
        city_list.append((x, y, pop))
        city_info.append((p.get("name") or p.get("nameascii") or "", x, y, pop, p["featurecla"], p.get("adm0_a3")))
    sizes = np.bincount(ids.ravel(), minlength=P + 1)

    # Reparto dentro de cada país ACTUAL (así se aprovechan sus datos de población).
    by_modern = defaultdict(list)
    for pid in range(1, P + 1):
        if pid not in sea_type:
            by_modern[modern_key(adm0_of[pid])].append(pid)
    population = np.zeros(P + 1)
    pop_factor = {}
    for mk, pids in by_modern.items():
        info = admin0.get(mk, {})
        cpop = max(info.get("POP_EST") or 0, 0) or sum(sizes[p] for p in pids) * 50
        if mk in H.POP_1936_MILLIONS and cpop > 0:
            factor = H.POP_1936_MILLIONS[mk] * 1e6 / cpop
        else:
            factor = H.REGION_POP_FACTOR.get(info.get("SUBREGION"), H.REGION_POP_FACTOR.get(info.get("REGION_UN"), 0.3))
            if info.get("REGION_UN") == "Americas":
                factor = 0.37 if info.get("SUBREGION") == "Northern America" else 0.22
        pop_factor[mk] = factor
        area_w = np.array([sizes[p] * TERRAIN_DENSITY[terrain[p]] + 0.01 for p in pids])
        city_w = np.array([city_pop[p] for p in pids])
        w = 0.35 * area_w / area_w.sum() + (0.65 * city_w / city_w.sum() if city_w.sum() > 0 else 0.65 * area_w / area_w.sum())
        for p, wi in zip(pids, w):
            population[p] = max(300, cpop * factor * wi)

    capital_pid = {}
    for tag, data in H.N.items():
        if data["capital"]:
            x, y = lonlat_to_pixel(*data["capital"])
            capital_pid[tag] = int(ids[y, x])

    development = np.ones(P + 1, dtype=int)
    gdp = np.zeros(P + 1)
    for pid in range(1, P + 1):
        if pid in sea_type:
            continue
        tag = owner[pid]
        data = H.N.get(tag)
        info = admin0.get(adm0_of[pid], {})
        cont = info.get("CONTINENT", "")
        region = "Americas" if "America" in cont else cont
        if data and adm0_of[pid] in data["core"]:
            pc = data["gdppc"]
        elif data and not data["core"] and tag not in ("PRC",):
            pc = data["gdppc"]            # naciones sin núcleo moderno propio (Manchukuo, Tíbet...)
        else:
            pc = H.REGION_GDPPC.get(region, 900)
        gdp[pid] = population[pid] * pc / 1e6
        d = 1 + 9 * (math.log10(max(pc, 300)) - 2.48) / (3.9 - 2.48)
        d += (1 if city_pop[pid] > 1e6 else 0) + (1.5 if capital_pid.get(tag) == pid else 0)
        development[pid] = int(min(10, max(1, round(d))))

    # ---------------------------------------------------------------- 10. Nombres de mares
    log("Nombrando mares...")
    marine = load_geojson("ne_10m_geography_marine_polys.geojson")

    def bbox_area(f):
        xs, ys = [], []
        for poly in polygons_of(f["geometry"]):
            for lon, la in poly[0]:
                xs.append(lon)
                ys.append(la)
        return (max(xs) - min(xs)) * (max(ys) - min(ys)) if xs else 0

    marine.sort(key=lambda f: -bbox_area(f))
    mimg = Image.new("I", (WIDTH, HEIGHT), 0)
    mdraw = ImageDraw.Draw(mimg)
    marine_names = [None]
    for i, f in enumerate(marine):
        p = f["properties"]
        marine_names.append(capitalize(p.get("name_es") or p.get("name") or ""))
        for poly in polygons_of(f["geometry"]):
            ring = [project(lon, max(min(la, 89), -89)) for lon, la in poly[0]]
            if len(ring) >= 3:
                mdraw.polygon(ring, fill=i + 1)
    mlabels = np.array(mimg, dtype=np.int64)
    del mimg, mdraw
    sea_mask = ~land
    M = len(marine_names) + 1
    mv = np.bincount(ids[sea_mask].astype(np.int64) * M + mlabels[sea_mask], minlength=(P + 1) * M).reshape(P + 1, M)
    mv[:, 0] = 0
    name_counter = Counter()
    for pid, typ in sea_type.items():
        label = int(mv[pid].argmax()) if mv[pid].max() > 0 else 0
        names[pid] = marine_names[label] if label else ("Lago" if typ == "lake" else "Aguas internacionales")
        name_counter[names[pid]] += 1
        terrain[pid] = T_LAKE if typ == "lake" else T_OCEAN
    seen = Counter()
    for pid in sea_type:
        base = names[pid]
        if name_counter[base] > 1:
            seen[base] += 1
            names[pid] = f"{base} {seen[base]}"

    # ---------------------------------------------------------------- 11. Luces de ciudades (noche)
    log("Generando luces nocturnas...")
    lights = np.zeros((HEIGHT, WIDTH), dtype=np.float32)
    for x, y, pop in city_list:
        if pop < 50000:
            continue
        dev = development[ids[y, x]] / 10.0
        # Núcleos pequeños (1936: menos electrificación que hoy); el halo lo añade el shader.
        radius = 0.55 + math.sqrt(pop / 1e6) * 1.1
        amp = min(1.0, 0.2 + 0.45 * math.log10(pop / 5e4 + 1)) * (0.3 + 0.7 * dev)
        rr = int(math.ceil(radius * 2.5))
        y0, y1 = max(0, y - rr), min(HEIGHT, y + rr + 1)
        xs = (np.arange(x - rr, x + rr + 1)) % WIDTH
        yy, xx = np.mgrid[y0 - y:y1 - y, -rr:rr + 1]
        blob = amp * np.exp(-(xx ** 2 + yy ** 2) / (2 * radius ** 2))
        lights[y0:y1, xs] = np.maximum(lights[y0:y1, xs], blob)
    lights *= land
    lights_u8 = (np.clip(lights, 0, 1) * 255).astype(np.uint8)

    # ---------------------------------------------------------------- 12. Escribir mapa
    log("Escribiendo archivos del mapa...")
    OUT.mkdir(parents=True, exist_ok=True)
    rng = random.Random(SEED)
    used_colors = set()
    prov_colors = np.zeros((P + 1, 3), dtype=np.uint8)
    for pid in range(1, P + 1):
        while True:
            col = (rng.randrange(1, 256), rng.randrange(1, 256), rng.randrange(1, 256))
            if col not in used_colors:
                used_colors.add(col)
                prov_colors[pid] = col
                break
    Image.fromarray(prov_colors[ids], "RGB").save(OUT / "provinces.png", optimize=True)
    Image.fromarray((np.clip(heights, 0, 1) * 255).astype(np.uint8), "L").save(OUT / "heightmap.png", optimize=True)
    (np.flipud(np.clip(heights, 0, 1)) * 65535).astype("<u2").tofile(OUT / "heightmap.r16")
    Image.fromarray(color, "RGB").save(OUT / "colormap.jpg", quality=90)
    Image.fromarray(lights_u8, "L").save(OUT / "citylights.png", optimize=True)

    coastal = set()
    pairs = neighbor_pairs(ids)
    for a, b_, _n in pairs:
        if (a in sea_type) != (b_ in sea_type):
            land_p, sea_p = (b_, a) if a in sea_type else (a, b_)
            if sea_type[sea_p] == "sea":
                coastal.add(land_p)

    lines = ["0;0;0;0;land;false;unknown;0"]
    for pid in range(1, P + 1):
        typ = sea_type.get(pid, "land")
        cr, cg, cb = prov_colors[pid]
        lines.append(f"{pid};{cr};{cg};{cb};{typ};{'true' if pid in coastal else 'false'};{TERRAIN_NAMES[terrain[pid]]};0")
    (OUT / "definition.csv").write_text("\n".join(lines) + "\n", encoding="utf-8")

    (OUT / "provinces.json").write_text(json.dumps({"provinces": [
        {"id": pid, "name": names[pid] or f"Provincia {pid}", "population": int(population[pid]),
         "development": int(development[pid]), "gdp": round(float(gdp[pid]), 2)}
        for pid in range(1, P + 1)]}, ensure_ascii=False, indent=1), encoding="utf-8")

    # ---------------------------------------------------------------- 12b. Ríos y ciudades (decoración)
    log("Exportando ríos y ciudades principales...")
    rivers_out = []
    for f in load_geojson("ne_10m_rivers_lake_centerlines.geojson"):
        pr = f["properties"]
        if pr.get("featurecla") != "River" or (pr.get("scalerank") or 99) > MAX_RIVER_RANK:
            continue
        g = f["geometry"]
        lines = g["coordinates"] if g["type"] == "MultiLineString" else [g["coordinates"]]
        rank = pr["scalerank"]
        width = 1.0 if rank <= 2 else 0.75 if rank <= 4 else 0.5 if rank <= 6 else 0.35
        for line in lines:
            pts = [project(lo, la) for lo, la in line if LAT_BOTTOM < la < LAT_TOP]
            # partir en el antimeridiano
            segs, cur = [], []
            for pt in pts:
                if cur and abs(pt[0] - cur[-1][0]) > WIDTH / 2:
                    segs.append(cur)
                    cur = []
                cur.append(pt)
            segs.append(cur)
            for seg in segs:
                seg = douglas_peucker(seg, 0.6)
                if len(seg) < 2:
                    continue
                flat = []
                for x, y in seg:
                    flat += [round(x / WIDTH, 6), round(1.0 - y / HEIGHT, 6)]
                rivers_out.append({"name": pr.get("name") or "", "width": width, "rank": int(rank), "points": flat})
    (OUT / "rivers.json").write_text(json.dumps({"rivers": rivers_out}, ensure_ascii=False), encoding="utf-8")

    chosen = {}
    by_nation = defaultdict(list)
    capital_cities = {}
    # Ciudades que no existían (o eran aldeas) en 1936.
    modern_only = {"Shenzhen", "Dongguan", "Foshan", "Brasília", "Brasilia", "Islamabad", "Abuja", "Dodoma", "Gaborone"}
    for name, x, y, pop, cla, a3 in city_info:
        if name in modern_only:
            continue
        pid = int(ids[y, x])
        tag = owner[pid]
        if tag is None:
            continue
        pop36 = pop * pop_factor.get(modern_key(adm0_of[pid]) if adm0_of[pid] else a3, 0.3)
        by_nation[tag].append((pop36, name, x, y))
        if capital_pid.get(tag) == pid and (tag not in capital_cities or capital_cities[tag][0] < pop36):
            capital_cities[tag] = (pop36, name, x, y)
    def add_city(pop36, name, x, y, tag, capital):
        key = (x // 6, y // 6)
        for (kx, ky), c in list(chosen.items()):
            if abs(kx - key[0]) <= 1 and abs(ky - key[1]) <= 1:  # ya hay una ciudad muy cerca
                if capital and not c["capital"]:
                    del chosen[(kx, ky)]
                    break
                return
        chosen[key] = {"name": name, "u": round((x + 0.5) / WIDTH, 6), "v": round(1.0 - (y + 0.5) / HEIGHT, 6),
                       "population": int(pop36), "capital": capital, "owner": tag}
    for tag, c in capital_cities.items():
        add_city(*c, tag, True)
    for tag, lst in by_nation.items():
        lst.sort(reverse=True)
        for i, (pop36, name, x, y) in enumerate(lst):
            if pop36 >= BIG_CITY_POP * 0.4 or (i < CITIES_PER_NATION and pop36 >= MIN_CITY_POP * 0.4):
                add_city(pop36, name, x, y, tag, False)
    (OUT / "cities.json").write_text(json.dumps({"cities": list(chosen.values())}, ensure_ascii=False, indent=1), encoding="utf-8")
    log(f"  {len(rivers_out)} tramos de río, {len(chosen)} ciudades con edificios")

    # ---------------------------------------------------------------- 13. Naciones
    log("Escribiendo naciones, banderas y retratos...")
    by_owner = defaultdict(list)
    for pid in range(1, P + 1):
        if owner[pid] is not None:
            by_owner[owner[pid]].append(pid)

    cborder = defaultdict(set)
    for a, b_, _n in pairs:
        oa, ob = owner[a], owner[b_]
        if oa and ob and oa != ob:
            cborder[oa].add(ob)
            cborder[ob].add(oa)
    ccolor = {t: H.N[t]["color"] for t in by_owner if t in H.N}
    for tag in sorted(by_owner, key=lambda t: -len(by_owner[t])):
        if tag in ccolor:
            continue
        crng = random.Random(zlib.crc32(tag.encode()))
        best, best_score = None, -1
        for _ in range(24):
            rr_, gg_, bb_ = colorsys.hsv_to_rgb(crng.random(), 0.35 + crng.random() * 0.4, 0.55 + crng.random() * 0.38)
            cand = (int(rr_ * 255), int(gg_ * 255), int(bb_ * 255))
            score = min([math.dist(cand, ccolor[n]) for n in cborder[tag] if n in ccolor] or [999])
            if score > best_score:
                best, best_score = cand, score
            if score > 90:
                break
        ccolor[tag] = best

    for folder in ("flags", "leaders"):
        (OUT / folder).mkdir(exist_ok=True)
        for old in (OUT / folder).glob("*.png"):  # restos de generaciones anteriores
            old.unlink()
    countries_json = {"countries": []}
    for tag in sorted(by_owner, key=lambda t: -population[by_owner[t]].sum()):
        pids = by_owner[tag]
        data = H.N.get(tag)
        modern = admin0.get(tag, {})
        cap = capital_pid.get(tag)
        if cap is None or cap not in pids:
            cap = max(pids, key=lambda p: population[p])
        name = data["name"] if data else (modern.get("NAME_ES") or tag)
        gov = data["gov"] if data else "Democracy"

        # Bandera: de época (Commons) → actual (flagcdn) → procedural en el juego.
        flag_cache = CACHE / "flags_1936" / f"{tag}.png"
        got_flag = data is not None and fetch_commons_image(flag_cache, data["flags"], 160, f"Bandera {name}")
        if not got_flag:
            core = sorted(data["core"])[0] if data and data["core"] else tag
            iso2 = str(admin0.get(core, {}).get("ISO_A2_EH") or "-99")
            if iso2 != "-99":
                try:
                    flag_cache.parent.mkdir(parents=True, exist_ok=True)
                    if not flag_cache.exists():
                        flag_cache.write_bytes(http_get(f"https://flagcdn.com/w160/{iso2.lower()}.png"))
                    got_flag = True
                except Exception as e:
                    log(f"    sin bandera para {tag}: {e}")
        if got_flag:
            (OUT / "flags" / f"{tag}.png").write_bytes(flag_cache.read_bytes())

        # Retrato del dirigente: imagen principal de su artículo, solo si está en Commons (licencia libre).
        if data and data["leader"]:
            leader_cache = CACHE / "leaders_raw" / f"{tag}.img"
            ok = leader_cache.exists() and leader_cache.with_suffix(".json").exists()
            if ok:
                CREDITS.append(json.loads(leader_cache.with_suffix(".json").read_text(encoding="utf-8")))
            else:
                try:
                    image_name = wiki_page_image(data["wiki"])
                    time.sleep(0.15)
                    ok = bool(image_name) and fetch_commons_image(leader_cache, [image_name], 400, f"Retrato de {data['leader']}")
                except Exception as e:
                    log(f"    sin retrato para {tag}: {e}")
            if ok:
                try:
                    make_portrait(leader_cache, OUT / "leaders" / f"{tag}.png")
                except Exception as e:
                    log(f"    retrato inválido para {tag}: {e}")
            else:
                log(f"    sin retrato libre para {data['leader']} ({tag})")

        r_, g_, b_ = ccolor[tag]
        countries_json["countries"].append({
            "tag": tag, "name": name, "nameEn": H.EN_NAMES.get(tag, name), "economy": H.economy_json(tag), "r": r_, "g": g_, "b": b_, "government": gov,
            "leader": data["leader"] if data else "", "overlord": (data or {}).get("overlord") or "",
            "capital": cap, "flagSeed": zlib.crc32(tag.encode()) & 0x7FFFFFFF, "provinces": pids,
        })
    (OUT / "countries.json").write_text(json.dumps(countries_json, ensure_ascii=False, indent=1), encoding="utf-8")
    (OUT / "map.json").write_text(json.dumps({
        "name": "Mundo 1936", "width": WIDTH, "height": HEIGHT, "seaLevel": SEA_LEVEL,
        "projection": "miller", "latTop": LAT_TOP, "latBottom": LAT_BOTTOM, "startDate": "1936-01-01",
        "normalStrength": 1.1,
    }, indent=1), encoding="utf-8")

    credits = ["Créditos del mapa", "=================", "",
               "Fronteras, ciudades y mares: Natural Earth (dominio público) — naturalearthdata.com",
               "Relieve, batimetría y color: NASA Visible Earth / GEBCO / Blue Marble (dominio público)",
               "Banderas actuales de respaldo: flagcdn.com (dominio público)", "",
               "Imágenes de Wikimedia Commons:"]
    for cr in CREDITS:
        credits.append(f"- {cr['what']}: «{cr['file']}» — {cr['author'] or 'autor desconocido'} — {cr['license']} — {cr['source']}")
    (OUT / "CREDITS.txt").write_text("\n".join(credits) + "\n", encoding="utf-8")

    log(f"Listo en {time.time() - t0:.0f} s: {P} provincias, {len(by_owner)} naciones → {OUT}")


if __name__ == "__main__":
    main()
