"""
Situación política del mundo el 1 de enero de 1936 (fecha de inicio, como en HOI4).

Las fronteras se construyen reasignando las provincias actuales (Natural Earth admin-1) a sus
dueños de 1936. Donde una frontera histórica corta una provincia actual se usan reglas por
coordenadas (p. ej. Sajalín al sur del paralelo 50 era japonés). Es una aproximación jugable,
igual que los "states" de HOI4; todo es editable aquí o en countries.json tras generar el mapa.

Datos de cada nación:
  name       nombre en español
  color      color en el mapa político
  gov        Democracy | Monarchy | Communism | Fascism | NonAligned
  leader     dirigente a 1 de enero de 1936
  wiki       artículo de Wikipedia (inglés) del dirigente, para su retrato
  capital    (lon, lat) de la capital en 1936
  core       países actuales (ADM0_A3) que forman su territorio metropolitano
  gdppc      PIB per cápita aproximado en 1936 (dólares internacionales de 1990, estilo Maddison)
  overlord   soberano si es un estado subordinado (colonia autónoma, títere, protectorado)
  flags      archivos de Wikimedia Commons candidatos para la bandera de la época
"""

N = {}


def nation(tag, name, color, gov, leader=None, wiki=None, capital=None, core=(), gdppc=1000, overlord=None, flags=()):
    N[tag] = dict(name=name, color=color, gov=gov, leader=leader, wiki=wiki or leader, capital=capital,
                  core=set(core), gdppc=gdppc, overlord=overlord, flags=list(flags))


# ----------------------------------------------------------------------------- Grandes potencias
nation("GER", "Alemania", (96, 96, 96), "Fascism", "Adolf Hitler", None, (13.40, 52.52), ["DEU"], 4450,
       flags=["Flag of Germany (1935–1945).svg", "Flag of the German Reich (1935–1945).svg"])
nation("ENG", "Reino Unido", (192, 48, 48), "Democracy", "Stanley Baldwin", None, (-0.13, 51.51), ["GBR"], 5500,
       flags=["Flag of the United Kingdom.svg"])
nation("FRA", "Francia", (58, 92, 190), "Democracy", "Albert Lebrun", None, (2.35, 48.86), ["FRA"], 4500,
       flags=["Flag of France.svg"])
nation("ITA", "Italia", (62, 142, 72), "Fascism", "Benito Mussolini", None, (12.50, 41.90), ["ITA"], 3050,
       flags=["Flag of Italy (1861–1946) crowned.svg", "Flag of Italy (1861-1946) crowned.svg"])
nation("SOV", "Unión Soviética", (128, 24, 24), "Communism", "Iósif Stalin", "Joseph Stalin", (37.62, 55.75),
       ["RUS", "UKR", "BLR", "KAZ", "UZB", "TKM", "KGZ", "TJK", "GEO", "ARM", "AZE"], 2100,
       flags=["Flag of the Soviet Union (1936–1955).svg", "Flag of the Soviet Union (1923–1955).svg", "Flag of the Soviet Union.svg"])
nation("USA", "Estados Unidos", (54, 136, 196), "Democracy", "Franklin D. Roosevelt", None, (-77.04, 38.90), ["USA"], 5470,
       flags=["Flag of the United States (1912–1959).svg", "Flag of the United States (1912-1959).svg"])
nation("JAP", "Japón", (226, 214, 174), "Fascism", "Hirohito", None, (139.69, 35.69), ["JPN"], 2250,
       flags=["Flag of Japan (1870–1999).svg", "Flag of Japan.svg"])

# ----------------------------------------------------------------------------- Asia
nation("CHI", "China", (212, 172, 64), "NonAligned", "Chiang Kai-shek", None, (118.80, 32.06), ["CHN"], 560,
       flags=["Flag of the Republic of China.svg"])
nation("PRC", "China comunista", (176, 44, 44), "Communism", "Mao Zedong", None, (109.49, 36.59), [], 450,
       flags=["Flag of the Chinese Soviet Republic.svg", "Flag of the Chinese Communist Party (pre-1996).svg"])
nation("MAN", "Manchukuo", (150, 110, 60), "Fascism", "Puyi", None, (125.32, 43.88), [], 900, overlord="JAP",
       flags=["Flag of Manchukuo.svg"])
nation("GXC", "Camarilla de Guangxi", (110, 150, 80), "NonAligned", "Li Zongren", None, (108.32, 22.82), [], 450,
       flags=["Flag of the Republic of China.svg"])
nation("YUN", "Yunnan", (160, 120, 170), "NonAligned", "Long Yun", None, (102.71, 25.04), [], 420,
       flags=["Flag of the Republic of China.svg"])
nation("SHX", "Shanxi", (200, 140, 100), "NonAligned", "Yan Xishan", None, (112.55, 37.87), [], 480,
       flags=["Flag of the Republic of China.svg"])
nation("XSM", "Camarilla Ma", (120, 170, 170), "NonAligned", "Ma Bufang", None, (101.78, 36.62), [], 380,
       flags=["Flag of the Republic of China.svg"])
nation("SIK", "Sinkiang", (140, 180, 110), "NonAligned", "Sheng Shicai", None, (87.62, 43.83), [], 400, overlord="SOV",
       flags=["Flag of the Republic of China.svg"])
nation("TIB", "Tíbet", (190, 150, 200), "NonAligned", "Reting Rinpoche", "5th Reting Rinpoche", (91.13, 29.65), [], 350,
       flags=["Flag of Tibet.svg"])
nation("MON", "Mongolia", (120, 130, 190), "Communism", "Peljidiin Genden", None, (106.91, 47.92), ["MNG"], 700, overlord="SOV",
       flags=["Flag of the Mongolian People's Republic (1930–1940).svg", "Flag of Mongolia (1924–1940).svg"])
nation("TAN", "Tannu Tuvá", (170, 90, 140), "Communism", "Salchak Toka", None, (94.45, 51.72), [], 600, overlord="SOV",
       flags=["Flag of the Tuvan People's Republic (1933–1939).svg", "Flag of Tuva (1933-1939).svg"])
nation("SIA", "Siam", (110, 90, 170), "NonAligned", "Phraya Phahonphonphayuhasena", None, (100.50, 13.75), ["THA"], 830,
       flags=["Flag of Thailand.svg"])
nation("PHI", "Filipinas", (140, 170, 220), "Democracy", "Manuel L. Quezon", None, (120.98, 14.60), ["PHL"], 1400, overlord="USA",
       flags=["Flag of the Philippines (1936–1985).svg", "Flag of the Philippines (navy blue).svg"])
nation("RAJ", "Raj británico", (230, 142, 48), "Democracy", "Lord Willingdon", "Freeman Freeman-Thomas, 1st Marquess of Willingdon",
       (77.21, 28.61), ["IND", "PAK", "BGD", "MMR"], 680, overlord="ENG",
       flags=["British Raj Red Ensign.svg", "Flag of British India.svg"])
nation("NEP", "Nepal", (170, 60, 90), "Monarchy", "Juddha Shumsher Jang Bahadur Rana", None, (85.32, 27.71), ["NPL"], 500,
       flags=["Flag of Nepal.svg"])
nation("BHU", "Bután", (200, 120, 60), "Monarchy", "Jigme Wangchuck", None, (89.64, 27.47), ["BTN"], 450,
       flags=["Flag of Bhutan.svg"])
nation("AFG", "Afganistán", (110, 140, 90), "Monarchy", "Mohammed Zahir Shah", None, (69.17, 34.53), ["AFG"], 700,
       flags=["Flag of Afghanistan (1931–1973).svg"])
nation("PER", "Irán", (90, 150, 110), "Monarchy", "Reza Shah", None, (51.39, 35.69), ["IRN"], 1150,
       flags=["State flag of Iran (1933–1964).svg", "Flag of Iran (1933–1964).svg"])
nation("IRQ", "Irak", (150, 120, 90), "Monarchy", "Ghazi de Irak", "Ghazi of Iraq", (44.37, 33.31), ["IRQ"], 1100,
       flags=["Flag of Iraq (1924–1959).svg"])
nation("SAU", "Arabia Saudí", (90, 130, 70), "Monarchy", "Ibn Saud", None, (46.72, 24.69), ["SAU"], 900,
       flags=["Flag of Saudi Arabia (1938–1973).svg", "Flag of Saudi Arabia.svg"])
nation("YEM", "Yemen", (160, 80, 80), "Monarchy", "Yahya Muhammad Hamid ed-Din", None, (44.21, 15.35), ["YEM"], 600,
       flags=["Flag of the Mutawakkilite Kingdom of Yemen.svg"])
nation("OMA", "Mascate y Omán", (180, 90, 100), "Monarchy", "Said bin Taimur", None, (58.59, 23.61), ["OMN"], 700, overlord="ENG",
       flags=["Flag of Muscat.svg"])
nation("TUR", "Turquía", (112, 164, 126), "NonAligned", "Mustafa Kemal Atatürk", None, (32.85, 39.93), ["TUR"], 1450,
       flags=["Flag of Turkey.svg"])

# ----------------------------------------------------------------------------- Europa
nation("SPR", "España", (222, 182, 44), "Democracy", "Niceto Alcalá-Zamora", None, (-3.70, 40.42), ["ESP"], 2050,
       flags=["Flag of the Second Spanish Republic.svg", "Flag of Spain (1931–1939).svg"])
nation("POR", "Portugal", (60, 120, 80), "NonAligned", "António de Oliveira Salazar", None, (-9.14, 38.72), ["PRT"], 1650,
       flags=["Flag of Portugal.svg"])
nation("POL", "Polonia", (206, 96, 132), "NonAligned", "Ignacy Mościcki", None, (21.01, 52.23), ["POL"], 1600,
       flags=["Flag of Poland.svg"])
nation("CZE", "Checoslovaquia", (70, 120, 170), "Democracy", "Edvard Beneš", None, (14.42, 50.09), ["CZE", "SVK"], 2900,
       flags=["Flag of the Czech Republic.svg"])
nation("AUT", "Austria", (220, 220, 220), "NonAligned", "Kurt Schuschnigg", None, (16.37, 48.21), ["AUT"], 3100,
       flags=["Flag of Austria.svg"])
nation("HUN", "Hungría", (180, 110, 80), "NonAligned", "Miklós Horthy", None, (19.04, 47.50), ["HUN"], 2500,
       flags=["Flag of Hungary (1915–1918, 1919–1946).svg", "Flag of Hungary.svg"])
nation("ROM", "Rumania", (210, 190, 80), "NonAligned", "Carol II", "Carol II of Romania", (26.10, 44.43), ["ROU", "MDA"], 1250,
       flags=["Flag of Romania.svg"])
nation("YUG", "Yugoslavia", (90, 90, 170), "Monarchy", "Príncipe Pablo", "Prince Paul of Yugoslavia", (20.46, 44.79),
       ["SRB", "MNE", "HRV", "SVN", "BIH", "MKD", "KOS"], 1300,
       flags=["Flag of the Kingdom of Yugoslavia.svg", "Flag of Yugoslavia (1918–1943).svg"])
nation("BUL", "Bulgaria", (100, 140, 70), "Monarchy", "Boris III", "Boris III of Bulgaria", (23.32, 42.70), ["BGR"], 1500,
       flags=["Flag of Bulgaria.svg"])
nation("GRE", "Grecia", (110, 160, 210), "Monarchy", "Jorge II", "George II of Greece", (23.73, 37.98), ["GRC"], 2100,
       flags=["Flag of Greece (1822–1978).svg", "Flag of Greece (1828-1978).svg"])
nation("ALB", "Albania", (150, 60, 60), "Monarchy", "Zog I", None, (19.82, 41.33), ["ALB"], 800,
       flags=["Flag of Albania (1934–1939).svg", "Flag of Albania.svg"])
nation("LIT", "Lituania", (210, 200, 110), "NonAligned", "Antanas Smetona", None, (23.90, 54.90), ["LTU"], 1700,
       flags=["Flag of Lithuania (1918–1940).svg", "Flag of Lithuania 1918-1940.svg"])
nation("LAT", "Letonia", (150, 70, 90), "NonAligned", "Kārlis Ulmanis", None, (24.11, 56.95), ["LVA"], 2300,
       flags=["Flag of Latvia.svg"])
nation("EST", "Estonia", (70, 100, 160), "NonAligned", "Konstantin Päts", None, (24.75, 59.44), ["EST"], 2400,
       flags=["Flag of Estonia.svg"])
nation("FIN", "Finlandia", (226, 228, 238), "Democracy", "Pehr Evind Svinhufvud", None, (24.94, 60.17), ["FIN"], 3100,
       flags=["Flag of Finland.svg"])
nation("SWE", "Suecia", (64, 112, 190), "Democracy", "Per Albin Hansson", None, (18.07, 59.33), ["SWE"], 4300,
       flags=["Flag of Sweden.svg"])
nation("NOR", "Noruega", (150, 70, 70), "Democracy", "Johan Nygaardsvold", None, (10.75, 59.91), ["NOR"], 3800,
       flags=["Flag of Norway.svg"])
nation("DEN", "Dinamarca", (170, 60, 60), "Democracy", "Thorvald Stauning", None, (12.57, 55.68), ["DNK"], 5200,
       flags=["Flag of Denmark.svg"])
nation("ICE", "Islandia", (110, 130, 200), "Democracy", "Hermann Jónasson", None, (-21.94, 64.15), ["ISL"], 3500,
       flags=["Flag of Iceland.svg"])
nation("HOL", "Países Bajos", (230, 130, 60), "Democracy", "Hendrikus Colijn", None, (4.90, 52.37), ["NLD"], 4800,
       flags=["Flag of the Netherlands.svg"])
nation("BEL", "Bélgica", (180, 160, 60), "Democracy", "Leopoldo III", "Leopold III of Belgium", (4.35, 50.85), ["BEL"], 4500,
       flags=["Flag of Belgium.svg"])
nation("LUX", "Luxemburgo", (100, 180, 200), "Democracy", "Joseph Bech", None, (6.13, 49.61), ["LUX"], 5500,
       flags=["Flag of Luxembourg.svg"])
nation("SWI", "Suiza", (200, 60, 60), "Democracy", "Albert Meyer", "Albert Meyer (Swiss politician)", (7.45, 46.95), ["CHE"], 6000,
       flags=["Flag of Switzerland.svg"])
nation("IRE", "Irlanda", (90, 170, 90), "Democracy", "Éamon de Valera", None, (-6.26, 53.35), ["IRL"], 2700,
       flags=["Flag of Ireland.svg"])
nation("DNZ", "Ciudad Libre de Dánzig", (210, 180, 180), "NonAligned", "Arthur Greiser", None, (18.65, 54.35), [], 3000,
       flags=["Flag of the Free City of Danzig.svg"])

# ----------------------------------------------------------------------------- Dominios y América
nation("CAN", "Canadá", (170, 50, 70), "Democracy", "William Lyon Mackenzie King", None, (-75.70, 45.42), ["CAN"], 4000, overlord="ENG",
       flags=["Canadian Red Ensign (1921–1957).svg", "Canadian Red Ensign 1921-1957.svg", "Flag of Canada (1921–1957).svg"])
nation("AST", "Australia", (66, 132, 160), "Democracy", "Joseph Lyons", None, (149.13, -35.28), ["AUS"], 5000, overlord="ENG",
       flags=["Flag of Australia (converted).svg", "Flag of Australia.svg"])
nation("NZL", "Nueva Zelanda", (60, 90, 140), "Democracy", "Michael Joseph Savage", None, (174.78, -41.29), ["NZL"], 5500, overlord="ENG",
       flags=["Flag of New Zealand.svg"])
nation("SAF", "Sudáfrica", (110, 140, 60), "Democracy", "J. B. M. Hertzog", None, (28.19, -25.75), ["ZAF"], 2800, overlord="ENG",
       flags=["Flag of South Africa (1928–1982).svg", "Flag of South Africa (1928-1994).svg"])
nation("MEX", "México", (118, 168, 96), "NonAligned", "Lázaro Cárdenas", None, (-99.13, 19.43), ["MEX"], 1700,
       flags=["Flag of Mexico (1934–1968).svg", "Flag of Mexico.svg"])
nation("BRA", "Brasil", (76, 160, 66), "NonAligned", "Getúlio Vargas", None, (-43.20, -22.91), ["BRA"], 1050,
       flags=["Flag of Brazil (1889–1960).svg", "Flag of Brazil.svg"])
nation("ARG", "Argentina", (132, 186, 222), "NonAligned", "Agustín P. Justo", None, (-58.38, -34.60), ["ARG"], 4100,
       flags=["Flag of Argentina.svg"])
nation("CHL", "Chile", (180, 60, 80), "Democracy", "Arturo Alessandri", None, (-70.65, -33.45), ["CHL"], 3100,
       flags=["Flag of Chile.svg"])
nation("PRU", "Perú", (200, 100, 90), "NonAligned", "Óscar R. Benavides", None, (-77.04, -12.05), ["PER"], 1700,
       flags=["Flag of Peru.svg"])
nation("COL", "Colombia", (220, 200, 90), "Democracy", "Alfonso López Pumarejo", None, (-74.07, 4.71), ["COL"], 1800,
       flags=["Flag of Colombia.svg"])
nation("VEN", "Venezuela", (190, 120, 60), "NonAligned", "Eleazar López Contreras", None, (-66.90, 10.49), ["VEN"], 2500,
       flags=["Flag of Venezuela (1930–2006).svg", "Flag of Venezuela.svg"])
nation("CUB", "Cuba", (90, 110, 170), "NonAligned", "Fulgencio Batista", None, (-82.37, 23.11), ["CUB"], 2000,
       flags=["Flag of Cuba.svg"])

# ----------------------------------------------------------------------------- África
nation("EGY", "Egipto", (200, 186, 120), "Monarchy", "Fuad I", "Fuad I of Egypt", (31.24, 30.04), ["EGY"], 900,
       flags=["Flag of Egypt (1922–1958).svg", "Flag of the Kingdom of Egypt.svg"])
nation("ETH", "Etiopía", (100, 170, 90), "Monarchy", "Haile Selassie", None, (38.75, 9.03), ["ETH"], 400,
       flags=["Flag of Ethiopia (1897–1936; 1941–1974).svg", "Flag of the Ethiopian Empire.svg"])
nation("LIB", "Liberia", (120, 90, 150), "NonAligned", "Edwin Barclay", None, (-10.80, 6.31), ["LBR"], 600,
       flags=["Flag of Liberia.svg"])

# ----------------------------------------------------------------------------- Pertenencia por país actual
# País actual (ADM0_A3 original, antes de agrupar dependencias) → dueño en 1936.
OWNER_BY_ADM0 = {
    # Imperio británico
    "GBR": "ENG", "GGY": "ENG", "JEY": "ENG", "IMN": "ENG", "GIB": "ENG", "MLT": "ENG", "CYP": "ENG", "CYN": "ENG",
    "ESB": "ENG", "WSB": "ENG", "ISR": "ENG", "PSX": "ENG", "JOR": "ENG", "KWT": "ENG", "BHR": "ENG", "QAT": "ENG",
    "ARE": "ENG", "SDN": "ENG", "SDS": "ENG", "KEN": "ENG", "UGA": "ENG", "TZA": "ENG", "ZMB": "ENG", "MWI": "ENG",
    "ZWE": "ENG", "BWA": "ENG", "LSO": "ENG", "SWZ": "ENG", "NGA": "ENG", "GHA": "ENG", "SLE": "ENG", "GMB": "ENG",
    "SOL": "ENG", "MUS": "ENG", "SYC": "ENG", "SHN": "ENG", "IOT": "ENG", "LKA": "ENG", "MDV": "ENG", "MYS": "ENG",
    "SGP": "ENG", "BRN": "ENG", "HKG": "ENG", "GUY": "ENG", "BLZ": "ENG", "JAM": "ENG", "BHS": "ENG", "TTO": "ENG",
    "BRB": "ENG", "ATG": "ENG", "KNA": "ENG", "LCA": "ENG", "VCT": "ENG", "GRD": "ENG", "DMA": "ENG", "BMU": "ENG",
    "CYM": "ENG", "TCA": "ENG", "VGB": "ENG", "AIA": "ENG", "MSR": "ENG", "FLK": "ENG", "SGS": "ENG", "FJI": "ENG",
    "SLB": "ENG", "VUT": "ENG", "KIR": "ENG", "TUV": "ENG", "TON": "ENG", "PCN": "ENG",
    "IND": "RAJ", "PAK": "RAJ", "BGD": "RAJ", "MMR": "RAJ", "KAS": "RAJ",
    "CAN": "CAN", "AUS": "AST", "PNG": "AST", "NRU": "AST", "NFK": "AST", "HMD": "AST", "IOA": "AST", "ATC": "AST",
    "NZL": "NZL", "WSM": "NZL", "COK": "NZL", "NIU": "NZL", "ZAF": "SAF", "NAM": "SAF", "IRL": "IRE", "OMN": "OMA",
    # Imperio francés
    "FRA": "FRA", "DZA": "FRA", "TUN": "FRA", "MAR": "FRA", "MRT": "FRA", "SEN": "FRA", "MLI": "FRA", "GIN": "FRA",
    "CIV": "FRA", "BFA": "FRA", "NER": "FRA", "BEN": "FRA", "TGO": "FRA", "TCD": "FRA", "CAF": "FRA", "COG": "FRA",
    "GAB": "FRA", "CMR": "FRA", "MDG": "FRA", "COM": "FRA", "DJI": "FRA", "SYR": "FRA", "LBN": "FRA", "VNM": "FRA",
    "LAO": "FRA", "KHM": "FRA", "NCL": "FRA", "PYF": "FRA", "WLF": "FRA", "SPM": "FRA", "BLM": "FRA", "MAF": "FRA",
    "ATF": "FRA", "CLP": "FRA",
    # Otros imperios coloniales
    "ITA": "ITA", "LBY": "ITA", "ERI": "ITA", "SOM": "ITA", "SMR": "ITA", "VAT": "ITA",
    "ESP": "SPR", "SAH": "SPR", "GNQ": "SPR",
    "PRT": "POR", "AGO": "POR", "MOZ": "POR", "GNB": "POR", "CPV": "POR", "STP": "POR", "TLS": "POR", "MAC": "POR",
    "NLD": "HOL", "IDN": "HOL", "SUR": "HOL", "ABW": "HOL", "CUW": "HOL", "SXM": "HOL",
    "BEL": "BEL", "COD": "BEL", "RWA": "BEL", "BDI": "BEL",
    "DNK": "DEN", "GRL": "DEN", "FRO": "DEN",
    "JPN": "JAP", "KOR": "JAP", "PRK": "JAP", "TWN": "JAP", "PLW": "JAP", "FSM": "JAP", "MHL": "JAP", "MNP": "JAP",
    "USA": "USA", "PGA": "USA", "PRI": "USA", "VIR": "USA", "GUM": "USA", "ASM": "USA", "UMI": "USA", "USG": "USA",
    "PHL": "PHI",
    # Unión Soviética y vecinos
    "RUS": "SOV", "UKR": "SOV", "BLR": "SOV", "KAZ": "SOV", "KAB": "SOV", "UZB": "SOV", "TKM": "SOV", "KGZ": "SOV",
    "TJK": "SOV", "GEO": "SOV", "ARM": "SOV", "AZE": "SOV", "MNG": "MON",
    "DEU": "GER", "POL": "POL", "CZE": "CZE", "SVK": "CZE", "AUT": "AUT", "HUN": "HUN", "ROU": "ROM", "MDA": "ROM",
    "SRB": "YUG", "MNE": "YUG", "HRV": "YUG", "SVN": "YUG", "BIH": "YUG", "MKD": "YUG", "KOS": "YUG",
    "BGR": "BUL", "GRC": "GRE", "ALB": "ALB", "TUR": "TUR", "LTU": "LIT", "LVA": "LAT", "EST": "EST", "FIN": "FIN",
    "ALD": "FIN", "SWE": "SWE", "NOR": "NOR", "ISL": "ICE", "CHE": "SWI", "LIE": "SWI", "LUX": "LUX",
    "MCO": "FRA", "AND": "AND",
    "CHN": "CHI", "THA": "SIA", "NPL": "NEP", "BTN": "BHU", "AFG": "AFG", "IRN": "PER", "IRQ": "IRQ", "SAU": "SAU",
    "YEM": "YEM", "EGY": "EGY", "ETH": "ETH", "LBR": "LIB",
    "MEX": "MEX", "BRA": "BRA", "ARG": "ARG", "CHL": "CHL", "PER": "PRU", "COL": "COL", "VEN": "VEN", "CUB": "CUB",
}

# Provincias concretas (código ISO 3166-2 de Natural Earth) con otro dueño.
OWNER_BY_ISO = {
    # Alemania de 1936: Prusia Oriental, Pomerania, Silesia, Neumark
    "RU-KGD": "GER", "PL-ZP": "GER", "PL-LB": "GER", "PL-DS": "GER", "PL-OP": "GER", "PL-WN": "GER",
    # Polonia de entreguerras: Kresy y Vilna
    "LT-VL": "POL", "BY-HR": "POL", "BY-BR": "POL", "UA-46": "POL", "UA-07": "POL", "UA-56": "POL",
    "UA-61": "POL", "UA-26": "POL",
    "UA-21": "CZE",                      # Rutenia Subcarpática
    "UA-77": "ROM",                      # Bucovina del norte
    "BG-08": "ROM", "BG-19": "ROM",      # Dobruja del sur
    "MD-SN": "SOV",                      # Transnistria (RASS Moldava)
    "HR-18": "ITA",                      # Istria
    "TR-31": "FRA",                      # Sanjak de Alejandreta (Siria francesa)
    "RU-TY": "TAN",                      # Tuvá
    "CA-NL": "ENG",                      # Dominio de Terranova (administrado por Londres)
    "IN-GA": "POR", "IN-PY": "FRA",      # Goa portuguesa, Pondicherry francesa
    # China: Manchukuo, señores de la guerra, comunistas
    "CN-HL": "MAN", "CN-JL": "MAN", "CN-LN": "MAN",
    "CN-GX": "GXC", "CN-YN": "YUN", "CN-SX": "SHX", "CN-NX": "XSM", "CN-QH": "XSM",
    "CN-XJ": "SIK", "CN-XZ": "TIB",
    "YE-AD": "ENG", "YE-LA": "ENG", "YE-AB": "ENG", "YE-SH": "ENG", "YE-HD": "ENG", "YE-MR": "ENG", "YE-DA": "ENG",
}

# Reglas por coordenadas: (adm0 original, iso o None, condición(lon, lat) → bool, dueño).
# Se evalúan píxel a píxel, así que pueden partir una provincia actual en dos.
PIXEL_RULES = [
    ("RUS", "RU-SAK", lambda lon, lat: (lat < 50.0) | (lon > 144.5), "JAP"),          # Karafuto y Kuriles
    ("RUS", "RU-LEN", lambda lon, lat: (lat > 60.15) & (lon < 30.6), "FIN"),            # istmo de Carelia
    ("RUS", "RU-KR", lambda lon, lat: (lon < 31.6) & (lat < 62.4), "FIN"),              # Carelia del Ládoga
    ("RUS", "RU-MUR", lambda lon, lat: (lon < 31.6) & (lat > 69.0), "FIN"),             # Petsamo
    ("POL", "PL-PM", lambda lon, lat: lon < 17.55, "GER"),                              # Lauenburg y Bütow
    ("POL", "PL-PM", lambda lon, lat: (lon > 18.35) & (lon < 19.3) & (lat > 54.05) & (lat < 54.45), "DNZ"),
    ("POL", "PL-SL", lambda lon, lat: (lon < 18.8) & (lat > 50.18) & (lat < 50.45), "GER"),   # Alta Silesia alemana
    ("POL", "PL-WP", lambda lon, lat: (lat > 52.95) | (lon < 15.95), "GER"),            # Schneidemühl / Fraustadt
    ("BLR", "BY-MI", lambda lon, lat: lon < 26.9, "POL"),
    ("BLR", "BY-VI", lambda lon, lat: (lon < 28.2) & (lat < 55.9), "POL"),
    ("UKR", "UA-51", lambda lon, lat: (lon < 30.0) & (lat < 46.6), "ROM"),             # Budjak (Besarabia del sur)
    ("SVN", None, lambda lon, lat: lon < 14.25, "ITA"),                                 # Litoral esloveno
    ("GRC", "GR-L", lambda lon, lat: (lon > 26.5) & (lat < 37.6), "ITA"),              # Dodecaneso
    ("CHN", "CN-NM", lambda lon, lat: lon > 117.6, "MAN"),                              # Rehe y Xing'an
    ("CHN", "CN-HE", lambda lon, lat: (lat > 40.55) & (lon > 116.8), "MAN"),            # Chengde (Rehe)
    ("CHN", "CN-SN", lambda lon, lat: lat > 36.2, "PRC"),                               # Shaanxi del norte (Yan'an)
    ("MAR", None, lambda lon, lat: (lat > 34.75) & (lon < -1.9), "SPR"),                # Protectorado español del norte
    ("MAR", None, lambda lon, lat: lat < 28.3, "SPR"),                                  # Cabo Juby
    ("MYS", None, lambda lon, lat: lon > 109.0, "ENG"),                                 # Borneo británico (ya ENG)
]

# Población de 1936 respecto a la actual, por país actual (cuando se conoce bien).
POP_1936_MILLIONS = {
    "DEU": 61, "GBR": 47, "FRA": 41.9, "ITA": 42.9, "ESP": 25, "POL": 32, "RUS": 104, "UKR": 30, "BLR": 9,
    "USA": 128, "JPN": 70, "CHN": 500, "IND": 290, "PAK": 25, "BGD": 40, "IDN": 66, "BRA": 38, "MEX": 18.5,
    "ARG": 12.8, "CAN": 11, "AUS": 6.8, "TUR": 16.5, "IRN": 15, "EGY": 15.9, "NGA": 20, "ETH": 10, "THA": 14.5,
    "PHL": 15, "VNM": 19, "KOR": 15, "PRK": 7, "TWN": 5.4, "CZE": 10.9, "SVK": 3.4, "ROU": 16, "HUN": 9,
    "AUT": 6.8, "NLD": 8.5, "BEL": 8.3, "SWE": 6.25, "NOR": 2.9, "DNK": 3.7, "FIN": 3.6, "CHE": 4.2, "PRT": 7.3,
    "GRC": 6.9, "BGR": 6.2, "IRL": 3.0, "NZL": 1.6, "ZAF": 9.6, "KAZ": 6, "UZB": 6.3, "MMR": 15, "LKA": 5.8,
}

# Factor de población 1936/actual por región (resto de países).
REGION_POP_FACTOR = {
    "Europe": 0.72, "Northern America": 0.37, "Latin America and the Caribbean": 0.22, "Eastern Asia": 0.33,
    "South-Eastern Asia": 0.25, "Southern Asia": 0.2, "Western Asia": 0.15, "Central Asia": 0.25,
    "Africa": 0.12, "Oceania": 0.3,
}

# PIB per cápita por defecto para territorios no metropolitanos (colonias), por región.
REGION_GDPPC = {"Africa": 600, "Asia": 700, "Americas": 1500, "Europe": 2500, "Oceania": 1200}


# Nombres en inglés (se muestran sobre el mapa).
EN_NAMES = {
    "RAJ": "British Raj",
    "CHI": "China",
    "SOV": "Soviet Union",
    "ENG": "United Kingdom",
    "USA": "United States",
    "FRA": "France",
    "JAP": "Japan",
    "MAN": "Manchukuo",
    "HOL": "Netherlands",
    "GER": "Germany",
    "ITA": "Italy",
    "SIK": "Sinkiang",
    "BRA": "Brazil",
    "POL": "Poland",
    "SPR": "Spain",
    "BEL": "Belgium",
    "ROM": "Romania",
    "MEX": "Mexico",
    "POR": "Portugal",
    "TUR": "Turkey",
    "EGY": "Egypt",
    "PHI": "Philippines",
    "PER": "Iran",
    "YUG": "Yugoslavia",
    "CZE": "Czechoslovakia",
    "SIA": "Siam",
    "GXC": "Guangxi Clique",
    "ARG": "Argentina",
    "SHX": "Shanxi",
    "COL": "Colombia",
    "CAN": "Canada",
    "YUN": "Yunnan",
    "XSM": "Ma Clique",
    "TIB": "Tibet",
    "ETH": "Ethiopia",
    "SAF": "South Africa",
    "AST": "Australia",
    "HUN": "Hungary",
    "AFG": "Afghanistan",
    "PRU": "Peru",
    "AUT": "Austria",
    "GRE": "Greece",
    "VEN": "Venezuela",
    "SWE": "Sweden",
    "BUL": "Bulgaria",
    "IRQ": "Iraq",
    "NEP": "Nepal",
    "SAU": "Saudi Arabia",
    "SWI": "Switzerland",
    "CHL": "Chile",
    "ECU": "Ecuador",
    "FIN": "Finland",
    "DEN": "Denmark",
    "GTM": "Guatemala",
    "IRE": "Ireland",
    "NOR": "Norway",
    "YEM": "Yemen",
    "BOL": "Bolivia",
    "CUB": "Cuba",
    "HTI": "Haiti",
    "DOM": "Dominican Republic",
    "HND": "Honduras",
    "ALB": "Albania",
    "PRC": "Communist China",
    "NZL": "New Zealand",
    "PRY": "Paraguay",
    "NIC": "Nicaragua",
    "SLV": "El Salvador",
    "LIT": "Lithuania",
    "LAT": "Latvia",
    "DNZ": "Danzig",
    "CRI": "Costa Rica",
    "MON": "Mongolia",
    "EST": "Estonia",
    "PAN": "Panama",
    "URY": "Uruguay",
    "OMA": "Muscat and Oman",
    "LIB": "Liberia",
    "LUX": "Luxembourg",
    "TAN": "Tannu Tuva",
    "ICE": "Iceland",
    "BHU": "Bhutan",
    "AND": "Andorra"
}

# ---------------------------------------------------------------------------------------------------------------
# Base económica de 1936 (se añade a history_1936.py).
# Valores orientativos basados en la situación real de cada país en 1936 (industrialización, comercio exterior,
# capital privado), comprimidos a propósito para que ningún país tenga cifras desmesuradas:
#   talleres, industrias civiles, centros comerciales, empresas privadas, capital privado (M$),
#   fábricas militares, fábricas aeronáuticas, astilleros, exportaciones e importaciones (por periodo de 15 días;
#   aprox. comercio anual real en millones de $ / 60).
# Las economías planificadas (URSS, Mongolia, Tannu Tuva, China comunista) no tienen empresas ni capital privado.
# ---------------------------------------------------------------------------------------------------------------
ECONOMY_FIELDS = ("workshops", "civilianFactories", "commercialCenters", "privateCompanies", "privateCapital",
                  "militaryFactories", "aircraftFactories", "shipyards", "exports", "imports")
ECONOMY_1936 = {
    # Grandes potencias
    "USA": (14, 30, 16, 10, 900, 3, 2, 6, 41.0, 40.0),
    "ENG": (14, 22, 16, 9, 800, 7, 4, 12, 33.0, 70.0),
    "GER": (10, 24, 9, 8, 600, 10, 6, 4, 32.0, 31.0),
    "FRA": (12, 16, 10, 7, 500, 7, 3, 5, 16.0, 25.0),
    "SOV": (16, 22, 4, 0, 0, 14, 6, 3, 5.5, 5.0),
    "ITA": (10, 11, 6, 4, 180, 6, 3, 4, 7.0, 7.0),
    "JAP": (10, 13, 7, 5, 250, 6, 4, 7, 13.0, 14.0),
    # Dominios y colonias británicas
    "CAN": (6, 8, 5, 3, 150, 1, 1, 1, 17.0, 12.0),
    "AST": (5, 5, 4, 2, 90, 1, 0, 1, 8.0, 7.5),
    "NZL": (2, 2, 2, 1, 30, 0, 0, 0, 3.7, 3.0),
    "SAF": (4, 4, 3, 2, 70, 1, 0, 0, 3.5, 6.5),
    "RAJ": (16, 6, 4, 1, 50, 1, 0, 1, 10.0, 8.0),
    "IRE": (3, 1, 2, 1, 20, 0, 0, 0, 1.8, 3.3),
    # Asia
    "CHI": (14, 4, 3, 1, 30, 2, 0, 1, 3.5, 4.7),
    "MAN": (5, 4, 2, 1, 30, 2, 1, 0, 3.3, 4.0),
    "PRC": (2, 0, 0, 0, 0, 0, 0, 0, 0.0, 0.0),
    "SIK": (3, 0, 1, 0, 0, 0, 0, 0, 0.2, 0.2),
    "GXC": (3, 1, 1, 0, 5, 0, 0, 0, 0.2, 0.3),
    "SHX": (3, 1, 1, 0, 5, 1, 0, 0, 0.1, 0.2),
    "YUN": (3, 0, 1, 0, 3, 0, 0, 0, 0.3, 0.3),
    "XSM": (2, 0, 0, 0, 0, 0, 0, 0, 0.1, 0.1),
    "TIB": (2, 0, 0, 0, 0, 0, 0, 0, 0.1, 0.1),
    "MON": (2, 0, 0, 0, 0, 0, 0, 0, 0.2, 0.2),
    "TAN": (1, 0, 0, 0, 0, 0, 0, 0, 0.05, 0.05),
    "BHU": (1, 0, 0, 0, 0, 0, 0, 0, 0.0, 0.0),
    "NEP": (2, 0, 1, 0, 0, 0, 0, 0, 0.1, 0.1),
    "AFG": (3, 0, 1, 0, 2, 0, 0, 0, 0.3, 0.3),
    "PHI": (5, 1, 2, 1, 20, 0, 0, 0, 2.5, 2.2),
    "SIA": (5, 1, 2, 0, 10, 0, 0, 0, 1.2, 1.0),
    # Oriente Medio y África
    "PER": (4, 1, 2, 0, 10, 0, 0, 0, 1.4, 1.2),
    "IRQ": (2, 0, 1, 0, 5, 0, 0, 0, 1.0, 0.8),
    "SAU": (2, 0, 1, 0, 0, 0, 0, 0, 0.2, 0.3),
    "YEM": (1, 0, 1, 0, 0, 0, 0, 0, 0.1, 0.1),
    "OMA": (1, 0, 1, 0, 0, 0, 0, 0, 0.1, 0.1),
    "TUR": (7, 2, 3, 1, 15, 1, 0, 1, 1.6, 1.5),
    "EGY": (6, 2, 3, 1, 40, 0, 0, 0, 2.7, 2.5),
    "ETH": (3, 0, 1, 0, 0, 0, 0, 0, 0.2, 0.2),
    "LIB": (1, 0, 1, 0, 5, 0, 0, 0, 0.1, 0.1),
    # Europa occidental y nórdica
    "HOL": (5, 6, 6, 4, 200, 1, 1, 2, 9.0, 11.0),
    "BEL": (4, 6, 4, 3, 150, 1, 0, 0, 12.0, 12.5),
    "LUX": (1, 1, 1, 1, 20, 0, 0, 0, 0.5, 0.5),
    "SWI": (4, 4, 4, 3, 150, 1, 0, 0, 4.7, 6.3),
    "SWE": (4, 5, 3, 3, 120, 1, 1, 1, 7.5, 7.8),
    "NOR": (3, 2, 2, 1, 40, 0, 0, 1, 3.5, 4.7),
    "DEN": (3, 3, 3, 2, 60, 0, 0, 1, 5.5, 5.8),
    "FIN": (3, 2, 1, 1, 25, 0, 0, 0, 2.8, 2.8),
    "ICE": (1, 0, 1, 0, 3, 0, 0, 0, 0.2, 0.2),
    "AND": (1, 0, 0, 0, 0, 0, 0, 0, 0.0, 0.0),
    "POR": (4, 2, 2, 1, 20, 0, 0, 1, 1.2, 1.8),
    "SPR": (7, 4, 3, 1, 40, 1, 0, 1, 3.3, 3.5),
    # Europa central y oriental
    "POL": (7, 4, 3, 1, 30, 2, 1, 0, 4.0, 3.8),
    "CZE": (5, 6, 3, 2, 80, 3, 1, 0, 5.5, 5.2),
    "AUT": (3, 3, 2, 1, 40, 1, 0, 0, 2.7, 3.3),
    "HUN": (4, 3, 2, 1, 30, 1, 0, 0, 2.2, 2.0),
    "ROM": (6, 3, 2, 1, 30, 1, 0, 0, 2.7, 2.0),
    "YUG": (6, 2, 2, 1, 20, 1, 0, 0, 1.7, 1.6),
    "BUL": (4, 1, 1, 0, 5, 0, 0, 0, 0.7, 0.6),
    "GRE": (4, 1, 2, 1, 15, 0, 0, 0, 1.2, 1.8),
    "ALB": (2, 0, 1, 0, 0, 0, 0, 0, 0.1, 0.2),
    "LIT": (2, 1, 1, 0, 5, 0, 0, 0, 0.8, 0.7),
    "LAT": (2, 1, 1, 0, 5, 0, 0, 0, 0.9, 0.8),
    "EST": (2, 1, 1, 0, 5, 0, 0, 0, 0.6, 0.6),
    "DNZ": (1, 0, 1, 0, 5, 0, 0, 1, 0.3, 0.3),
    # América
    "MEX": (6, 2, 3, 1, 30, 0, 0, 0, 3.5, 2.2),
    "BRA": (8, 3, 3, 2, 50, 0, 0, 1, 5.3, 4.5),
    "ARG": (6, 4, 4, 2, 80, 1, 0, 1, 8.7, 6.3),
    "CHL": (3, 1, 2, 1, 20, 0, 0, 0, 2.5, 1.5),
    "PRU": (3, 1, 1, 1, 15, 0, 0, 0, 1.5, 1.0),
    "COL": (3, 1, 2, 1, 15, 0, 0, 0, 1.8, 1.6),
    "VEN": (2, 1, 2, 1, 20, 0, 0, 0, 3.3, 1.5),
    "ECU": (2, 0, 1, 0, 5, 0, 0, 0, 0.3, 0.3),
    "BOL": (2, 0, 1, 0, 5, 0, 0, 0, 0.6, 0.4),
    "PRY": (2, 0, 1, 0, 0, 0, 0, 0, 0.2, 0.2),
    "URY": (2, 1, 1, 1, 15, 0, 0, 0, 1.5, 1.3),
    "CUB": (2, 1, 2, 1, 20, 0, 0, 0, 2.5, 1.9),
    "HTI": (1, 0, 1, 0, 0, 0, 0, 0, 0.15, 0.15),
    "DOM": (1, 0, 1, 0, 3, 0, 0, 0, 0.3, 0.2),
    "HND": (1, 0, 1, 0, 3, 0, 0, 0, 0.2, 0.2),
    "NIC": (1, 0, 1, 0, 0, 0, 0, 0, 0.1, 0.1),
    "SLV": (1, 0, 1, 0, 3, 0, 0, 0, 0.2, 0.2),
    "GTM": (1, 0, 1, 0, 3, 0, 0, 0, 0.3, 0.3),
    "CRI": (1, 0, 1, 0, 3, 0, 0, 0, 0.2, 0.2),
    "PAN": (1, 0, 1, 0, 5, 0, 0, 0, 0.1, 0.4),
}


def economy_json(tag):
    """Objeto "economy" de countries.json para una etiqueta (None si no hay datos)."""
    values = ECONOMY_1936.get(tag)
    return dict(zip(ECONOMY_FIELDS, values)) if values else None
