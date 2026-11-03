# IdleMMO — Idle MMORPG prototipas (C# / .NET 8)

## Telefone / naršyklėje
`web/index.html` – ta pati logika JavaScript'u, vienas failas, mobiliam ekranui. Progresas saugomas naršyklėje (localStorage), offline progresas skaičiuojamas atidarius iš naujo.
Regiono duomenys (`data/regions/emberwatch.json`) įterpiami į puslapį: pakeitus JSON paleiskite `python3 tools/embed_region.py`.

Web versija – **„Plyšio sargas“** (visas tekstas lietuviškai):
- **Tikslas:** užverti Plyšį. 7 skyriai (po vieną kiekvienai vietovei + finalas). Viršuje visada rodomas kitas žingsnis su mygtuku, kuris nuveda ten, kur reikia (`STORY` kode). Pirmas skyrius moko: užduotis → kova → rūda → kalvė → bosas.
- **Pradžia:** titulinis ekranas (Tęsti / Naujas herojus) → 5 klasės.
- **Miestas Žaržiūra:** nieko nevyksta savaime. Kairėje – rajonų ir pastatų navigacija (kalvė su lydymu ir stiprinimu, juvelyrė, ginklinė, vaistinė, medžiotojų namelis, kapitonės salė, skelbimų lenta, vartai).
- **Vietovė:** stovykloje renkiesi – kovoti, medžioti gyvūnus ar rinkti. Bosą iššauki pats, kai surenki 25 pergales. Offline progresas skaičiuojamas tik vietovėje.
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
