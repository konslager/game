# IdleMMO — Idle MMORPG prototipas (C# / .NET 8)

## Telefone / naršyklėje
`web/index.html` – ta pati logika JavaScript'u, vienas failas, mobiliam ekranui. Progresas saugomas naršyklėje (localStorage), offline progresas skaičiuojamas atidarius iš naujo.
Regiono duomenys (`data/regions/emberwatch.json`) įterpiami į puslapį: pakeitus JSON paleiskite `python3 tools/embed_region.py`.

Web versija – **„Plyšio sargas“** (visas tekstas lietuviškai):
- **Tikslas:** užverti Plyšį. 7 skyriai (po vieną kiekvienai vietovei + finalas). Viršuje visada rodomas kitas žingsnis su mygtuku, kuris nuveda ten, kur reikia (`STORY` kode). Pirmas skyrius moko: užduotis → kova → rūda → kalvė → bosas.
- **Pradžia:** titulinis ekranas (Tęsti / Naujas herojus) → 5 klasės.
- **Miestas Žaržiūra:** nieko nevyksta savaime. Kairėje – rajonų ir pastatų navigacija (kalvė su lydymu ir stiprinimu, juvelyrė, ginklinė, vaistinė, medžiotojų namelis, kapitonės salė, skelbimų lenta, vartai).
- **Vietovė:** stovykloje renkiesi – kovoti, medžioti gyvūnus ar rinkti. Bosą iššauki pats, kai surenki 25 pergales. Offline progresas skaičiuojamas tik vietovėje.
- **Istorija:** 22 scenos su dialogais ir portretais (`SCENES`), 3 pasirinkimai (Izolda, Veksas, Ilsa), kurie keičia atlygius ir pabaigą. Veikėjai turi biografijas ir pokalbių frazes; vietovėse sutinkami brolis Aldas, vaidilutė Rūta, kapitonas Gedas, astronomė Eglė. Metraštis (16 įrašų) ir pasiekimai (14).
- **Pabaisos:** kiekviena vietovė turi savo pabaisas ir bosą (JSON `monsters`, išvaizda – `art`). Herojaus figūroje matoma dėvima apranga (šalmas, šarvų apvadai, batai, diržo sagtis, ginklo švytėjimas pagal retumą).
- **Portalų ratas** (Plyšio vartai): 5 animuoti portalai į atskirus pasaulius (Liepsnų plynė, Šerkšno karalystė, Grybų giria, Sapnų sala, Kraujo mėnulio kapinės) – savi fonai, priešai, bosai, „pasaulio“ apranga ir esmės. Portalai derinami gelmių kristalais ir ankstesnio pasaulio esme (`REALMS`, zonų id 7–11).
- **Požemiai** (Senoji šachta, Pelenynai): gylis po gylio, 4 kambariai gylyje su kelio pasirinkimu (kova, elitas, lobis, šaltinis, spąstai, šventovė), bosas kas 5 gylius. Gyvybė savaime neatsistato; grobis išsaugomas tik iškilus, žuvus – daiktai prarandami, auksas ir kristalai per pusę.
- **Bankas (Vartų aikštė):** auksą galima padėti ir pasiimti. Žuvus prarandama dalis kišenėje esančio aukso (vietovėje 10%, požemiuose 15%, pasaloje 20%), banke – niekada.
- **Įvykių dažnis:** Plyšio audra ir pasaulių įvykiai kaupiasi po 0,5% už pergalę (~200 kovų), medžioklė – 0,25%.
- **Kompiuterio ekranas:** nuo 900 px pločio – dviejų stulpelių išdėstymas (kairėje herojus, scena ir meniu, dešinėje turinys), didesnis šriftas ir scena.
- **Pasaulių įvykiai:** kiekviename portalo pasaulyje vietoj Plyšio audros – savas interaktyvus įvykis su savu bosu (liepsnų feniksas, ledo karalienė, sporų medūza, sapnų drugys, kraujo šikšnosparnis), fonu ir oru (Liepsnų liūtis, Didžioji pūga, Sporų žydėjimas, Košmaro naktis, Kraujo pilnatis; `STORM_THEMES`). Krūvis kaupiasi kovojant tame pasaulyje.
- **Bosų kaina auga:** po kiekvienos pergalės prieš bosą kitam iššūkiui reikia proporcingai daugiau pergalių (25 → 50 → 75 …; `bossNeed`).
- **Perėjimai:** ~5 s kosminė animacija (žvaigždžių tunelis, ūkai, blyksnis, pasaulio pavadinimas); paliesk, kad praleistum.
- **Plyšio audra:** interaktyvus slibino Vortrakso įvykis (vengimas, silpnos vietos, skrynia); finale – paskutinė audra užveria Plyšį.
- **Kuprinė:** 6 aprangos vietos aplink herojų, daiktai / medžiagos / gėrimai, daikto kortelė su palyginimu.

## Paleidimas
```bash
dotnet run --project src/IdleMMO.Cli                      # interaktyvus režimas (1 tick = 1 s)
dotnet run --project src/IdleMMO.Cli -- --speed 20        # 20 tick'ų per sekundę
dotnet run --project src/IdleMMO.Cli -- --sim 3600 --new  # headless: 1 val. simuliacija + santrauka
dotnet test                                               # unit testai
```
Parinktys: `--save <kelias>` (numatyta `save.json`), `--seed <int>`, `--new`.
Klavišai: **B** bosas · **U** auto-upgrade · **A** auto-boss on/off · **N/P** kita/ankstesnė zona · **X** salvage · **S** išsaugoti · **Q** išeiti.

## Core Loop
```
Auto-Battle (farm) → Loot & EXP → Gear upgrade / skills → Boss gate
   ├─ Laimėta → atrakinama nauja zona (auto-advance)
   └─ Pralaimėta → re-farm (KillsForBoss / 2) → bandymas iš naujo
```

## Struktūra
| Failas | Turinys |
|---|---|
| `src/IdleMMO.Core/Stats.cs` | `Attributes` (STR/AGI/INT/VIT/LUK), `DerivedStats` (dmg, def, crit, regen, attack speed), mitigacija |
| `src/IdleMMO.Core/Items.cs` | `Item`, `ItemTier` (Common→Legendary), `EquipSlot` (6 slotai), enhance/promote kainos |
| `src/IdleMMO.Core/Skills.cs` | `SkillDef`, `ClassDef`, Gladiator klasė (4 dmg + heal + shield, atrakinami pagal lygį) |
| `src/IdleMMO.Core/Player.cs` | lygiai, EXP kreivė, regen, cooldown'ai, auto-equip, enhance/promote |
| `src/IdleMMO.Core/World.cs` | `Monster`, `Zone`, `ZoneCatalog` (6 zonos), `LootTable` (svoriniai tier'ai) |
| `src/IdleMMO.Core/CombatAI.cs` | prioritetų medis: HP<30% heal → HP<50% shield → stipriausias skill → auto-attack |
| `src/IdleMMO.Core/GameSession.cs` | žaidimo ciklas, boss gate, zonų kelionės, `AutoUpgrade` |
| `src/IdleMMO.Core/Persistence.cs` | JSON save/load (atominis įrašymas), `OfflineProgress` |
| `src/IdleMMO.Cli/Program.cs` | konsolės UI, autosave kas 30 s |
| `tests/IdleMMO.Tests` | xUnit testai (AI, lygiai, įranga, bosai, save, offline) |

## Pagrindinės formulės (balanso rankenėlės)
- **EXP iki kito lygio:** `100 · L^2.2`
- **Žala:** `PhysDmg = 10 + 2·STR`; skill daugiklis; crit `5% + 0.2%·LUK` (max 60%), crit dmg `1.5 + 0.002·AGI`
- **Mitigacija:** `dmg · 100 / (100 + DEF)`, min 1
- **Item galia:** `base · TierMult · (1 + 0.1·Enh)`; TierMult = 1 / 1.5 / 2.2 / 3.2 / 4.5
- **Promote:** reikia +5 → tier+1, enhancement grįžta į 0 (Uncommon → Rare → … → Legendary)
- **Bosas:** 20× HP, 3× dmg, 15× atlygis; atsiranda po 25 nužudymų zonoje
- **Offline:** 300 tick'ų tikra simuliacija → išmatuotas EXP/gold greitis × likęs laikas × 0.6; max 12 val.; be loot/bosų

Simuliuotas tempas (seed 1): zona 2 ~6 min, zona 6 ~3 val.
