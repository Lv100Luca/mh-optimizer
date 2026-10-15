# Motion values (melee weapons)

Datamine for game version 1.0.11, from the community sheet linked in the GameFAQs thread "MH Wilds Motion Values (1.0.11 update)":
https://docs.google.com/spreadsheets/d/1Bine6S9MD6hjM85keFNyj9WbaeVb1ZVHYnhs93U3pVM (fetched 2026-10-07).

* **Motion Value** multiplies true raw as a percentage. **Element** / **Status** multiply true element / status for that attack (Wilds folds the old charge-level multipliers into these columns).
* **Sharpness** is sharpness consumed per hit; a color means the attack ignores sharpness and acts as that color.
* Numbers 1 / 2 in a name are hits within one input; "Power" is the damage under the attack's special condition.
* **Update (2026-10-07):** the author published a sheet for game version 1.040 (datamine 1.02.0, no balance changes in 1.030):
  https://docs.google.com/spreadsheets/d/1F_bOwQT6A6mFtCN7WCetnZDrmvLYu-ne4y97IsG6Fkk. The attack picker (`src/MHWildsOptimizer.Core/Damage/Attacks.cs`)
  uses its numbers; the Great Sword table below is still the 1.0.11 one (Long Sword is 1.040). Great Sword changes in 1.040 that the attacks use:
  Charged Slash Lv3 176 MV / element x1.5, Strong Charged Slash Lv3 187 / x1.8, True Charged Slash 2 Lv3 209 / x2.5 (power 267 / x2.5),
  Tackle Lv3 48 (no element, green). The Switch Axe table below is the 1.040 one: TU2 raised the element modifier of the Element-phial
  explosions from 0.35 to 0.8 (amped explosion 1.0).
* Checked on the training dummy (2026-10-07): Charged Slash Lv3 (160) and the normal TCS finisher (190) land within 2 % of the formula; the Dark Arts shockwave behaves as a fixed 30 MV hit.

## Great Sword

| Attack | Motion Value | Stun | Exhaust | Element | Status | Sharpness | Part Break | Notes |
|---|---|---|---|---|---|---|---|---|
| Charged Slash Lv0 | 78 | 0 | 0 | 1 | 1 | 1 |  |  |
| Charged Slash Lv1 | 101 | 0 | 0 | 1.1 | 1.1 | 1 |  |  |
| Charged Slash Lv2 | 129 | 0 | 0 | 1.2 | 1.2 | 1 |  |  |
| Charged Slash Lv3 | 160 | 0 | 0 | 1.3 | 1.3 | 1 | 1.2 |  |
| Wide Slash | 42 | 0 | 0 | 1 | 1 | 1 |  |  |
| Offset Rising Slash Lv0 | 38 | 0 | 0 | 1 | 1 | 1 |  | 75 Parry |
| Offset Rising Slash Lv1 | 94 | 0 | 0 | 1.1 | 1.1 | 1 |  | 100 Parry |
| Offset Rising Slash Lv2 | 121 | 0 | 0 | 1.2 | 1.2 | 1 |  | 150 Parry |
| Offset Rising Slash Lv3 | 151 | 0 | 0 | 1.3 | 1.3 | 1 | 1.2 | 200 Parry |
| Follow-up Cross Slash 1 Lv1 | 44 | 0 | 0 | 1 | 1 | 1 |  |  |
| Follow-up Cross Slash 2 Lv1 | 180 | 0 | 0 | 1 | 1 | 1 |  |  |
| Follow-up Cross Slash 1 Lv2 | 49 | 0 | 0 | 1 | 1 | 1 |  |  |
| Follow-up Cross Slash 2 Lv2 | 200 | 0 | 0 | 1 | 1 | 1 |  |  |
| Follow-up Cross Slash 1 Lv3 | 58 | 0 | 0 | 1 | 1 | 1 | 1.2 |  |
| Follow-up Cross Slash 2 Lv3 | 230 | 0 | 0 | 1 | 1 | 1 |  |  |
| Kick | 5 | 10 | 0 | 1 | 1 | Yellow |  | Deals Hitzone-ignoring damage |
| Side Blow | 16 | 20 | 15 | 1 | 1 | 1 |  | Deals Blunt damage |
| Strong Charged Slash Lv0 (Unavailable) | 80 | 0 | 0 | 1 | 1 | 1 |  |  |
| Strong Charged Slash Lv1 | 108 | 0 | 0 | 1.2 | 1.2 | 1 |  |  |
| Strong Charged Slash Lv2 | 140 | 0 | 0 | 1.3 | 1.3 | 1 |  |  |
| Strong Charged Slash Lv3 | 176 | 0 | 0 | 1.4 | 1.4 | 1 | 1.2 |  |
| Strong Wide Slash Lv0 (Unavailable) | 72 | 0 | 0 | 1 | 1 | 1 |  |  |
| Strong Wide Slash Lv1 | 87 | 0 | 0 | 1.5 | 1.5 | 1 |  |  |
| Strong Wide Slash Lv2 | 105 | 0 | 0 | 1.7 | 1.7 | 1 |  |  |
| Strong Wide Slash Lv3 | 130 | 0 | 0 | 2 | 2 | 1 | 1.2 |  |
| Tackle Lv0 | 23 | 30 | 20 | 0 | 0 | Green |  | Deals Blunt damage |
| Tackle Lv1 | 26 | 30 | 20 | 0 | 0 | Green |  | Deals Blunt damage |
| Tackle Lv2 | 35 | 40 | 25 | 0 | 0 | Green |  | Deals Blunt damage |
| Tackle Lv3 | 48 | 55 | 30 | 0 | 0 | Green | 1.2 | Deals Blunt damage |
| Leaping Wide Slash Lv0 | 88 | 0 | 0 | 1 | 1 | 1 |  |  |
| Leaping Wide Slash Lv1 | 106 | 0 | 0 | 2 | 2 | 1 |  |  |
| Leaping Wide Slash Lv2 | 128 | 0 | 0 | 2.5 | 2.5 | 1 |  |  |
| Leaping Wide Slash Lv3 | 159 | 0 | 0 | 3.1 | 3.1 | 1 | 1.5 |  |
| True Charged Slash 1 Lv0 (Unavailable) | 10 | 0 | 0 | 1 | 1 | 1 |  |  |
| True Charged Slash 1 Lv1 | 12 | 0 | 0 | 1 | 1 | 1 |  |  |
| True Charged Slash 1 Lv2 | 14 | 0 | 0 | 1 | 1 | 1 |  |  |
| True Charged Slash 1 Lv3 | 16 | 0 | 0 | 1 | 1 | 1 |  |  |
| True Charged Slash 2 Lv0 (Unavailable) | 82 | 0 | 0 | 1 | 1 | 1 |  |  |
| True Charged Slash 2 Lv1 | 102 | 0 | 0 | 1.3 | 1.3 | 1 |  |  |
| True Charged Slash 2 Lv2 | 149 | 0 | 0 | 1.4 | 1.4 | 1 | 1 |  |
| True Charged Slash 2 Lv3 | 190 | 0 | 0 | 1.5 | 1.5 | 1 | 1.5 |  |
| Power True Charged Slash 2 Lv0 (Unavailable) | 90 | 0 | 0 | 1 | 1 | 1 | 1 |  |
| Power True Charged Slash 2 Lv1 | 121 | 0 | 0 | 1.3 | 1.3 | 1 | 1 |  |
| Power True Charged Slash 2 Lv2 | 181 | 0 | 0 | 1.4 | 1.4 | 1 | 1 |  |
| Power True Charged Slash 2 Lv3 | 241 | 0 | 0 | 1.5 | 1.5 | 1 | 1.5 |  |
| Jumping Charged Slash Lv0 | 48 | 0 | 0 | 1 | 1 | 1 | 1 |  |
| Jumping Charged Slash Lv1 | 58 | 0 | 0 | 1.1 | 1.1 | 1 | 1 |  |
| Jumping Charged Slash Lv2 | 69 | 0 | 0 | 1.2 | 1.2 | 1 | 1 |  |
| Jumping Charged Slash Lv3 | 87 | 0 | 0 | 1.3 | 1.3 | 1 | 1.2 |  |
| Charged Rising Slash Lv0 | 48 | 0 | 0 | 1 | 1 | 1 | 1 |  |
| Charged Rising Slash Lv1 | 48 | 0 | 0 | 1 | 1 | 1 | 1 |  |
| Charged Rising Slash Lv2 | 72 | 0 | 0 | 1 | 1 | 1 | 1 |  |
| Charged Rising Slash Lv3 | 98 | 0 | 0 | 1 | 1 | 1 | 1.2 |  |
| Plunging Thrust Lv0 | 36 | 0 | 0 | 0.4 | 0.4 | 0.2 | 1 |  |
| Plunging Thrust Lv1 | 42 | 0 | 0 | 0.4 | 0.4 | 0.2 | 1 |  |
| Plunging Thrust Lv2 | 50 | 0 | 0 | 0.4 | 0.4 | 0.2 | 1 |  |
| Plunging Thrust Lv3 | 58 | 0 | 0 | 0.4 | 0.4 | 0.2 | 1.2 |  |
| Scaling Drop Slash 1 | 20 | 0 | 0 | 1 | 1 | 1 | 1 |  |
| Scaling Drop Slash 2 | 45 | 0 | 0 | 1 | 1 | 1 | 1 |  |
| Seikret Attack I | 15 | 0 | 0 | 1 | 1 | 1 | 1 |  |
| Seikret Attack II | 10 | 0 | 0 | 1 | 1 | 1 | 1 |  |
| Dismount Attack 1 | 20 | 0 | 0 | 1 | 1 | 1 | 1 |  |
| Dismount Attack 2 | 50 | 0 | 0 | 1 | 1 | 1 | 1 |  |
| Focus Strike: Perforate Lv0 | 15 | 0 | 0 | 0.4 | 0.4 | 0.3 | 1 | Deals multiple hits until it finds a wound |
| Focus Strike: Perforate Lv1 | 20 | 0 | 0 | 0.4 | 0.4 | 0.3 | 1 | Deals multiple hits until it finds a wound |
| Focus Strike: Perforate Lv2 | 26 | 0 | 0 | 0.4 | 0.4 | 0.3 | 1 | Deals multiple hits until it finds a wound |
| Focus Strike: Perforate Lv3 | 33 | 0 | 0 | 0.4 | 0.4 | 0.3 | 1 | Deals multiple hits until it finds a wound |
| Focus Strike: Perforate Lv0 Extra Hit | 14 | 0 | 0 | 0.1 | 0.1 | 0 | 1 |  |
| Focus Strike: Perforate Lv1 Extra Hit | 16 | 0 | 0 | 0.1 | 0.1 | 0 | 1 |  |
| Focus Strike: Perforate Lv2 Extra Hit | 18 | 0 | 0 | 0.1 | 0.1 | 0 | 1 |  |
| Focus Strike: Perforate Lv3 Extra Hit | 24 | 0 | 0 | 0.1 | 0.1 | 0 | 1 |  |
| Focus Strike: Perforate Lv0 Multihit | 10 | 0 | 0 | 0.1 | 0.1 | 0 | 1 | Each successive attack deals less damage than the previous one |
| Focus Strike: Perforate Lv1 Multihit | 11 | 0 | 0 | 0.1 | 0.1 | 0 | 1 | Each successive attack deals less damage than the previous one |
| Focus Strike: Perforate Lv2 Multihit | 12 | 0 | 0 | 0.1 | 0.1 | 0 | 1 | Each successive attack deals less damage than the previous one |
| Focus Strike: Perforate Lv3 Multihit | 13 | 0 | 0 | 0.1 | 0.1 | 0 | 1 | Each successive attack deals less damage than the previous one |
| Focus Strike: Perforate Lv0 Finisher 1 | 15 | 0 | 0 | 0.1 | 0.1 | 0 | 1 |  |
| Focus Strike: Perforate Lv1 Finisher 1 | 20 | 0 | 0 | 0.1 | 0.1 | 0 | 1 |  |
| Focus Strike: Perforate Lv2 Finisher 1 | 22 | 0 | 0 | 0.1 | 0.1 | 0 | 1 |  |
| Focus Strike: Perforate Lv3 Finisher 1 | 24 | 0 | 0 | 0.1 | 0.1 | 0 | 1 |  |
| Focus Strike: Perforate Lv0 Finisher 2 | 6 | 0 | 0 | 0.1 | 0.1 | 0 | 1 |  |
| Focus Strike: Perforate Lv1 Finisher 2 | 6 | 0 | 0 | 0.1 | 0.1 | 0 | 1 |  |
| Focus Strike: Perforate Lv2 Finisher 2 | 6 | 0 | 0 | 0.1 | 0.1 | 0 | 1 |  |
| Focus Strike: Perforate Lv3 Finisher 2 | 6 | 0 | 0 | 0.1 | 0.1 | 0 | 1 |  |
| Focus Strike: Perforate Lv0 Finisher 3 | 2 | 0 | 0 | 0.1 | 0.1 | 0 | 1 |  |
| Focus Strike: Perforate Lv1 Finisher 3 | 2 | 0 | 0 | 0.1 | 0.1 | 0 | 1 |  |
| Focus Strike: Perforate Lv2 Finisher 3 | 2 | 0 | 0 | 0.1 | 0.1 | 0 | 1 |  |
| Focus Strike: Perforate Lv3 Finisher 3 | 2 | 0 | 0 | 0.1 | 0.1 | 0 | 1 |  |
| Aerial Focus Strike Lv0 | 15 | 0 | 0 | 0.4 | 0.4 | 0.3 | 1 | Deals multiple hits until it finds a wound |
| Aerial Focus Strike Lv1 | 20 | 0 | 0 | 0.4 | 0.4 | 0.3 | 1 | Deals multiple hits until it finds a wound |
| Aerial Focus Strike Lv2 | 26 | 0 | 0 | 0.4 | 0.4 | 0.3 | 1 | Deals multiple hits until it finds a wound |
| Aerial Focus Strike Lv3 | 33 | 0 | 0 | 0.4 | 0.4 | 0.3 | 1 | Deals multiple hits until it finds a wound |
| Aerial Focus Strike Extra Hit Lv0 | 14 | 0 | 0 | 0.1 | 0.1 | 0 | 1 | Proceeds into Focus Strike Multihit |
| Aerial Focus Strike Extra Hit Lv1 | 16 | 0 | 0 | 0.1 | 0.1 | 0 | 1 | Proceeds into Focus Strike Multihit |
| Aerial Focus Strike Extra Hit Lv2 | 18 | 0 | 0 | 0.1 | 0.1 | 0 | 1 | Proceeds into Focus Strike Multihit |
| Aerial Focus Strike Extra Hit Lv3 | 24 | 0 | 0 | 0.1 | 0.1 | 0 | 1 | Proceeds into Focus Strike Multihit |
| Clash Finisher | 90 | 0 | 0 | 1 | 1 | 1 | 1 |  |
| Mount Attack | 90 | 0 | 0 | 1 | 1 | 0.1 | 1 |  |
| Mount Finisher 1 | 120 | 0 | 0 | 1 | 1 | 0.1 | 1 |  |
| Mount Finisher Multihit | 10 | 0 | 0 | 1 | 1 | 0.1 | 1 |  |
| Mount Finisher 2 | 60 | 0 | 0 | 1 | 1 | 0.1 | 1 |  |
| Sneak Attack | 106 | 0 | 0 | 1 | 1 | 0.1 | 1 | Deals hitzone-ignoring damage |
| Aerial Sneak Attack | 106 | 0 | 0 | 1 | 1 | 0.1 | 1 | Deals hitzone-ignoring damage |

## Long Sword (1.040)

From the 1.040 sheet (gid 646073038). The Spirit Gauge multiplies raw: x1.02 white, x1.04 yellow, x1.1 red (Game8, Icy Veins; not datamined).
Crimson moves only exist at a red gauge; the notes say which hits use a lower gauge multiplier (Helm Breaker, Spirit Release Slash).
The red-gauge loops (Game8): Crimson Slash I into Rising Slash (left click, right click), or into Spirit Blade I (left click, R).

| Attack | Motion Value | Mount | Element | Status | Sharpness | Part Break | Gauge generation | Gauge cost | Notes |
|---|---|---|---|---|---|---|---|---|---|
| Overhead Slash I | 24 | 0 | 1 | 1 | 1 | 1 | 26 | 0 |  |
| Overhead Slash II | 18 | 0 | 1 | 1 | 1 | 1 | 30 | 0 |  |
| Crescent Slash | 32 | 0 | 1 | 1 | 1 | 1 | 23 | 0 |  |
| Crimson Slash I 1 | 33 | 0 | 0.5 | 0.5 | 0.5 | 1 | 10 | 0 |  |
| Crimson Slash I 2 | 16 | 0 | 0.5 | 0.5 | 0.5 | 1 | 16 | 0 |  |
| Crimson Slash II 1 | 11 | 0 | 1.05 | 0.7 | 0.5 | 1 | 10 | 0 |  |
| Crimson Slash II 2 | 22 | 0 | 1.05 | 0.7 | 0.5 | 1 | 20 | 0 |  |
| Crimson Slash III 1 | 30 | 0 | 1.1 | 0.8 | 0.3 | 1 | 5 | 0 |  |
| Crimson Slash III 2 | 18 | 0 | 1.1 | 0.8 | 0.3 | 1 | 8 | 0 |  |
| Crimson Slash III 3 | 55 | 0 | 1.1 | 0.8 | 0.3 | 1 | 10 | 0 |  |
| Thrust | 18 | 0 | 1 | 1 | 1 | 1 | 23 | 0 |  |
| Rising Slash | 18 | 0 | 1 | 1 | 1 | 1 | 23 | 0 |  |
| Fade Slash | 22 | 0 | 1 | 1 | 1 | 1 | 50 | 0 |  |
| Spirit Blade I | 31 | 0 | 1 | 1 | 1 | 1 | 0 | 10 |  |
| Spirit Blade I (No Gauge) | 14 | 0 | 1 | 1 | 1 | 1 | 10 | 0 |  |
| Directional Spirit Blade I 1 | 31 | 0 | 1 | 1 | 1 | 1 | 0 | 10 |  |
| Directional Spirit Blade I 1 (No Gauge) | 14 | 0 | 1 | 1 | 1 | 1 | 10 | 0 |  |
| Spirit Blade II | 30 | 0 | 1 | 1 | 1 | 1 | 0 | 15 |  |
| Spirit Blade II (No Gauge) | 14 | 0 | 1 | 1 | 1 | 1 | 0 | 15 |  |
| Directional Spirit Blade II 1 | 14 | 0 | 1 | 1 | 0.5 | 1 | 0 | 15 |  |
| Directional Spirit Blade II 2 | 26 | 0 | 1 | 1 | 0.5 | 1 | 0 | 0 |  |
| Directional Spirit Blade II 1 (No Gauge) | 8 | 0 | 1 | 1 | 0.5 | 1 | 0 | 15 |  |
| Directional Spirit Blade II 2 (No Gauge) | 18 | 0 | 1 | 1 | 0.5 | 1 | 0 | 0 |  |
| Spirit Blade III 1 | 14 | 0 | 1 | 1 | 0.3 | 1 | 0 | 25 |  |
| Spirit Blade III 2 | 19 | 0 | 1 | 1 | 0.3 | 1 | 0 | 0 |  |
| Spirit Blade III 3 | 34 | 0 | 1 | 1 | 0.3 | 1 | 0 | 0 |  |
| Spirit Blade III 1 (No Gauge) | 8 | 0 | 1 | 1 | 0.3 | 1 | 0 | 25 |  |
| Spirit Blade III 2 (No Gauge) | 11 | 0 | 1 | 1 | 0.3 | 1 | 0 | 0 |  |
| Spirit Blade III 3 (No Gauge) | 19 | 0 | 1 | 1 | 0.3 | 1 | 0 | 0 |  |
| Spirit Roundslash | 38 | 0 | 1 | 1 | 1 | 1 | 0 | 20 | Raises Spirit Gauge level by 1. |
| Spirit Roundslash (No Gauge) | 19 | 0 | 1 | 1 | 1 | 1 | 0 | 20 | Raises Spirit Gauge level by 1. |
| Spirit Roundslash (Red) | 42 | 0 | 1 | 1 | 1 | 1 | 10/5/3/0 | 0 | Gauge generation depends on how many times it has been used during this Red Spirit Gauge period |
| Spinning Crimson Slash | 60 | 0 | 1 | 1 | 1 | 1 | 10/5/3/0 | 0 | Gauge generation depends on how many times it has been used during this Red Spirit Gauge period |
| Spirit Step Slash | 28 | 0 | 1 | 1 | 1 | 1 | 0 | 20 |  |
| Spirit Step Slash (No Gauge) | 18 | 0 | 1 | 1 | 1 | 1 | 10 | 0 |  |
| Jumping Slash | 26 | 30 | 1 | 1 | 1 | 1 | 30 | 0 |  |
| Jumping Spirit Blade I | 30 | 40 | 1 | 1 | 1 | 1 | 0 | 15 |  |
| Jumping Spirit Blade I (No Gauge) | 16 | 40 | 1 | 1 | 1 | 1 | 0 | 15 |  |
| Jumping Spirit Blade II 1 | 12 | 30 | 1 | 1 | 0.5 | 1 | 0 | 20 |  |
| Jumping Spirit Blade II 2 | 26 | 30 | 1 | 1 | 0.5 | 1 | 0 | 0 |  |
| Jumping Spirit Blade II 1 (No Gauge) | 8 | 30 | 1 | 1 | 0.5 | 1 | 0 | 20 |  |
| Jumping Spirit Blade II 2 (No Gauge) | 15 | 30 | 1 | 1 | 0.5 | 1 | 0 | 0 |  |
| Jumping Spirit Blade III 1 | 12 | 30 | 1 | 1 | 0.3 | 1 | 0 | 20 |  |
| Jumping Spirit Blade III 2 | 22 | 30 | 1 | 1 | 0.3 | 1 | 0 | 0 |  |
| Jumping Spirit Blade III 3 (Not used?) | 24 | 20 | 1 | 1 | 0.3 | 1 | 0 | 0 |  |
| Jumping Spirit Blade III 1 (No Gauge) | 7 | 30 | 1 | 1 | 0.3 | 1 | 0 | 20 | Not available |
| Jumping Spirit Blade III 2 (No Gauge) | 13 | 30 | 1 | 1 | 0.3 | 1 | 0 | 0 | Not available |
| Jumping Spirit Blade III 3 (No Gauge) | 13 | 20 | 1 | 1 | 0.3 | 1 | 0 | 0 | Used even when you have gauge? |
| Jumping Rising Slash | 20 | 30 | 1 | 1 | 1 | 1 | 30 | 0 |  |
| Aerial Draw Spirit Blade | 48 | 65 | 1 | 1 | 1 | 1 | 0 | 20 |  |
| Aerial Draw Spirit Blade (No Gauge) | 26 | 65 | 1 | 1 | 1 | 1 | 35 | 20 |  |
| Foresight Slash | 18 | 0 | 1 | 1 | 1 | 1 | 0 | 100 |  |
| Foresight Slash (No Gauge) | 11 | 0 | 1 | 1 | 1 | 1 | 10 | 100 |  |
| Foresight Slash (Success) | 23.4 | 0 | 1 | 1 | 1 | 1 | 100 | 100 | Actually just a 1.3x multiplier. |
| Foresight Whirl Slash 1 | 22 | 0 | 1 | 1 | 0.5 | 1 | 0 | 8 |  |
| Foresight Whirl Slash 2 | 13 | 0 | 1 | 1 | 0.5 | 1 | 0 | 0 |  |
| Foresight Whirl Slash 1 (Sucessful) | 28.6 | 0 | 1 | 1 | 0.5 | 1 | 4 | 8 | Actually just a 1.3x multiplier |
| Foresight Whirl Slash 2 (Successful) | 16.9 | 0 | 1 | 1 | 0.5 | 1 | 0 | 0 | Actually just a 1.3x multiplier |
| Iai Slash 1 | 18 | 0 | 1 | 1 | 0.5 | 1 | 0 | 0 |  |
| Iai Slash 2 | 13 | 0 | 1 | 1 | 0.5 | 1 | 0 | 0 |  |
| Iai Spirit Slash 1 (None) | 17 | 0 | 1 | 1 | 1 | 1.5 | 0 | 0 | Increases Spirit Gauge level by 1 on a successful counter |
| Iai Spirit Slash 1 (White) | 19 | 0 | 1 | 1 | 1 | 1.5 | 0 | 0 | Increases Spirit Gauge level by 1 on a successful counter |
| Iai Spirit Slash 2 (White) | 55 | 0 | 1 | 1 | 0 | 1.5 | 0 | 0 | Only on a successful counter |
| Iai Spirit Slash 1 (Yellow) | 31 | 0 | 1 | 1 | 1 | 1.5 | 0 | 0 | Increases Spirit Gauge level by 1 on a successful counter |
| Iai Spirit Slash 2 (Yellow) | 72 | 0 | 1 | 1 | 0 | 1.5 | 0 | 0 | Only on a successful counter |
| Iai Spirit Slash 1 (Red) | 39 | 0 | 1 | 1 | 1 | 1.5 | 0 | 0 |  |
| Iai Spirit Slash 2 (Red) | 86 | 0 | 1 | 1 | 0 | 1.5 | 0 | 0 | Only on a successful counter |
| Spirit Thrust | 19 | 0 | 1 | 1 | 1 | 1 | 0 | 0 |  |
| Spirit Helmbreaker (White) | 15 | 0 | 0.2 | 0.2 | 0 | 0.9 | 0 | 0 | Hits 7 times. Last hit has a Part Break multiplier of 1. Uses no gauge multiplier. |
| Spirit Helmbreaker (Yellow) | 20 | 0 | 0.2 | 0.2 | 0 | 0.9 | 0 | 0 | Hits 7 times. Last hit has a Part Break multiplier of 1. Uses White gauge multiplier. |
| Spirit Helmbreaker (Red) | 23 | 0 | 0.2 | 0.2 | 0 | 0.9 | 0 | 0 | Hits 7 times. Last hit has a Part Break multiplier of 1. Uses Yellow gauge multiplier. |
| Spirit Release Slash 1 (x2) | 5 | 0 | 1 | 1 | 0.3 | 1 | 0 | 0 | Uses Yellow Gauge multiplier |
| Spirit Release Slash 2 (x3) | 10 | 0 | 0.1 | 0.1 | 0 | 1.5 | 10 | 0 | Uses White Gauge multiplier |
| Spirit Release Slash 3 (x3) | 15 | 0 | 0.1 | 0.1 | 0 | 1.5 | 10 | 0 | Uses White Gauge multiplier |
| Spirit Release Slash 4 (x3) | 22 | 0 | 0.1 | 0.1 | 0 | 1.5 | 10 | 0 | Uses White Gauge multiplier |
| Spirit Release Slash 5 (x3) | 35 | 0 | 0.1 | 0.1 | 0 | 3 | 10 | 0 | Uses White Gauge multiplier |
| Seikret Attack I 1 | 15 | 0 | 1 | 1 | 0.5 | 1 | 10 | 0 |  |
| Seikret Attack I 2 | 10 | 0 | 1 | 1 | 0.5 | 1 | 5 | 0 |  |
| Seikret Attack II | 20 | 0 | 1 | 1 | 1 | 1 | 20 | 0 |  |
| Dismount Attack 1 | 30 | 30 | 1 | 1 | 1 | 1 | 25 | 0 | Increases Spirit Gauge level by 1 on a successful counter |
| Dismount Attack 2 | 15 | 0 | 1 | 1 | 1 | 1 | 10 | 0 |  |
| Focus Strike: Unbound Thrust 1 | 26 | 0 | 1 | 1 | 1 | 1 | 10 | 0 |  |
| Focus Strike: Unbound Thrust Extra Hit | 26 | 0 | 1 | 1 | 1 | 1 | 0 | 0 |  |
| Focus Strike: Unbound Thrust 2 | 10 | 0 | 1 | 1 | 1 | 1 | 0 | 0 |  |
| Focus Strike: Unbound Thrust 3 | 40 | 0 | 1 | 1 | 1 | 1 | 0 | 0 |  |
| Focus Strike: Unbound Thrust 4 | 40 | 0 | 1 | 1 | 1 | 1 | 0 | 0 |  |
| Focus Strike: Unbound Thrust 5 | 40 | 0 | 1 | 1 | 0 | 1 | 0 | 0 | Applies once per wound. Increases Spirit Gauge level by 1. Uses post-level up modifiers |
| Mount Attack 1 | 23 | 0 | 1 | 1 | 0.1 | 1 | 5 | 0 |  |
| Mount Attack 2 | 20 | 0 | 1 | 1 | 0.1 | 1 | 5 | 0 |  |
| Mount Finisher 1 | 70 | 0 | 1 | 1 | 0.1 | 1 | 10 | 0 |  |
| Mount Finisher 2 | 100 | 0 | 1 | 1 | 0.1 | 1 | 10 | 0 |  |
| Sneak Attack 1 | 50 | 0 | 1 | 1 | 0.5 | 1 | 10 | 0 | Deals hitzone-ignoring damage |
| Sneak Attack 2 | 56 | 0 | 1 | 1 | 0.5 | 1 | 10 | 0 | Deals hitzone-ignoring damage |
| Aerial Sneak Attack 1 | 50 | 0 | 1 | 1 | 0.5 | 1 | 10 | 0 | Deals hitzone-ignoring damage |
| Aerial Sneak Attack 2 | 56 | 0 | 1 | 1 | 0.5 | 1 | 10 | 0 | Deals hitzone-ignoring damage |
| Underwater Attack 1 | 40 | 0 | 0.7 | 0.7 | 1 | 1 | 5 | 0 |  |
| Underwater Attack 2 | 42 | 0 | 0.7 | 0.7 | 1 | 1 | 5 | 0 |  |
| Underwater Attack 3 | 60 | 0 | 0.7 | 0.7 | 1 | 1 | 10 | 0 |  |

## Switch Axe (1.040)

Element "0.35/0.8": Power and Dragon phials 0.35, Element phial 0.8. Gauge columns: Switch gauge, Amped gauge, gauge cost.

| Attack | Motion Value | Mount | Element | Status | Sharpness | Part Break | Can Bounce | Switch Gauge generation | Amped Gauge generation | Gauge cost | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Axe Overhead Slash | 45 | 0 | 1 | 1 | 1 | 1 | Yes | 10 | 0 | 0 |  |
| Axe Side Slash | 23 | 0 | 1 | 1 | 1 | 1 | Yes | 7 | 0 | 0 |  |
| Axe Spiral Burst Slash 1 | 39 | 0 | 1 | 1 | 0.5 | 1 | Yes | 15 | 0 | 0 |  |
| Axe Spiral Burst Slash 2 | 50 | 0 | 1 | 1 | 0.5 | 1 | Yes | 20 | 0 | 0 |  |
| Axe Forward Slash | 35 | 0 | 1 | 1 | 1 | 1 | Yes | 10 | 0 | 0 |  |
| Axe Forward Overhead Slash | 45 | 0 | 1 | 1 | 1 | 1 | Yes | 10 | 0 | 0 |  |
| Axe Fade Slash | 42 | 0 | 1 | 1 | 1 | 1 | Yes | 12 | 0 | 0 |  |
| Axe Offset Rising Slash | 50 | 0 | 1 | 1 | 1 | 1 | Yes | 7 | 0 | 0 | 100 Parry |
| Axe Follow-up Heavy Slam 1 | 32 | 0 | 1 | 1 | 0.5 | 1 | No | 15 | 0 | 0 |  |
| Axe Follow-up Heavy Slam 2 | 90 | 0 | 1 | 1 | 0.5 | 1 | No | 35 | 0 | 0 | Activates Power Axe |
| Axe Follow-up Morph Slash | 123 | 0 | 1 | 1 | 1 | 1 | No | 0 | 38 | 20 |  |
| Axe Follow-up Morph Slash Explosion | 120 | 0 | 0.35/0.8 | 0.35 | 0 | 1 | No | 0 | 0 | 0 | Status Phials have guaranteed application. Exhaust Phials get a 0.2 modifier to both Exhaust and Stun. Dragon Phials and Power Phials get a 0.35x elemental modifier, while Elemental Phials get a 0.8x elemental modifiier |
| Axe Wild Slash Left | 23 | 0 | 1 | 1 | 1 | 1 | Yes | 3 | 0 | 0 |  |
| Axe Wild Slash Right | 21 | 0 | 1 | 1 | 1 | 1 | Yes | 3 | 0 | 0 |  |
| Axe Heavy Slam 1 | 15 | 0 | 1 | 1 | 0.5 | 1 | Yes | 8 | 0 | 0 |  |
| Axe Heavy Slam 2 | 72 | 0 | 1 | 1 | 0.5 | 1 | Yes | 14 | 0 | 0 | Activates Power Axe |
| Axe Morph Sweep 1 | 20 | 0 | 1 | 1 | 0.3 | 1 | Yes | 5 | 0 | 0 |  |
| Axe Morph Sweep 2 | 70 | 0 | 1 | 1 | 0.3 | 1 | Yes | 7 | 0 | 0 |  |
| Axe Morph Sweep 3 | 35 | 0 | 1 | 1 | 0.4 | 1 | Yes | 10 | 0 | 0 |  |
| Axe Morph Slash | 34 | 0 | 1 | 1 | 1 | 1 | No | 0 | 7 | 7 |  |
| Axe Morph Rising Double Slash 1 | 37 | 0 | 1 | 1 | 0.5 | 1 | No | 0 | 15 | 6 |  |
| Axe Morph Rising Double Slash 2 | 33 | 0 | 1 | 1 | 0.5 | 1 | No | 0 | 10 | 0 |  |
| Sword Overhead Slash | 40 | 0 | 1 | 1 | 1 | 1 | No | 0 | 7 | 5 |  |
| Sword Right Rising Slash | 58 | 0 | 1 | 1 | 1 | 1 | No | 0 | 7 | 4 |  |
| Sword Left Rising Slash | 62 | 0 | 1 | 1 | 1 | 1 | No | 0 | 7 | 4 |  |
| Sword Triple Slash 1 | 25 | 0 | 1 | 1 | 0.5 | 1 | No | 0 | 12 | 0 |  |
| Sword Double Slash 1/Triple Slash 2 | 22 | 0 | 1 | 1 | 0.5 | 1 | No | 0 | 10 | 10 |  |
| Sword Double Slash 2/Triple Slash 3 | 26 | 0 | 1 | 1 | 0.5 | 1 | No | 0 | 18 | 0 |  |
| Sword Heavenward Flurry 1 | 34 | 0 | 1 | 1 | 0.5 | 1 | No | 0 | 17 | 10 |  |
| Sword Heavenward Flurry 2 | 46 | 0 | 1 | 1 | 0.5 | 1 | No | 0 | 28 | 0 |  |
| Elemental Discharge Start | 25 | 0 | 1 | 1 | 1 | 1 | No | 0 | 7 | 10 |  |
| Elemental Discharge Ticks | 12 | 0 | 1 | 0.7 | 0.2 | 0.1 | No | 0 | 1 | 0 | Exhaust Phials get a 0.3 modifier to both Exhaust and Stun |
| Elemental Discharge Explosion | 160 | 0 | 1 | 1 | 0 | 1 | No | 0 | 30 | 20 | Status Phials have guaranteed application |
| Elemental Discharge Early Explosion | 75 | 0 | 1 | 1 | 0 | 1 | No | 0 | 20 | 20 | Status Phials have guaranteed application |
| Zero Sum Discharge Explosion | 175 | 0 | 1 | 1 | 0 | 1 | No | 0 | 20 | 20 | Status Phials have guaranteed application |
| Zero Sum Discharge Early Explosion | 80 | 0 | 1 | 1 | 0 | 1 | No | 0 | 20 | 20 | Status Phials have guaranteed application |
| Sword Counter Rising Slash 1 | 28 | 0 | 1 | 1 | 1 | 1 | No | 0 | 7 | 15 |  |
| Sword Counter Rising Slash 1 (Success) | 37 | 0 | 1 | 1 | 0.5 | 1 | No | 0 | 15 | 15 |  |
| Sword Counter Rising Slash 2 | 45 | 0 | 1 | 1 | 0.5 | 1 | No | 0 | 23 | 0 |  |
| Unbridled Slash | 85 | 0 | 1 | 1 | 1 | 1 | No | 0 | 25 | 20 |  |
| Unbridled Slash Explosion (x3) | 25 | 0 | 0.35/0.8 | 0.35 | 0 | 1 | No | 0 | 0 | 0 | Status Phials have guaranteed application. Exhaust Phials get a 0.2 modifier to both Exhaust and Stun. Dragon Phials and Power Phials get a 0.35x elemental modifier, while Elemental Phials get a 0.8x elemental modifiier |
| Full Release Slash 1 | 50 | 0 | 1 | 1 | 1 | 1 | No | 0 | 7 | 0 |  |
| Full Release Slash 2 | 82 | 0 | 1 | 1 | 1 | 1 | No | 0 | 28 | 40 |  |
| Full Release Explosion 1 (x3) | 20 | 0 | 0.35/0.8 | 0.35 | 0 | 1 | No | 0 | 0 | 0 | Status Phials have guaranteed application. Exhaust Phials get a 0.2 modifier to both Exhaust and Stun. Dragon Phials and Power Phials get a 0.35x elemental modifier, while Elemental Phials get a 0.8x elemental modifiier |
| Full Release Explosion 2 (x5) | 35 | 0 | 0.35/0.8 | 0.35 | 0 | 1 | No | 0 | 0 | 0 | Status Phials have guaranteed application. Exhaust Phials get a 0.2 modifier to both Exhaust and Stun. Dragon Phials and Power Phials get a 0.35x elemental modifier, while Elemental Phials get a 0.8x elemental modifiier |
| Amped Explosion | 12 | 0 | 0.35/1 | 0.35 | 0 | 1 | No | 0 | 0 | 0 | Status Phials have guaranteed application. Exhaust Phials get a 0.2 modifier to both Exhaust and Stun. Dragon Phials and Power Phials get a 0.35x elemental modifier, while Elemental Phials get a 1x elemental modifiier |
| Sword Morph Slash | 23 | 0 | 1 | 1 | 1 | 1 | Yes | 5 | 0 | 0 |  |
| Sword Advancing Morph Slash | 55 | 0 | 1 | 1 | 1 | 1 | Yes | 5 | 0 | 0 |  |
| Sword Overhead Morph Slash | 45 | 0 | 1 | 1 | 1 | 1 | Yes | 3 | 0 | 0 |  |
| Sword Downward Fade Slash | 55 | 0 | 1 | 1 | 1 | 1 | Yes | 5 | 0 | 0 |  |
| Sword Morph Double Slash 1 | 40 | 0 | 1 | 1 | 0.5 | 1 | Yes | 4 | 0 | 0 |  |
| Sword Morph Double Slash 2 | 60 | 0 | 1 | 1 | 0.5 | 1 | Yes | 5 | 0 | 0 |  |
| Axe Jumping Slash | 45 | 55 | 1 | 1 | 1 | 1 | No | 14 | 0 | 0 |  |
| Sword Jumping Slash | 29 | 50 | 1 | 1 | 1 | 1 | No | 0 | 15 | 7 |  |
| Axe Jumping Sweep/Scaling Wide Slash/Sword Scaling Double Slash | 45 | 45 | 1 | 1 | 1 | 1 | No | 14 | 0 | 0 |  |
| Sword Jumping Rising Slash | 29 | 40 | 1 | 1 | 1 | 1 | No | 0 | 15 | 7 |  |
| Sword Scaling Double Slash 1 | 31 | 20 | 1 | 1 | 1 | 1 | No | 0 | 10 | 7 |  |
| Sword Scaling Double Slash 2 | 29 | 30 | 1 | 1 | 1 | 1 | No | 0 | 15 | 0 |  |
| Seikret Attack Start | 10 | 0 | 1 | 1 | 1 | 1 | No | 3 | 0 | 0 |  |
| Seikret Attack Swings | 10 | 0 | 1 | 1 | 0.5 | 1 | No | 2 | 0 | 0 |  |
| Seikret Attack Finisher | 20 | 0 | 1 | 1 | 0.5 | 1 | No | 6 | 0 | 0 |  |
| Dismount attack 1 | 10 | 0 | 1 | 1 | 0.5 | 1 | No | 3 | 0 | 0 |  |
| Dismount attack 2 | 25 | 55 | 1 | 1 | 0.5 | 1 | No | 10 | 0 | 0 |  |
| Axe Morph Combination | 15 | 0 | 1 | 1 | 1 | 1 | No | 5 | 0 | 0 |  |
| Axe Morph Combination Extra Hit | 15 | 0 | 1 | 1 | 1 | 1 | No | 5 | 0 | 0 |  |
| Axe Morph Combination Swings | 20 | 0 | 1 | 1 | 0.2 | 1 | No | 5 | 0 | 0 |  |
| Axe Morph Combination Finisher 1 | 20 | 0 | 1 | 1 | 1 | 1 | No | 5 | 0 | 0 |  |
| Axe Morph Combination Finisher 2 | 46 | 0 | 1 | 1 | 1 | 1 | No | 30 | 0 | 0 | Activates Power Axe |
| Sword Morph Combination | 25 | 0 | 1 | 1 | 1 | 1 | No | 0 | 3 | 7 |  |
| Sword Morph Combination Extra Hit | 25 | 0 | 1 | 1 | 1 | 1 | No | 0 | 4 | 0 |  |
| Sword Morph Combination Swings | 35 | 0 | 1 | 1 | 0.2 | 1 | No | 0 | 7 | 3 |  |
| Sword Morph Combination Explosion | 100 | 0 | 1 | 1 | 0 | 1 | No | 0 | 10 | 20 | Status Phials have guaranteed application |
| Mount Attack | 35 | 0 | 1 | 1 | 0.1 | 1 | No | 5 | 0 | 0 |  |
| Mount Finisher | 60 | 0 | 1 | 1 | 0.1 | 1 | No | 0 | 7 | 0 |  |
| Mount Finisher Multihit | 12 | 0 | 0.5 | 0.5 | 0.1 | 0.1 | No | 0 | 1 | 0 | Exhaust Phials get a 0.3 modifier to both Exhaust and Stun? |
| Mount Finisher Explosion | 80 | 0 | 1 | 1 | 0 | 1 | No | 0 | 7 | 20 |  |
| Sneak Attack 1 | 50 | 0 | 1 | 1 | 0.5 | 1 | No | 5 | 0 | 0 | Deals hitzone-ignoring damage |
| Sneak Attack 2 | 56 | 0 | 1 | 1 | 0.5 | 1 | No | 35 | 0 | 0 | Deals hitzone-ignoring damage. Activates Power Axe |
| Aerial Sneak Attack | 106 | 0 | 1 | 1 | 1 | 1 | No | 35 | 0 | 0 | Deals hitzone-ignoring damage. Activates Power Axe |
| Underwater Attack 1 | 71 | 0 | 1 | 1 | 1 | 1 | No | 10 | 0 | 0 |  |
| Underwater Attack 2 | 71 | 0 | 1 | 1 | 1 | 1 | No | 10 | 0 | 0 |  |

## Other melee weapons (1.040)

From the 1.040 sheet, one tab each (gid below), fetched 2026-10-15. The attack picker (`Attacks.cs`) leaves out sneak attacks, kinsect
attacks, clash and underwater attacks. Blunt hits of cutting weapons (shield hits, kinsect marks) are scored at the weapon type's slash
hitzone. Demon mode, extracts, Vaulting Dance and Power Axe multipliers are not modeled. Bowguns are out of scope (ammo model).

Hits that cannot crit (sheet "Can Crit: No", or a color in the Sharpness column) are scored without the crit factors: their raw and
element add `true raw x noCritRawFactor` and `element x noCritElementFactor` next to EFR and EFE. Assumptions where the sheet says nothing:

* **Gunlance shells, Wyvernfire, Wyrmstake explosions:** the "Hitzone-ignoring" column is a raw MV that ignores the hitzone and sharpness;
  the fixed fire ignores the hitzone too and counts as a proc. Shelling type from the Gogma focus (attack Normal, affinity Long, element
  Wide, wilds.mhdb.io), level 3 for all Artian gunlances. Wyrmstake ticks are ordinary hits (GL tab), level from the shelling.
* **Charge Blade phial bursts:** phial from the Gogma focus (attack Impact, affinity and element Element). Impact bursts use the raw
  hitzone (the sheet does not mark them hitzone-ignoring). ED I, ED II, AED and its follow-up burst one phial each, SAED all five.
  The Condensed Elemental Slash explosion can crit (sheet).
* **Hunting Horn soundwaves and echo hits:** yellow sharpness, no crit; Echo Bubble raw ignores the hitzone, its element does not.
  Echo Wave (element / status) and song effects are not modeled.

### Sword & Shield (gid 1261650290)

| Attack | Motion Value | Stun | Exhaust | Mount | Element | Status | Sharpness | Can Bounce | Notes |
|---|---|---|---|---|---|---|---|---|---|
| Advancing Slash | 28 | 0 | 0 | 0 | 1 | 1 | 1 | Yes |  |
| Rising Slash | 18 | 0 | 0 | 0 | 1 | 1 | 1 | Yes |  |
| Chop | 18 | 0 | 0 | 0 | 1.3 | 1 | 1 | Yes |  |
| Side Slash | 17 | 0 | 0 | 0 | 1.3 | 1 | 1 | Yes |  |
| Diagonal Rising Slash | 24 | 0 | 0 | 0 | 1.3 | 1 | 1 | Yes |  |
| Diagonal Chop | 27 | 0 | 0 | 0 | 1.3 | 1 | 1 | Yes |  |
| Lateral Slash | 27 | 0 | 0 | 0 | 1.2 | 1 | 1 | Yes |  |
| Return Stroke | 28 | 0 | 0 | 0 | 1.2 | 1 | 1 | Yes |  |
| Spinning Rising Slash | 39 | 0 | 0 | 0 | 1.2 | 1 | 1 | Yes |  |
| Roundslash | 41 | 0 | 0 | 0 | 1 | 1 | 1 | Yes |  |
| Spinning Reaper | 65 | 0 | 0 | 0 | 1 | 1 | 1 | No |  |
| Shield Attack | 16 | 10 | 25 | 0 | 0 | 0 | 0 | No |  |
| Shield Bash | 27 | 8 | 25 | 0 | 0 | 0 | 0 | No |  |
| Hard Basher | 40 | 35 | 25 | 0 | 0 | 0 | 0 | No |  |
| Guard Slash | 14 | 0 | 0 | 0 | 1 | 1 | 1 | Yes |  |
| Charged Slash 1 | - | - | - | - | - | - | - | - | Continues to not exist |
| Charged Slash 2 | 40 | 0 | 0 | 0 | 1 | 1 | 1 | No |  |
| Scaling Slash | 25 | 0 | 0 | 0 | 1 | 1 | 1 | No |  |
| Jumping Rising Slash | 18 | 0 | 0 | 45 | 1 | 1 | 1 | No |  |
| Jumping Advancing Slash | 18 | 0 | 0 | 45 | 1 | 1 | 1 | No |  |
| Jumping Slash | 20 | 0 | 0 | 55 | 1 | 1 | 1 | No |  |
| Falling Bash 1 | 52 | 10 | 15 | 0 | 0 | 0 | 0 | No |  |
| Falling Bash 2 | 47 | 20 | 15 | 0 | 0 | 0 | 0 | No |  |
| Plunging Thrust | 12 | 0 | 0 | 1 | 0.3 | 0.3 | 0.2 | No |  |
| Sliding Sweep 1 | 10 | 0 | 0 | 0 | 1 | 1 | 0.5 | No |  |
| Sliding Sweep 2 | 50 | 0 | 0 | 0 | 1 | 1 | 0.5 | No |  |
| Sliding Slash 1 | 12 | 0 | 0 | 0 | 1 | 1 | 0.5 | No |  |
| Sliding Slash 2 | 30 | 0 | 0 | 0 | 1 | 1 | 0.5 | No |  |
| Leaping Slash | 30 | 0 | 0 | 0 | 1 | 1 | 1 | No |  |
| Perfect Rush I 1 | 10 | 5 | 0 | 0 | 0 | 0 | 0 | No |  |
| Perfect Rush I 2 | 15 | 0 | 0 | 0 | 1 | 1 | 1 | No |  |
| Perfect Rush I 3 | 15 | 0 | 0 | 0 | 1 | 1 | 1 | No |  |
| Perfect Rush II | 20 | 7 | 0 | 0 | 0 | 0 | 0 | No |  |
| Perfect Rush III | 30 | 0 | 0 | 0 | 1 | 1 | 1 | No |  |
| Perfect Rush III Multihit | 18 | 0 | 0 | 0 | 1 | 0.3 | 0 | No | Hits 3 times |
| Power Perfect Rush I 1 | 40 | 15 | 10 | 0 | 0 | 0 | 0 | No | When timed properly. |
| Power Perfect Rush I 2 | 40 | 0 | 0 | 0 | 1.5 | 1 | 1 | No | When timed properly. |
| Power Perfect Rush I 3 | 45 | 0 | 0 | 0 | 1.5 | 1 | 1 | No | When timed properly. |
| Power Perfect Rush II | 60 | 20 | 15 | 0 | 0 | 0 | 0 | No | When timed properly. |
| Power Perfect Rush III | 80 | 0 | 0 | 0 | 1.5 | 1 | 1 | No | When timed properly. |
| Power Perfect Rush III Multihit | 34 | 0 | 0 | 0 | 1 | 0.3 | 0 | No | When timed properly. Hits 3 times. |
| Charged Chop 1 | 18 | 0 | 0 | 0 | 1 | 1 | 1 | Yes |  |
| Charged Chop 2 | 28 | 0 | 0 | 0 | 1.2 | 1.2 | 1 | No |  |
| Charged Chop 3 | 14 | 0 | 0 | 0 | 0.3 | 0.3 | 1 | No | Hits 4 times |
| Power Charged Chop 3 | 28 | 0 | 0 | 0 | 1.25 | 0.3 | 1 | No | Hits 4 times |
| Counter Slash | 55 | 0 | 0 | 0 | 1.5 | 1 | 1 | No |  |
| Recovering Slash | 18 | 0 | 0 | 0 | 1 | 1 | 1 | Yes |  |
| Seikret Attack I | 10 | 0 | 0 | 0 | 1 | 1 | 1 | No |  |
| Seikret Attack II | 10 | 0 | 0 | 0 | 1 | 1 | 1 | No |  |
| Seikret Dash Attack | 10 | 0 | 0 | 0 | 1 | 1 | 1 | No |  |
| Dismount Attack | 20 | 0 | 0 | 0 | 1 | 1 | 1 | No |  |
| Vital Stab | 15 | 0 | 0 | 0 | 1 | 1 | 1 | No |  |
| Aerial Vital Stab | 15 | 0 | 0 | 40 | 1 | 1 | 1 | No |  |
| Focus Strike: Vital Stab Extra Hit | 15 | 0 | 0 | 0 | 1 | 1 | 1 | No |  |
| Vital Stab forcing a wound to open | 19 | 0 | 0 | 0 | 0.3 | 0.3 | 1 | No | Hits thrice |
| Falling Slash | 20 | 0 | 0 | 0 | 1 | 1 | 1 | No | Hits twice |
| Falling Slash Multihit | 7 | 0 | 0 | 0 | 0.3 | 0.3 | 1 | No |  |
| Upswing Bash | 20 | 40 | 15 | 0 | 0 | 0 | 0 | No | Hits twice |
| Upswing Bash Multihit | 20 | 20 | 0 | 0 | 0 | 0 | 0 | No | Hits 3 times. Not sure if and when it applies |
| Clash Stabs | 15 | 0 | 0 | 0 | 1 | 1 | 1 | No | Up to 4 |
| Clash Finisher | 45 | 0 | 0 | 0 | 1 | 1 | Yellow | No | Deals blunt damage |
| Mount Attack 1 | 15 | 0 | 0 | 0 | 1 | 1 | 0.1 | No |  |
| Mount Attack 2 | 17 | 0 | 0 | 0 | 1 | 1 | 0.1 | No |  |
| Mount Finisher 1 | 13 | 5 | 25 | 0 | 0 | 0 | 0 | No |  |
| Mount Finisher 2 | 13 | 5 | 25 | 0 | 0 | 0 | 0 | No |  |
| Mount Finisher 3 | 25 | 5 | 25 | 0 | 0 | 0 | 0 |  |  |
| Mount Finisher 4 | 70 | 0 | 0 | 0 | 1 | 1 | 0.1 |  |  |
| Mount Finisher 5 | 100 | 0 | 0 | 0 | 1 | 1 | 0.1 | No |  |
| Sneak Attack 1 | 33 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | Deals hitzone-ignoring damage |
| Sneak Attack 2 | 33 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | Deals hitzone-ignoring damage |
| Sneak Attack 3 | 40 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | Deals hitzone-ignoring damage |
| Aerial Sneak Attack | 106 | 0 | 0 | 0 | 1 | 1 | 0 | No | Deals hitzone-ignoring damage |
| Underwater Attack 1 | 26 | 0 | 0 | 0 | 0.5 | 0.5 | 1 | No |  |
| Underwater Attack 2 | 30 | 0 | 0 | 0 | 0.5 | 0.5 | 1 | No |  |
| Underwater Attack 3 | 36 | 0 | 0 | 0 | 0.5 | 0.5 | 1 | No |  |
| Underwater Attack 4 | 58 | 0 | 0 | 0 | 0.5 | 0.5 | 0 | No |  |

### Dual Blades (gid 444431133)

| Attack | Motion Value | Mount | Element | Status | Sharpness | Part Break | Can Bounce | Gauge generation | Gauge cost | Notes |
|---|---|---|---|---|---|---|---|---|---|---|
| Double Slash 1 | 8 | 0 | 0.6 | 0.4 | 0.5 | 1 | Yes | 0 | 0 |  |
| Double Slash 2 | 11 | 0 | 0.6 | 0.4 | 0.5 | 1 | Yes | 0 | 0 |  |
| Double Slash Return Stroke 1 | 9 | 0 | 0.6 | 0.4 | 0.5 | 1 | Yes | 0 | 0 |  |
| Double Slash Return Stroke 1 (Unused) | 11 | 0 | 0.6 | 0.4 | 0.5 | 1 | Yes | 0 | 0 |  |
| Double Slash Return Stroke 2 | 11 | 0 | 0.6 | 0.4 | 0.5 | 1 | Yes | 0 | 0 |  |
| Circle Slash 1 | 6 | 0 | 0.7 | 0.5 | 0.3 | 1 | Yes | 0 | 0 |  |
| Circle Slash 2 | 11 | 0 | 0.7 | 0.5 | 0.3 | 1 | Yes | 0 | 0 |  |
| Circle Slash 3 | 12 | 0 | 0.7 | 0.5 | 0.4 | 1 | Yes | 0 | 0 |  |
| Demon Fangs | 14 | 0 | 0.6 | 0.4 | 1 | 1 | Yes | 7 | 0 |  |
| Twofold Demon Slash 1 | 15 | 0 | 0.6 | 0.4 | 0.5 | 1 | Yes | 5 | 0 |  |
| Twofold Demon Slash 2 | 7 | 0 | 0.6 | 0.4 | 0.5 | 1 | Yes | 5 | 0 |  |
| Sixfold Demon Slash 1 | 13 | 0 | 0.6 | 0.4 | 0.2 | 1 | Yes | 5 | 0 |  |
| Sixfold Demon Slash 2 | 13 | 0 | 0.6 | 0.4 | 0.2 | 1 | Yes | 5 | 0 |  |
| Sixfold Demon Slash 3 | 9 | 0 | 0.6 | 0.4 | 0.2 | 1 | Yes | 7 | 0 |  |
| Sixfold Demon Slash 4 | 9 | 0 | 0.6 | 0.4 | 0.2 | 1 | Yes | 7 | 0 |  |
| Sixfold Demon Slash 5 | 18 | 0 | 0.6 | 0.4 | 0.3 | 1 | Yes | 13 | 0 |  |
| Sixfold Demon Slash 6 | 18 | 0 | 0.6 | 0.4 | 0.3 | 1 | Yes | 13 | 0 |  |
| Fade Slash | 10 | 0 | 0.6 | 0.4 | 1 | 1 | Yes | 7.5 | 0 |  |
| Rising Slash | 8 | 0 | 0.6 | 0.4 | 1 | 1 | Yes | 0 | 0 |  |
| Demon Rising Slash | 10 | 0 | 0.8 | 0.6 | 1 | 1 | Yes | 5 | 0 |  |
| Lunging Strike 1 | 3 | 0 | 0.6 | 0.4 | 0.2 | 1 | Yes | 0 | 0 |  |
| Lunging Strike 2 | 3 | 0 | 0.6 | 0.4 | 0.2 | 1 | Yes | 0 | 0 |  |
| Lunging Strike 3 | 6 | 0 | 0.6 | 0.4 | 0.3 | 1 | Yes | 0 | 0 |  |
| Lunging Strike 4 | 6 | 0 | 0.6 | 0.4 | 0.3 | 1 | Yes | 0 | 0 |  |
| Side/Back Lunging Strike 1 | 6 | 0 | 0.6 | 0.4 | 0.3 | 1 | Yes | 0 | 0 |  |
| Side/Back Lunging Strike 2 | 6 | 0 | 0.6 | 0.4 | 0.3 | 1 | Yes | 0 | 0 |  |
| Demon Flurry Rush 1 | 5 | 0 | 0.6 | 0.5 | 0.3 | 1 | No | 2.5 | 2.5 | Only spends gauge in Archdemon Mode. |
| Demon Flurry Rush 2 | 5 | 0 | 0.6 | 0.5 | 0.3 | 1 | No | 2.5 | 0 |  |
| Demon Flurry Rush 3 | 4 | 0 | 0.6 | 0.5 | 0.3 | 1 | No | 2.5 | 0 |  |
| Demon Flurry Rush 4 | 4 | 0 | 0.6 | 0.5 | 0.3 | 1 | No | 2.5 | 0 |  |
| Demon Flurry Rush 5 | 11 | 0 | 1 | 0.8 | 0.4 | 1 | No | 5 | 0 |  |
| Demon Flurry Rush 6 | 11 | 0 | 1 | 0.8 | 0.4 | 1 | No | 5 | 0 |  |
| Left Spinning Slash 1 | 15 | 0 | 0.6 | 0.5 | 0.3 | 1 | No | 2.5 | 0 |  |
| Left Spinning Slash 2 | 7 | 0 | 0.6 | 0.5 | 0.3 | 1 | No | 5 | 0 |  |
| Left Spinning Slash 3 | 5 | 0 | 0.6 | 0.5 | 0.4 | 1 | No | 10 | 0 |  |
| Right Spinning Slash 1 | 15 | 0 | 0.6 | 0.5 | 0.3 | 1 | No | 2.5 | 0 |  |
| Right Spinning Slash 1 (unused) | 15 | 0 | 1 | 1 | 0.3 | 1 | No | 5 | 0 |  |
| Right Spinning Slash 2 | 7 | 0 | 0.6 | 0.5 | 0.3 | 1 | No | 5 | 0 |  |
| Right Spinning Slash 3 | 5 | 0 | 0.6 | 0.5 | 0.4 | 1 | No | 10 | 0 |  |
| Left Double Roundslash 1 | 16 | 0 | 1 | 1 | 0.3 | 1 | No | 5 | 0 |  |
| Left Double Roundslash 2 | 8 | 0 | 1 | 1 | 0.3 | 1 | No | 7.5 | 0 |  |
| Left Double Roundslash 2 (unused) | 6 | 0 | 0.6 | 0.5 | 0.3 | 1 | No | 5 | 0 |  |
| Left Double Roundslash 3 | 5 | 0 | 1 | 1 | 0.4 | 1 | No | 15 | 0 |  |
| Right Double Roundslash 1 | 16 | 0 | 1 | 1 | 0.3 | 1 | No | 5 | 0 |  |
| Right Double Roundslash 2 | 8 | 0 | 1 | 1 | 0.3 | 1 | No | 7.5 | 0 |  |
| Right Double Roundslash 3 | 5 | 0 | 1 | 1 | 0.4 | 1 | No | 15 | 0 |  |
| Blade Dance I 1 | 18 | 0 | 1 | 0.8 | 0.2 | 1 | No | 0 | 5 |  |
| Blade Dance I 2 | 18 | 0 | 1 | 0.8 | 0.2 | 1 | No | 0 | 0 |  |
| Blade Dance I 3 | 6 | 0 | 0.6 | 0.4 | 0.2 | 1 | No | 0 | 0 |  |
| Blade Dance I 4 | 6 | 0 | 0.6 | 0.4 | 0.2 | 1 | No | 0 | 0 |  |
| Blade Dance I 5 | 10 | 0 | 0.6 | 0.4 | 0.2 | 1 | No | 0 | 0 |  |
| Blade Dance I 6 | 10 | 0 | 0.6 | 0.4 | 0.2 | 1 | No | 0 | 0 |  |
| Blade Dance I 7 | 4 | 0 | 0.8 | 0.5 | 0.2 | 1 | No | 0 | 0 |  |
| Blade Dance I 8 | 4 | 0 | 0.8 | 0.5 | 0.2 | 1 | No | 0 | 0 |  |
| Blade Dance I 9 | 20 | 0 | 0.8 | 0.6 | 0.3 | 1 | No | 0 | 0 |  |
| Blade Dance I 10 | 20 | 0 | 0.8 | 0.6 | 0.3 | 1 | No | 0 | 0 |  |
| Blade Dance I 11 | 11 | 0 | 0.8 | 0.6 | 0.4 | 1 | No | 0 | 0 |  |
| Blade Dance I 12 | 11 | 0 | 0.8 | 0.6 | 0.4 | 1 | No | 0 | 0 |  |
| Blade Dance II 1 | 16 | 0 | 0.6 | 0.5 | 0.2 | 1 | No | 0 | 10 |  |
| Blade Dance II 2 | 16 | 0 | 0.6 | 0.5 | 0.2 | 1 | No | 0 | 0 |  |
| Blade Dance II 3 | 6 | 0 | 0.8 | 0.6 | 0.3 | 1 | No | 0 | 0 |  |
| Blade Dance II 4 | 25 | 0 | 0.8 | 0.6 | 0.3 | 1 | No | 0 | 0 |  |
| Blade Dance III 1 | 22 | 0 | 0.6 | 0.4 | 0.3 | 1 | No | 0 | 15 |  |
| Blade Dance III 2 | 8 | 0 | 0.6 | 0.4 | 0.3 | 1 | No | 0 | 0 |  |
| Blade Dance III 3 | 5 | 0 | 0.6 | 0.4 | 0.2 | 1 | No | 0 | 0 |  |
| Blade Dance III 4 | 22 | 0 | 0.6 | 0.4 | 0.3 | 1 | No | 0 | 0 |  |
| Blade Dance III 5 | 5 | 0 | 0.8 | 0.6 | 0.2 | 1 | No | 0 | 0 |  |
| Blade Dance III 6 | 14 | 0 | 0.8 | 0.6 | 0.2 | 1 | No | 0 | 0 |  |
| Blade Dance III 7 | 18 | 0 | 0.8 | 0.6 | 0.2 | 1 | No | 0 | 0 |  |
| Blade Dance III 8 | 8 | 0 | 0.8 | 0.6 | 0.3 | 1 | No | 0 | 0 |  |
| Blade Dance III 9 | 18 | 0 | 0.8 | 0.6 | 0.2 | 1 | No | 0 | 0 |  |
| Blade Dance III 10 | 36 | 0 | 0.8 | 0.6 | 0.4 | 1 | No | 0 | 0 |  |
| Blade Dance III 11 | 36 | 0 | 0.8 | 0.6 | 0.4 | 1 | No | 0 | 0 |  |
| Demon Flurry I 1 | 15 | 0 | 1 | 0.8 | 0.5 | 1 | No | 0 | 5 |  |
| Demon Flurry I 2 | 15 | 0 | 1 | 0.8 | 0.5 | 1 | No | 0 | 0 |  |
| Demon Flurry I 3 | 15 | 0 | 1 | 0.8 | 0.5 | 1 | No | 0 | 0 |  |
| Demon Flurry I 4 | 15 | 0 | 1 | 0.8 | 0.5 | 1 | No | 0 | 0 |  |
| Demon Flurry II 1 | 6 | 0 | 0.6 | 0.5 | 0.3 | 1 | No | 0 | 0 |  |
| Demon Flurry II 2 | 8 | 0 | 0.6 | 0.5 | 0.3 | 1 | No | 0 | 10 |  |
| Demon Flurry II 3 | 18 | 0 | 1 | 0.8 | 0.4 | 1 | No | 0 | 0 |  |
| Demon Flurry II 4 | 18 | 0 | 1 | 0.8 | 0.4 | 1 | No | 0 | 0 |  |
| Jumping Doubleslash 1 | 12 | 20 | 0.6 | 0.4 | 0.5 | 1 | No | 0 | 0 |  |
| Jumping Doubleslash 2 | 9 | 35 | 0.6 | 0.4 | 0.5 | 1 | No | 0 | 0 |  |
| Midair Spinning Blade Dance 1 | 12 | 0 | 0.5 | 0.4 | 0.2 | 1 | No | 0 | 0 |  |
| Midair Spinning Blade Dance 2 | 8 | 0 | 0.5 | 0.4 | 0.2 | 1 | No | 0 | 0 |  |
| Midair Spinning Blade Dance 3 | 12 | 0 | 0.7 | 0.6 | 0.3 | 1 | No | 0 | 0 |  |
| Midair Spinning Blade Dance 4 | 8 | 0 | 0.7 | 0.6 | 0.3 | 1 | No | 0 | 0 |  |
| Midair Spinning Blade Dance Finisher 1 | 15 | 0 | 0.7 | 0.7 | 0.5 | 1 | No | 0 | 0 |  |
| Midair Spinning Blade Dance Finisher 2 | 15 | 0 | 0.7 | 0.7 | 0.5 | 1 | No | 0 | 0 |  |
| Midair Spinning Blade Dance Finisher 3 | 20 | 0 | 0.8 | 0.7 | 0.2 | 1 | No | 0 | 0 |  |
| Midair Spinning Blade Dance Finisher 4 | 20 | 0 | 0.8 | 0.7 | 0.2 | 1 | No | 0 | 0 |  |
| Midair Spinning Blade Dance Finisher 5 | 6 | 0 | 0.8 | 0.7 | 0.3 | 1 | No | 0 | 0 |  |
| Midair Spinning Blade Dance Finisher 6 | 6 | 0 | 0.8 | 0.7 | 0.3 | 1 | No | 0 | 0 |  |
| Heavenly Blade Dance 1 | 15 | 0 | 0.4 | 0.4 | 0.1 | 1 | No | 1 | 0 | Raw damage multiplied by 0.5 after the 8th hit, by 0.3 after the 10th, and by 0.15 after the 12th |
| Heavenly Blade Dance 2 | 10 | 0 | 0.4 | 0.4 | 0.1 | 1 | No | 1 | 0 | Raw damage multiplied by 0.5 after the 8th hit, by 0.3 after the 10th, and by 0.15 after the 12th |
| Heavenly Blade Dance 3 | 12 | 0 | 0.4 | 0.4 | 0.1 | 1 | No | 1 | 0 | Raw damage multiplied by 0.5 after the 8th hit, by 0.3 after the 10th, and by 0.15 after the 12th |
| Heavenly Blade Dance 4 | 8 | 0 | 0.4 | 0.4 | 0.1 | 1 | No | 1 | 0 | Raw damage multiplied by 0.5 after the 8th hit, by 0.3 after the 10th, and by 0.15 after the 12th |
| Heavenly Blade Dance Finisher 1 | 20 | 0 | 1 | 1 | 0.1 | 1 | No | 20 | 0 |  |
| Heavenly Blade Dance Finisher 2 | 20 | 0 | 1 | 1 | 0.1 | 1 | No | 20 | 0 |  |
| Heavenly Blade Dance Finisher 3 | 24 | 0 | 0.7 | 0.7 | 0.2 | 1 | No | 15 | 0 |  |
| Heavenly Blade Dance Finisher 4 | 24 | 0 | 0.7 | 0.7 | 0.2 | 1 | No | 15 | 0 |  |
| Heavenly Blade Dance Finisher 5 | 8 | 0 | 0.7 | 0.7 | 0.3 | 1 | No | 15 | 0 |  |
| Heavenly Blade Dance Finisher 6 | 8 | 0 | 0.7 | 0.7 | 0.3 | 1 | No | 15 | 0 |  |
| Sliding Slash 1 | 10 | 0 | 0.6 | 0.4 | 0.2 | 1 | No | 0 | 0 |  |
| Sliding Slash 2 | 10 | 0 | 0.6 | 0.4 | 0.2 | 1 | No | 0 | 0 |  |
| Sliding Slash 3 | 13 | 0 | 0.6 | 0.4 | 0.3 | 1 | No | 0 | 0 |  |
| Sliding Slash 4 | 13 | 0 | 0.6 | 0.4 | 0.3 | 1 | No | 0 | 0 |  |
| Perfect Evade 1 | 6 | 0 | 0.3 | 0.2 | 0.3 | 1 | No | 1.5 | 10 | Only spends gauge in Archdemon Mode. It's also the cost for a regular Demon Dash |
| Perfect Evade 2 | 6 | 0 | 0.3 | 0.2 | 0.3 | 1 | No | 1.5 | 0 |  |
| Perfect Evade 3 | 10 | 0 | 0.6 | 0.4 | 0.4 | 1 | No | 2 | 0 |  |
| Perfect Evade 4 (unused) | 10 | 0 | 0.6 | 0.4 | 0.4 | 1 | No | 2 | 0 |  |
| Screw Slicer 1 | 2 | 0 | 0.8 | 0.6 | 0.1 | 1 | No | 2 | 0 |  |
| Screw Slicer 2 | 3 | 0 | 0.8 | 0.6 | 0.1 | 1 | No | 2 | 0 |  |
| Screw Slicer 3 | 15 | 0 | 1 | 0.8 | 0.3 | 1 | No | 2.5 | 0 |  |
| Screw Slicer 4 | 10 | 0 | 1 | 0.8 | 0.3 | 1 | No | 2.5 | 0 |  |
| Screw Slicer 5 | 15 | 0 | 1 | 0.8 | 0.3 | 1 | No | 2.5 | 0 |  |
| Screw Slicer 6 | 10 | 0 | 1 | 0.8 | 0.3 | 1 | No | 2.5 | 0 |  |
| Seikret Attack I 1 | 10 | 0 | 0.6 | 0.4 | 0.5 | 1 | No | 0 | 0 |  |
| Seikret Attack I 2 | 8 | 0 | 0.6 | 0.4 | 0.5 | 1 | No | 0 | 0 |  |
| Seikret Attack II 1 | 10 | 0 | 0.6 | 0.4 | 0.5 | 1 | No | 0 | 0 |  |
| Seikret Attack II 2 | 8 | 0 | 0.6 | 0.4 | 0.5 | 1 | No | 0 | 0 |  |
| Dismount Attack 1 | 12 | 8 | 0.5 | 0.4 | 0.2 | 1 | No | 0 | 0 |  |
| Dismount Attack 2 | 8 | 8 | 0.5 | 0.4 | 0.2 | 1 | No | 0 | 0 |  |
| Dismount Attack 3 | 12 | 8 | 0.7 | 0.6 | 0.3 | 1 | No | 0 | 0 |  |
| Dismount Attack 4 | 8 | 8 | 0.7 | 0.6 | 0.3 | 1 | No | 0 | 0 |  |
| Dismount Attack Finisher 1 | 14 | 0 | 0.6 | 0.4 | 0.4 | 1 | No | 0 | 0 |  |
| Dismount Attack Finisher 2 | 6 | 0 | 0.6 | 0.4 | 0.4 | 1 | No | 0 | 0 |  |
| Dismount Attack Finisher 3 | 8 | 0 | 0.6 | 0.4 | 0.3 | 1 | No | 0 | 0 |  |
| Focus Strike: Turning Tide 1 | 10 | 0 | 1 | 0.8 | 0.3 | 1 | No | 2.5 | 0 |  |
| Focus Strike: Turning Tide 2 | 10 | 0 | 1 | 0.8 | 0.3 | 1 | No | 2.5 | 0 |  |
| Focus Strike: Turning Tide 1 Extra Hit | 10 | 0 | 1 | 0.8 | 0 | 1 | No | 2.5 | 0 |  |
| Focus Strike: Turning Tide 2 Extra Hit | 10 | 0 | 1 | 0.8 | 0 | 1 | No | 2.5 | 0 |  |
| Focus Strike: Turning Tide Followup 1 | 27 | 0 | 1 | 0.8 | 0.3 | 1 | No | 2.5 | 0 |  |
| Focus Strike: Turning Tide Followup 2 | 27 | 0 | 1 | 0.8 | 0.3 | 1 | No | 2.5 | 0 |  |
| Turning Tidedrill 1 | 12 | 0 | 0.6 | 0.5 | 0.1 | 1 |  | 4.5 | 0 |  |
| Turning Tidedrill 2 | 9 | 0 | 0.6 | 0.5 | 0.1 | 1 |  | 4.5 | 0 |  |
| Turning Tidedrill 3 | 9 | 0 | 0.6 | 0.5 | 0.1 | 1 |  | 4.5 | 0 |  |
| Turning Tidedrill 4 | 12 | 0 | 0.6 | 0.5 | 0.1 | 1 |  | 4.5 | 0 |  |
| Turning Tidedrill 5 | 9 | 0 | 0.6 | 0.5 | 0.1 | 1 |  | 4.5 | 0 |  |
| Turning Tidedrill 6 | 12 | 0 | 0.6 | 0.5 | 0.1 | 1 |  | 4.5 | 0 |  |
| Turning Tidedrill 7 | 12 | 0 | 0.6 | 0.5 | 0.1 | 1 |  | 4.5 | 0 |  |
| Turning Tidedrill 8 | 9 | 0 | 0.6 | 0.5 | 0.1 | 1 |  | 4.5 | 0 |  |
| Turning Tidedrill 9 | 18 | 0 | 0.7 | 0.6 | 0.3 | 1 | No | 1.4 | 0 |  |
| Turning Tidedrill 10 | 24 | 0 | 0.7 | 0.6 | 0.3 | 1 | No | 2.5 | 0 |  |
| Mounted Attack 1 | 5 | 0 | 0.6 | 0.4 | 0.1 | 1 | No | 0 | 0 |  |
| Mounted Attack 2 | 5 | 0 | 0.6 | 0.4 | 0.1 | 1 | No | 0 | 0 |  |
| Mounted Attack 3 | 8 | 0 | 0.6 | 0.4 | 0.1 | 1 | No | 0 | 0 |  |
| Mounted Attack 4 | 8 | 0 | 0.6 | 0.4 | 0.1 | 1 | No | 0 | 0 |  |
| Mounted Attack 5 | 6 | 0 | 0.6 | 0.4 | 0.1 | 1 | No | 0 | 0 |  |
| Mounted Attack 6 | 6 | 0 | 0.6 | 0.4 | 0.1 | 1 | No | 0 | 0 |  |
| Mounted Finisher 1 | 20 | 0 | 0.6 | 0.4 | 0.1 | 1 | No | 0 | 0 |  |
| Mounted Finisher 2 | 18 | 0 | 0.6 | 0.4 | 0.1 | 1 | No | 0 | 0 |  |
| Mounted Finisher 3 | 18 | 0 | 0.6 | 0.4 | 0.1 | 1 | No | 0 | 0 |  |
| Mounted Finisher 4 | 20 | 0 | 0.6 | 0.4 | 0.1 | 1 | No | 0 | 0 |  |
| Mounted Finisher 5 | 10 | 0 | 0.6 | 0.4 | 0.1 | 1 | No | 0 | 0 |  |
| Mounted Finisher 6 | 8 | 0 | 0.6 | 0.4 | 0.1 | 1 | No | 0 | 0 |  |
| Mounted Finisher 7 | 10 | 0 | 0.6 | 0.4 | 0.1 | 1 | No | 0 | 0 |  |
| Mounted Finisher 8 | 8 | 0 | 0.6 | 0.4 | 0.1 | 1 | No | 0 | 0 |  |
| Mounted Finisher 9 | 30 | 0 | 1 | 1 | 0.1 | 1 | No | 0 | 0 |  |
| Mounted Finisher 10 | 30 | 0 | 1 | 1 | 0.1 | 1 | No | 0 | 0 |  |
| Sneak Attack 1 | 50 | 0 | 1 | 1 | 0.5 | 1 | No | 0 | 0 | Deals hitzone-ignoring damage |
| Sneak Attack 2 | 56 | 0 | 1 | 1 | 0.5 | 1 | No | 0 | 0 | Deals hitzone-ignoring damage |
| Aerial Sneak Attack 1 | 40 | 0 | 0.4 | 0.4 | 0.1 | 1 | No | 0 | 0 | Deals hitzone-ignoring damage |
| Aerial Sneak Attack 2 | 48 | 0 | 0.4 | 0.4 | 0.1 | 1 | No | 0 | 0 | Deals hitzone-ignoring damage |
| Aerial Sneak Attack 3 | 10 | 0 | 0.4 | 0.4 | 0.1 | 1 | No | 0 | 0 | Deals hitzone-ignoring damage |
| Aerial Sneak Attack 4 | 8 | 0 | 0.4 | 0.4 | 0.1 | 1 | No | 0 | 0 | Deals hitzone-ignoring damage |
| Underwater Attack 1 | 16 | 0 | 0.35 | 0.35 | 0.5 | 1 | No | 0 | 0 |  |
| Underwater Attack 2 | 18 | 0 | 0.35 | 0.35 | 0.5 | 1 | No | 0 | 0 |  |
| Underwater Attack 3 | 26 | 0 | 0.35 | 0.35 | 0.5 | 1 | No | 0 | 0 |  |
| Underwater Attack 4 | 22 | 0 | 0.35 | 0.35 | 0.5 | 1 | No | 0 | 0 |  |
| Underwater Attack 5 | 32 | 0 | 0.35 | 0.35 | 0.5 | 1 | No | 0 | 0 |  |
| Underwater Attack 6 | 36 | 0 | 0.35 | 0.35 | 0.5 | 1 | No | 0 | 0 |  |

### Hammer (gid 1147989956)

| Attack | Motion Value | Stun | Exhaust | Mount | Element | Status | Sharpness | Part Break | Can Bounce | Notes |
|---|---|---|---|---|---|---|---|---|---|---|
| Swing | 22 | 15 | 10 | 0 | 1 | 1 | 1 | 1 | Yes |  |
| Follow-up | 18 | 15 | 10 | 0 | 1 | 1 | 1 | 1 | Yes |  |
| Overhead Smash I | 41 | 20 | 10 | 0 | 1 | 1 | 1 | 1 | Yes |  |
| Overhead Smash II | 45 | 25 | 15 | 0 | 1 | 1 | 1 | 1 | Yes |  |
| Upswing | 95 | 50 | 10 | 0 | 1.3 | 1 | 1 | 1 | No | 150 parry |
| Side Smash | 23 | 20 | 10 | 0 | 1 | 1 | 1 | 1 | Yes |  |
| Big Bang I | 43 | 5 | 5 | 0 | 1.1 | 1 | 1 | 1 | No |  |
| Big Bang II | 48 | 5 | 5 | 0 | 1.1 | 1 | 1 | 1 | No |  |
| Big Bang III | 70 | 10 | 10 | 0 | 1.1 | 1 | 1 | 1 | No |  |
| Big Bang IV | 90 | 10 | 10 | 0 | 1.1 | 1 | 1 | 1 | No |  |
| Big Bang V 1 | 28 | 5 | 0 | 0 | 1.1 | 1 | 0.5 | 1 | No |  |
| Big Bang V 2 | 28 | 5 | 0 | 0 | 1.1 | 1 | 0.5 | 1 | No |  |
| Big Bang V 3 | 130 | 15 | 15 | 0 | 1.5 | 1 | 1 | 1.5 | No |  |
| Charged Side Blow | 25 | 15 | 10 | 0 | 1.1 | 1 | 1 | 1 | Yes |  |
| Charged Followup | 55 | 25 | 10 | 0 | 1 | 1 | 1 | 1 | No |  |
| Charged Brutal Upswing | 70 | 45 | 10 | 0 | 1.2 | 1 | 1 | 1 | No | 100 Parry |
| Charged Big Bang 1 | 20 | 5 | 0 | 0 | 1 | 1 | 1 | 1 | No |  |
| Charged Big Bang 2 | 130 | 50 | 30 | 0 | 1.5 | 1 | 1 | 1.2 | No |  |
| Spinning Bludgeon Start | 23 | 5 | 2 | 0 | 2.5 | 1 | 0.3 | 1 | No |  |
| Spinning Bludgeon Mid | 18 | 10 | 2 | 0 | 2.5 | 1 | 0.3 | 1 | No |  |
| Spinning Bludgeon Finish | 25 | 15 | 5 | 0 | 1.5 | 1 | 1 | 1 | No |  |
| Spinning Side Smash | 30 | 15 | 5 | 0 | 1.5 | 1 | 1 | 1 | No |  |
| Spinning Followup | 55 | 25 | 5 | 0 | 1.5 | 1 | 1 | 1 | No |  |
| Spinning Strong Upswing | 100 | 40 | 5 | 0 | 1 | 1 | 1 | 1 | No | 150 parry |
| Mighty Upswing | 130 | 40 | 10 | 0 | 1.2 | 1 | 1 | 1 | No | 100 Parry |
| Mighty Charge Slam 1 | 20 | 5 | 0 | 0 | 1 | 1 | 1 | 1 | No |  |
| Mighty Charge Slam 2 | 20 | 5 | 0 | 0 | 1 | 1 | 1 | 1 | No |  |
| Mighty Charge Slam 3 | 200 | 40 | 30 | 0 | 1.5 | 1 | 1 | 1 | No |  |
| Follow-up Spinslam Spin | 20 | 5 | 1 | 0 | 1 | 1 | 0.3 | 1 | No | Up to 5 spins |
| Follow-up Spinslam 1 | 50 | 1 | 0 | 0 | 1 | 1 | 1 | 0.1 | No |  |
| Follow-up Spinslam 2 | 210 | 50 | 25 | 0 | 1.5 | 1 | 1 | 1.5 | No |  |
| Jump Attack | 37 | 20 | 10 | 55 | 1 | 1 | 1 | 1 | No |  |
| Aerial Charge Lv1 | 72 | 40 | 10 | 55 | 1.1 | 1 | 1 | 1 | No |  |
| Aerial Charge Lv2 | 77 | 45 | 10 | 80 | 1.2 | 1 | 1 | 1 | No |  |
| Aerial Charge Lv3 | 88 | 55 | 10 | 100 | 1.5 | 1 | 1 | 1 | No |  |
| Midair Spinning Bludgeon Spin | 27 | 10 | 2 | 10 | 1 | 1 | 0.3 | 1 | No |  |
| Midair Spinning Bludgeon Land | 110 | 60 | 30 | 0 | 1 | 1 | 1 | 1 | No |  |
| Seikret Attack | 10 | 2 | 2 | 0 | 1 | 1 | 1 | 1 | No |  |
| Dismount Attack Spin | 10 | 10 | 2 | 10 | 1 | 1 | 1 | 1 | No |  |
| Dismount Attack Land | 37 | 20 | 10 | 0 | 1 | 1 | 1 | 1 | No |  |
| Focus Strike: Earthquake Start | 15 | 15 | 10 | 0 | 1 | 1 | 1 | 1 | No |  |
| Focus Strike: Earthquake Spin | 30 | 10 | 2 | 0 | 1 | 1 | 0.2 | 1 | No |  |
| Focus Strike: Earthquake Finish | 45 | 15 | 10 | 0 | 1 | 1 | 0.5 | 1 | No |  |
| Mount Attack | 37 | 10 | 10 | 0 | 1 | 1 | 0.1 | 1 | No |  |
| Mount Finisher 1 | 140 | 10 | 10 | 0 | 1 | 1 | 0.1 | 1 | No |  |
| Mount Finisher 2 | 100 | 30 | 10 | 0 | 1 | 1 | 0.1 | 1 | No |  |
| Sneak Attack | 106 | 30 | 10 | 0 | 1 | 1 | 1 | 1 | No | Deals hitzone-ignoring damage |
| Aerial Sneak Attack Spin | 5 | 1 | 5 | 0 | 1 | 1 | 0.3 | 1 | No | Deals hitzone-ignoring damage |
| Aerial Sneak Attack Finish | 95 | 30 | 10 | 0 | 1 | 1 | 1 | 1 | No | Deals hitzone-ignoring damage |
| Underwater Attack 1 | 70 | 10 | 10 | 0 | 1 | 1 | 1 | 1 | No |  |
| Underwater Attack 2 | 80 | 10 | 10 | 0 | 1 | 1 | 1 | 1 | No |  |

### Hunting Horn (gid 1483722342)

| Attack | Motion Value | Stun | Exhaust | Mount | Element | Status | Sharpness | Part Break | Can Bounce | Can Crit | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Forward Smash | 32 | 20 | 15 | 0 | 1 | 1 | 1 | 1 | Yes | Yes |  |
| Left Swing | 33 | 20 | 20 | 0 | 1 | 1 | 1 | 1 | Yes | Yes |  |
| Right Swing | 33 | 20 | 15 | 0 | 1 | 1 | 1 | 1 | Yes | Yes |  |
| Flourish 1 | 15 | 6 | 6 | 0 | 1 | 1 | 1 | 1 | Yes | Yes |  |
| Flourish 2 | 25 | 8 | 8 | 0 | 1 | 1 | 1 | 1 | Yes | Yes |  |
| Kick Up | - | - | - | - | - | - | - | - | - | - | No longer exists |
| Backwards Strike | 46 | 20 | 25 | 0 | 1 | 1 | 1 | 1 | Yes | Yes |  |
| Overhead Smash 1 | 14 | 5 | 10 | 0 | 1 | 1 | 1 | 1 | No | Yes |  |
| Overhead Smash 2 | 42 | 22 | 15 | 0 | 1 | 1 | 1 | 1 | No | Yes |  |
| Crush | 65 | 22 | 15 | 0 | 1 | 1 | 1 | 1 | Yes | Yes |  |
| Hilt Stab | 10 | 0 | 0 | 0 | 1 | 1 | 1 | 1 | Yes | Yes | Deals sever damage |
| Hilt Stab Counter | 10 | 0 | 0 | 0 | 1 | 1 | 1 | 1 | Yes | Yes | When hit during Hilt Stab. Both hits can hit from a single Hilt Stab, depending on timing. Deals sever damage |
| Neutral Perform | 35 | 22 | 22 | 0 | 1 | 1 | 1 | 1 | No | Yes |  |
| Forward Perform | 35 | 22 | 22 | 0 | 1 | 1 | 1 | 1 | No | Yes |  |
| Side Perform | 45 | 22 | 22 | 0 | 1 | 1 | 1 | 1 | No | Yes |  |
| Back Perform | 60 | 22 | 22 | 0 | 1 | 1 | 1 | 1 | No | Yes |  |
| Performance Beat I | 33 | 22 | 22 | 0 | 1 | 1 | 0.5 | 1 | No | Yes |  |
| Power Performance Beat I | 40 | 30 | 22 | 0 | 1 | 1 | 0.5 | 1 | Yes | Yes | When timed. It being able to bounce is probably an oversight |
| Performance Beat II | 35 | 22 | 22 | 0 | 1 | 1 | 0.5 | 1 | No | Yes |  |
| Power Performance Beat II | 50 | 30 | 22 | 0 | 1 | 1 | 0.5 | 1 | No | Yes | When timed |
| Encore 1 | 20 | 10 | 10 | 0 | 1 | 1 | 0.5 | 1 | No | Yes |  |
| Encore 2 | 60 | 27 | 22 | 0 | 1 | 1 | 0.5 | 1 | No | Yes |  |
| Power Encore 2 | 75 | 27 | 22 | 0 | 1 | 1 | 0.5 | 1 | No | Yes | When timed. |
| Jumping Smash | 35 | 20 | 20 | 55 | 1 | 1 | 1 | 1 | No | Yes |  |
| Jumping Strong Overhead Smash | 55 | 30 | 20 | 55 | 1 | 1 | 1 | 1 | No | Yes |  |
| Scaling Smash | 30 | 20 | 20 | 20 | 1 | 1 | 1 | 1 | No | Yes |  |
| Seikret Attack I | 20 | 15 | 8 | 0 | 1 | 1 | 1 | 1 | No | Yes |  |
| Seikret Attack II | 30 | 15 | 8 | 0 | 1 | 1 | 1 | 1 | No | Yes |  |
| Seikret Perform | 29 | 22 | 22 | 0 | 1 | 1 | 1 | 1 | No | Yes |  |
| Seikret Encore | 35 | 27 | 22 | 0 | 1 | 1 | 1 | 1 | No | Yes |  |
| Seikret Dismount | 45 | 30 | 20 | 0 | 1 | 1 | 1 | 1 | No | Yes |  |
| Sneak Attack | 106 | 30 | 20 | 0 | 1 | 1 | 1 | 1 | No | Yes |  |
| Focus Strike: Reverb | 20 | 10 | 10 | 0 | 1 | 1 | 1 | 1 | No | Yes |  |
| Focus Strike Soundwaves | 25 | 5 | 5 | 0 | 1 | 1 | Yellow | 1 | No | No | Needs to be timed |
| Focus Strike Finisher | 70 | 5 | 5 | 0 | 1 | 1 | Yellow | 1 | No | No | Only happens if you time all 5 soundwaves |
| Mounted Attack | 37 | 10 | 10 | 0 | 1 | 1 | 1 | 1 | No | Yes |  |
| Mounted Finisher 1 | 140 | 10 | 10 | 0 | 1 | 1 | 1 | 1 | No | Yes |  |
| Mounted Finisher 2 | 100 | 30 | 10 | 0 | 1 | 1 | 1 | 1 | No | Yes |  |
| Sneak Attack | 106 | 30 | 20 | 0 | 1 | 1 | 1 | 1 | No | Yes | Deals hitzone-ignoring damage |
| Aerial Sneak Attack | 106 | 30 | 20 | 0 | 1 | 1 | 1 | 1 | No | Yes | Deals hitzone-ignoring damage |
| Underwater Attack 1 | 72 | 10 | 10 | 0 | 1 | 1 | 1 | 1 | No | Yes |  |
| Underwater Attack 2 | 70 | 10 | 10 | 0 | 1 | 1 | 1 | 1 | No | Yes |  |
| Offset Melody | 75 | 20 | 30 | 0 | 1 | 1 | 1 | 1 | No | Yes | 150 Parry |
| Melody of Life | 0 | 0 | 0 | 0 | 75 Healing | 0 | 1 | 0 | No | No |  |
| Resounding Melody 1 | 20 | 10 | 10 | 0 | 1 | 1 | 0.3 | 1 | No | Yes | Triggers Resounding Melody Soundwaves |
| Resounding Melody 2 | 22 | 10 | 10 | 0 | 1 | 1 | 0.3 | 1 | No | Yes | Triggers Resounding Melody Soundwaves |
| Resounding Melody 3 | 20 | 30 | 10 | 0 | 1 | 1 | 0.4 | 1 | No | Yes | Triggers Resounding Melody Soundwaves |
| Resounding Melody Soundwaves | 15 | 3 | 3 | 0 | 0.5 | 0.5 | Yellow | 1 | No | No | Raw damage receives a 0.925x multiplier on the 3rd hit, 0.85x on the 4th, 0.65x on the 5th, 0.588x on the 6th, 0.525x on the 7th, 0.463x on the 8th, 0.4x on the 9th, and 0.25x on any subsequent hit. |
| Perform Sound Wave | 27 | 5 | 5 | 0 | 1 | 1 | Yellow | 1 | No | No |  |
| Song Soundwave | 10 | 5 | 5 | 0 | 1 | 1 | Yellow | 1 | No | No |  |
| Encore Song Soundwave | 10 | 5 | 5 | 0 | 1 | 1 | Green | 1 | No | No | Does not scale with raw. Likely a bug |
| Echo Bubble Place | 45 | 0 | 0 | 0 | 0.3 | 0.3 | Yellow | 1 | No | No | Deals Hitzone-ignoring damage |
| Echo Bubble Note Resonance | 5 | 0 | 0 | 0 | 0.3 | 0.3 | Yellow | 1 | No | No | Deals Hitzone-ignoring damage |
| Echo Bubble Song Resonance | 12 | 0 | 0 | 0 | 0.3 | 0.3 | Yellow | 1 | No | No | Deals Hitzone-ignoring damage |
| Echo Wave Blunt | 50 | 30 | 25 | 0 | 0 | 0 | Yellow | 1.1 | No | No |  |
| Echo Wave Blunt x2 | 70 | 60 | 40 | 0 | 0 | 0 | Yellow | 1.1 | No | No |  |
| Echo Wave Blunt x3 | 100 | 90 | 60 | 0 | 0 | 0 | Yellow | 1.1 | No | No |  |
| Echo Wave Slash | 50 | 0 | 0 | 0 | 0 | 0 | Yellow | 1.1 | No | Yes | Deals Sever damage |
| Echo Wave Slash x2 | 70 | 0 | 0 | 0 | 0 | 0 | Yellow | 1.1 | No | Yes | Deals Sever damage |
| Echo Wave Slash x3 | 100 | 0 | 0 | 0 | 0 | 0 | Yellow | 1.1 | No | Yes | Deals Sever damage |
| Echo Wave Element | 45 | 0 | 0 | 0 | 60 | 0 | Yellow | 1.1 | No | No | Elemental Value is fixed |
| Echo Wave Element x2 | 65 | 0 | 0 | 0 | 100 | 0 | Yellow | 1.1 | No | No | Elemental Value is fixed |
| Echo Wave Element x3 | 85 | 0 | 0 | 0 | 140 | 0 | Yellow | 1.1 | No | No | Elemental Value is fixed |
| Echo Wave Status | 45 | 0 | 0 | 0 | 0 | 50 | Yellow | 1.1 | No | No | Status Value is fixed |
| Echo Wave Status x2 | 65 | 0 | 0 | 0 | 0 | 100 | Yellow | 1.1 | No | No | Status Value is fixed |
| Echo Wave Status x3 | 85 | 0 | 0 | 0 | 0 | 150 | Yellow | 1.1 | No | No | Status Value is fixed |

### Lance (gid 132328400)

| Attack | Motion Value | Stun | Exhaust | Mount | Element | Status | Sharpness | Can Bounce | Notes |
|---|---|---|---|---|---|---|---|---|---|
| Mid Thrust I/II | 26 | 0 | 0 | 0 | 1.2 | 1 | 1 | Yes |  |
| Mid Thrust III | 35 | 0 | 0 | 0 | 1.4 | 1 | 1 | Yes |  |
| High Thrust I/II | 31 | 0 | 0 | 0 | 1 | 1 | 1 | Yes |  |
| High Thrust III | 42 | 0 | 0 | 0 | 1 | 1 | 1 | Yes |  |
| Triple Thrust 1/2 | 25 | 0 | 0 | 0 | 1.3 | 1 | 0.3 | Yes |  |
| Triple Thrust 3 | 42 | 0 | 0 | 0 | 1.5 | 1 | 0.4 | Yes |  |
| Charge Counter Lv0 | 43 | 0 | 0 | 0 | 1 | 1 | 1 | No |  |
| Charge Counter Lv1 | 51.6 | 0 | 0 | 0 | 1 | 1 | 1 | No | Actually a 1.2x multiplier |
| Charge Counter Lv2 | 60.2 | 0 | 0 | 0 | 1 | 1 | 1 | No | Actually a 1.4x multiplier |
| Charge Counter Lv3 | 73.1 | 0 | 0 | 0 | 1 | 1 | 1 | No | Actually a 1.7x multiplier |
| Double Counter-thrust 1 Lv0 | 33 | 0 | 0 | 0 | 1 | 1 | 0.5 | No |  |
| Double Counter-thrust 1 Lv1 | 39.6 | 0 | 0 | 0 | 1 | 1 | 0.5 | No | Actually a 1.2x multiplier |
| Double Counter-thrust 1 Lv2 | 46.2 | 0 | 0 | 0 | 1 | 1 | 0.5 | No | Actually a 1.4x multiplier |
| Double Counter-thrust 1 Lv3 | 56.1 | 0 | 0 | 0 | 1 | 1 | 0.5 | No | Actually a 1.7x multiplier |
| Double Counter-thrust 2 Lv0 | 50 | 0 | 0 | 0 | 1 | 1 | 0.5 | No |  |
| Double Counter-thrust 2 Lv1 | 60 | 0 | 0 | 0 | 1 | 1 | 0.5 | No | Actually a 1.2x multiplier |
| Double Counter-thrust 2 Lv2 | 70 | 0 | 0 | 0 | 1 | 1 | 0.5 | No | Actually a 1.4x multiplier |
| Double Counter-thrust 2 Lv3 | 85 | 0 | 0 | 0 | 1 | 1 | 0.5 | No | Actually a 1.7x multiplier |
| Leaping Thrust | 29 | 0 | 0 | 0 | 1.2 | 1.2 | 0.5 | No | Hits twice |
| Wide Sweep | 80 | 0 | 0 | 0 | 1.4 | 1 | 1 | Yes |  |
| Shield Attack | 15 | 20 | 20 | 0 | 0 | 0 | 0 | No |  |
| Guard Thrust | 20 | 0 | 0 | 0 | 1 | 1 | 1 | Yes |  |
| Dash Attack | 17 | 0 | 0 | 0 | 0.5 | 0.5 | 0.5 | Yes |  |
| Air Dash Attack | 25 | 0 | 0 | 0 | 0.5 | 0.5 | 1 | No |  |
| Finishing Thrust | 55 | 0 | 0 | 0 | 1.4 | 1 | 1 | No |  |
| Finishing Twin Thrust 1 | 35 | 0 | 0 | 0 | 1.6 | 1 | 1 | No |  |
| Finishing Twin Thrust 2 | 70 | 0 | 0 | 0 | 1.6 | 1 | 1 | No |  |
| Reverse Attack | 53 | 0 | 0 | 0 | 1.4 | 1 | 1 | Yes |  |
| High Speed Reverse Attack | 70 | 0 | 0 | 0 | 1.4 | 1 | 1 | Yes |  |
| Dash Jump Attack | 30 | 0 | 0 | 50 | 0.5 | 0.5 | 1 | No |  |
| High Speed Dash Jump Attack | 33 | 0 | 0 | 60 | 0.5 | 0.5 | 1 | No |  |
| Jump Attack | 30 | 0 | 0 | 50 | 1 | 1 | 1 | No |  |
| Shield Counter | 20 | 15 | 15 | 0 | 0 | 0 | 0 | No |  |
| Perfect Shield Counter | 25 | 20 | 20 | 0 | 0 | 0 | 0 | No |  |
| Return Thrust | 24 | 0 | 0 | 0 | 1.2 | 1 | 1 | Yes |  |
| Perfect Return Thrust | 31 | 0 | 0 | 0 | 1.4 | 1 | 1 | Yes | After Perfect Guard |
| Power Return Thrust Lv1 | 26.4 | 0 | 0 | 0 | 1.4 | 1 | 1 | Yes | After Power Guard grants white aura. Is actually a 1.1x multiplier on the base version |
| Power Return Thrust Lv2 | 31.2 | 0 | 0 | 0 | 1.4 | 1 | 1 | Yes | After Power Guard grants yellow aura. Is actually a 1.3x multiplier on the base version |
| Power Return Thrust Lv3 | 40.8 | 0 | 0 | 0 | 1.4 | 1 | 1 | Yes | After Power Guard grants red aura. Is actually a 1.7x multiplier on the base version |
| Payback Thrust 1 | 30 | 0 | 0 | 0 | 1 | 1 | 0.5 | No |  |
| Payback Thrust 2 | 25 | 0 | 0 | 0 | 1 | 1 | 0.5 | No |  |
| Perfect Payback Thrust 1 | 42 | 0 | 0 | 0 | 1.4 | 1 | 0.5 | No | After Perfect Guard |
| Perfect Payback Thrust 2 | 25 | 0 | 0 | 0 | 1 | 1 | 0.5 | No | After Perfect Guard |
| Power Payback Thrust 1 Lv1 | 33 | 0 | 0 | 0 | 1 | 1 | 0.5 | No | After Power Guard grants white aura. Is actually a 1.1x multiplier on the base version |
| Power Payback Thrust 2 Lv1 | 27.5 | 0 | 0 | 0 | 1 | 1 | 0.5 | No | After Power Guard grants white aura. Is actually a 1.1x multiplier on the base version |
| Power Payback Thrust 1 Lv2 | 39 | 0 | 0 | 0 | 1 | 1 | 0.5 | No | After Power Guard grants yellow aura. Is actually a 1.3x multiplier on the base version |
| Power Payback Thrust 2 Lv2 | 32.5 | 0 | 0 | 0 | 1 | 1 | 0.5 | No | After Power Guard grants yellow aura. Is actually a 1.3x multiplier on the base version |
| Power Payback Thrust 1 Lv3 | 51 | 0 | 0 | 0 | 1 | 1 | 0.5 | No | After Power Guard grants red aura. Is actually a 1.7x multiplier on the base version |
| Power Payback Thrust 2 Lv3 | 42.5 | 0 | 0 | 0 | 1 | 1 | 0.5 | No | After Power Guard grants red aura. Is actually a 1.7x multiplier on the base version |
| Retribution Thrust 1 | 20 | 0 | 0 | 0 | 1 | 1 | 0.3 | No |  |
| Retribution Thrust 2 | 30 | 0 | 0 | 0 | 1.4 | 1 | 0.4 | No |  |
| Perfect Retribution Thrust 1 | 20 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | After Perfect Guard |
| Perfect Retribution Thrust 2 | 58 | 0 | 0 | 0 | 1.6 | 1 | 0.4 | No | After Perfect Guard |
| Grand Retribution Thrust 1 Lv0 | 56 | 0 | 0 | 0 | 1 | 1 | 0.4 | No | After a hit with Power Guard. Never available since it always grants you some aura |
| Grand Retribution Thrust 2 Lv0 | 20 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | After a hit with Power Guard. Never available since it always grants you some aura |
| Grand Retibution Thrust 3 Lv0 | 12 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | After a hit with Power Guard. Never available since it always grants you some aura |
| Grand Retribution Thrust 1 Lv1 | 61.6 | 0 | 0 | 0 | 1 | 1 | 0.4 | No | After Power Guard grants white aura. Is actually a 1.1x multiplier on the base version |
| Grand Retribution Thrust 2 Lv1 | 22 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | After Power Guard grants white aura. Is actually a 1.1x multiplier on the base version |
| Grand Retibution Thrust 3 Lv1 | 13.2 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | After Power Guard grants white aura. Is actually a 1.1x multiplier on the base version |
| Grand Retribution Thrust 1 Lv2 | 72.8 | 0 | 0 | 0 | 1 | 1 | 0.4 | No | After Power Guard grants yellow aura. Is actually a 1.3x multiplier on the base version |
| Grand Retribution Thrust 2 Lv2 | 26 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | After Power Guard grants yellow aura. Is actually a 1.3x multiplier on the base version |
| Grand Retibution Thrust 3 Lv2 | 15.6 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | After Power Guard grants yellow aura. Is actually a 1.3x multiplier on the base version |
| Grand Retribution Thrust 1 Lv3 | 95.2 | 0 | 0 | 0 | 1 | 1 | 0.4 | No | After Power Guard grants red aura. Is actually a 1.7x multiplier on the base version |
| Grand Retribution Thrust 2 Lv3 | 34 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | After Power Guard grants red aura. Is actually a 1.7x multiplier on the base version |
| Grand Retibution Thrust 3 Lv3 | 20.4 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | After Power Guard grants red aura. Is actually a 1.7x multiplier on the base version |
| Follow-up Dash Attack 1 | 50 | 0 | 0 | 0 | 1.2 | 1.2 | 1 | No | No longer applied? Why was it buffed then? |
| Follow-up Dash Attack 2/4 | 17 | 0 | 0 | 0 | 0.5 | 1 | 0.5 | No |  |
| Follow-up Dash Attack 3 | 24 | 0 | 0 | 0 | 1 | 1 | 1 | No |  |
| Seikret Charge | 10 | 0 | 0 | 0 | 0.5 | 0.5 | 0.5 | No |  |
| Seikret Upswing | 30 | 0 | 0 | 0 | 1 | 1 | 1 | No |  |
| Seikret Sweep | 35 | 0 | 0 | 0 | 1 | 1 | 1 | No |  |
| Dismount Attack | 20 | 0 | 0 | 30 | 1 | 1 | 1 | No |  |
| Seikret Charge Dismount Attack 1 | 10 | 20 | 20 | 30 | 0 | 0 | 0 | No |  |
| Seikret Charge Dismount Attack 2 | 30 | 0 | 0 | 0 | 1 | 1 | 1 | No |  |
| Victory Thrust | 20 | 20 | 20 | 0 | 1 | 1 | 0 | No |  |
| Victory Thrust Charge | 15 | 5 | 5 | 0 | 1 | 1 | 0 | No | Hits up to 4 times |
| Victory Thrust Resolve | 40 | 0 | 0 | 0 | 1.2 | 1 | 0.3 | No | Hits 3 times |
| Clash Finisher 1 | 60 | 20 | 20 | 0 | 0 | 0 | 0 | No |  |
| Clash Finisher 2 | 120 | 0 | 0 | 0 | 1 | 1 | 1 | No |  |
| Laser Counter Finisher | 70 | 0 | 0 | 0 | 1.4 | 1 | 1 | No |  |
| Mounted Attack | 37 | 0 | 0 | 0 | 1 | 1 | 0.1 | No |  |
| Mounted Finisher | 180 | 0 | 0 | 0 | 1 | 1 | 0.1 | No |  |
| Sneak Attack | 106 | 0 | 0 | 0 | 1 | 1 | 1 | No | Deals hitzone-ignoring damage |
| Aerial Sneak Attack | 106 | 0 | 0 | 0 | 1 | 1 | 1 | No | Deals hitzone-ignoring damage |
| Underwater Attack 1 | 77 | 0 | 0 | 0 | 1 | 1 | 1 | No |  |
| Underwater Attack 2 | 73 | 0 | 0 | 0 | 1 | 1 | 1 | No |  |

### Gunlance (gid 1366970871)

| Attack | Motion Value | Mount | Element | Status | Sharpness | Part Break | Can Bounce | Gauge generation | Notes |
|---|---|---|---|---|---|---|---|---|---|
| Lateral Thrust I/II | 24 | 0 | 2 | 1 | 1 | 1 | Yes | 2 |  |
| Lunging Upthrust | 38 | 0 | 1.2 | 1 | 1 | 1 | Yes | 2 |  |
| Rising Slash | 38 | 0 | 1.2 | 1 | 1 | 1 | Yes | 2 |  |
| Guard Thrust | 18 | 0 | 2 | 1 | 1 | 1 | Yes | 1 |  |
| Overhead Smash | 57 | 0 | 2 | 1 | 1 | 1 | No | 4 |  |
| Sweep/Moving Sweep/Aerial Burst | 40 | 0 | 1.5 | 1.5 | 1 | 1 | No | 4 |  |
| Wyrmstake Full Blast Sweep | 30 | 0 | 1 | 1 | 1 | 1 | No | 4 |  |
| Multi Wyrmstake Full Blast Sweep | 30 | 0 | 1 | 1 | 1 | 1 | No | 8 |  |
| Wyrmstake Thrust | 40 | 0 | 2 | 1 | 1 | 1 | No | 3 |  |
| Wyrmstake Full Blast Thrust | 20 | 0 | 1 | 1 | 1 | 1 | No | 4 |  |
| Wyrmstake Attach | 25 | 0 | 2 | 1 | 1 | 1 | No | 0 |  |
| Normal/Wide Wyrmstake Lv1 Ticks | 5 | 0 | 0.5 | 0.1 | 0 | 1 | No | 0 | Number and frequency of ticks depends on attack used to place it |
| Normal/Wide Wyrmstake Lv2 Ticks | 6 | 0 | 0.5 | 0.1 | 0 | 1 | No | 0 | Number and frequency of ticks depends on attack used to place it |
| Normal/Wide Wyrmstake Lv3 Ticks | 7 | 0 | 0.5 | 0.1 | 0 | 1 | No | 0 | Number and frequency of ticks depends on attack used to place it |
| Normal/Wide Wyrmstake Lv4 Ticks | 8 | 0 | 0.5 | 0.1 | 0 | 1 | No | 0 | Number and frequency of ticks depends on attack used to place it |
| Normal/Wide Wyrmstake Lv5 Ticks | 9 | 0 | 0.5 | 0.1 | 0 | 1 | No | 0 | Not available. Number and frequency of ticks depends on attack used to place it |
| Normal/Wide Wyrmstake Lv6 Ticks | 10 | 0 | 0.5 | 0.1 | 0 | 1 | No | 0 | Not available. Number and frequency of ticks depends on attack used to place it |
| Normal/Wide Wyrmstake Lv7 Ticks | 11 | 0 | 0.5 | 0.1 | 0 | 1 | No | 0 | Not available. Number and frequency of ticks depends on attack used to place it |
| Long Wyrmstake Lv1 Ticks | 6 | 0 | 0.5 | 0.1 | 0 | 1 | No | 0 | Number and frequency of ticks depends on attack used to place it |
| Long Wyrmstake Lv2 Ticks | 7 | 0 | 0.5 | 0.1 | 0 | 1 | No | 0 | Number and frequency of ticks depends on attack used to place it |
| Long Wyrmstake Lv3 Ticks | 8 | 0 | 0.5 | 0.1 | 0 | 1 | No | 0 | Number and frequency of ticks depends on attack used to place it |
| Long Wyrmstake Lv4 Ticks | 9 | 0 | 0.5 | 0.1 | 0 | 1 | No | 0 | Number and frequency of ticks depends on attack used to place it |
| Long Wyrmstake Lv5 Ticks | 10 | 0 | 0.5 | 0.1 | 0 | 1 | No | 0 | Not available. Number and frequency of ticks depends on attack used to place it |
| Long Wyrmstake Lv6 Ticks | 11 | 0 | 0.5 | 0.1 | 0 | 1 | No | 0 | Not available. Number and frequency of ticks depends on attack used to place it |
| Long Wyrmstake Lv7 Ticks | 12 | 0 | 0.5 | 0.1 | 0 | 1 | No | 0 | Not available. Number and frequency of ticks depends on attack used to place it |
| Jumping Thrust | 25 | 55 | 1 | 1 | 1 | 1 | No | 3 |  |
| Jumping Smash | 44 | 35 | 1 | 1 | 1 | 1 | No | 3 |  |
| Jumping Rising Slash | 35 | 0 | 1 | 1 | 1 | 1 | No | 2 |  |
| Seikret Charge | 10 | 0 | 0.5 | 0.5 | 0.3 | 1 | No | 1 |  |
| Seikret Upswing | 30 | 0 | 1 | 1 | 1 | 1 | No | 2 |  |
| Seikret Slam | 48 | 0 | 1 | 1 | 1 | 1 | No | 3 |  |
| Drake Auger | 18 | 0 | 1 | 1 | 1 | 1 | No | 3 |  |
| Drake Auger Ticks | 7 | 0 | 0.5 | 0.1 | 1 | 1 | No | 0.3 | Followed by a Wyrmstake |
| Mount Attack | 15 | 0 | 1 | 1 | 0.3 | 1 | No | 0 | Followed by a shell |
| Mount Finisher | 10 | 0 | 1 | 1 | 0.3 | 1 | No | 0 |  |
| Mount Finisher Ticks | 7 | 0 | 0.5 | 0.1 | 0 | 1 | No | 0 | Followed by a Wyrmstake |
| Sneak Attack | 106 | 0 | 1 | 1 | 1 | 1 | No | 10 | Deals hitzone-ignoring damage |
| Aerial Sneak Attack 1 | 50 | 0 | 1 | 1 | 1 | 1 | No | 10 | Deals hitzone-ignoring damage |
| Aerial Sneak Attack 2 | 56 | 0 | 1 | 1 | 1 | 1 | No | 10 | Deals hitzone-ignoring damage |
| Underwater Attack 1 | 70 | 0 | 0 | 0 | 1 | 1 | No | 1 |  |
| Underwater Attack 2 | 80 | 0 | 0 | 0 | 1 | 1 | No | 1 |  |

### Gunlance shelling (gid 1000365357)

| Attack | Hitzone-ignoring | Fire (fixed) | Sharpness | Gauge gain |  |
|---|---|---|---|---|---|
| Normal Shell Lv1 (Slightly Weak) | 7 | 6 | 0.2 | 2.5 |  |
| Normal Shell Lv2 (Normal) | 8 | 7 | 0.2 | 2.5 |  |
| Normal Shell Lv3 (Slightly Strong) | 9 | 8 | 0.2 | 2.5 |  |
| Normal Shell Lv4 (Strong) | 10 | 9 | 0.2 | 2.5 |  |
| Normal Shell Lv5 | 11 | 10 | 0.2 | 2.5 | Not available |
| Normal Shell Lv6 | 12 | 11 | 0.2 | 2.5 | Not available |
| Normal Shell Lv7 | 13 | 12 | 0.2 | 2.5 | Not available |
| Fullburst Normal Shell Lv1 (Slightly Weak) | 9.8 | 6 | 0.1 | 1.2 |  |
| Fullburst Normal Shell Lv2 (Normal) | 11.2 | 7 | 0.1 | 1.2 |  |
| Fullburst Normal Shell Lv3 (Slightly Strong) | 12.6 | 8 | 0.1 | 1.2 |  |
| Fullburst Normal Shell Lv4 (Strong) | 14 | 9 | 0.1 | 1.2 |  |
| Fullburst Normal Shell Lv5 | 15.4 | 10 | 0.1 | 1.2 | Not available |
| Fullburst Normal Shell Lv6 | 16.8 | 11 | 0.1 | 1.2 | Not available |
| Fullburst Normal Shell Lv7 | 18.2 | 12 | 0.1 | 1.2 | Not available |
| Wyrmstake Fullburst Normal Shell Lv1 (Slightly Weak) | 8.75 | 6 | 0.1 | 1.2 |  |
| Wyrmstake Fullburst Normal Shell Lv2 (Normal) | 10 | 7 | 0.1 | 1.2 |  |
| Wyrmstake Fullburst Normal Shell Lv3 (Slightly Strong) | 11.25 | 8 | 0.1 | 1.2 |  |
| Wyrmstake Fullburst Normal Shell Lv4 (Strong) | 12.5 | 9 | 0.1 | 1.2 |  |
| Wyrmstake Fullburst Normal Shell Lv5 | 13.75 | 10 | 0.1 | 1.2 | Not available |
| Wyrmstake Fullburst Normal Shell Lv6 | 15 | 11 | 0.1 | 1.2 | Not available |
| Wyrmstake Fullburst Normal Shell Lv7 | 16.25 | 12 | 0.1 | 1.2 | Not available |
| Charged Normal Shell Lv1 (Slightly Weak) | 12.6 | 6 | 0.3 | 8 |  |
| Charged Normal Shell Lv2 (Normal) | 14.4 | 7 | 0.3 | 8 |  |
| Charged Normal Shell Lv3 (Slightly Strong) | 16.2 | 8 | 0.3 | 8 |  |
| Charged Normal Shell Lv4 (Strong) | 18 | 9 | 0.3 | 8 |  |
| Charged Normal Shell Lv5 | 19.8 | 10 | 0.3 | 8 | Not available |
| Charged Normal Shell Lv6 | 21.6 | 11 | 0.3 | 8 | Not available |
| Charged Normal Shell Lv7 | 23.4 | 12 | 0.3 | 8 | Not available |
| Normal Wyvernfire Lv1 (Slightly Weak) | 39 | 22 | 2 | 0 |  |
| Normal Wyvernfire Lv2 (Normal) | 43 | 24 | 2 | 0 |  |
| Normal Wyvernfire Lv3 (Slightly Strong) | 47 | 26 | 2 | 0 |  |
| Normal Wyvernfire Lv4 (Strong) | 52 | 29 | 2 | 0 |  |
| Normal Wyvernfire Lv5 | 57 | 32 | 2 | 0 | Not available |
| Normal Wyvernfire Lv6 | 63 | 35 | 2 | 0 | Not available |
| Normal Wyvernfire Lv7 | 69 | 38 | 2 | 0 | Not available |
| Normal Wyrmstake Explosion Lv1 (Slightly Weak) | 23 | 19 | 0.5 | 0 | 10% more raw damage if placed with Multi Wyrmstake Full Blast |
| Normal Wyrmstake Explosion Lv2 (Normal) | 25 | 21 | 0.5 | 0 | 10% more raw damage if placed with Multi Wyrmstake Full Blast |
| Normal Wyrmstake Explosion Lv3 (Slightly Strong) | 28 | 23 | 0.5 | 0 | 10% more raw damage if placed with Multi Wyrmstake Full Blast |
| Normal Wyrmstake Explosion Lv4 (Strong) | 31 | 25 | 0.5 | 0 | 10% more raw damage if placed with Multi Wyrmstake Full Blast |
| Normal Wyrmstake Explosion Lv5 | 34 | 27 | 0.5 | 0 | Not available. 10% more raw damage if placed with Multi Wyrmstake Full Blast |
| Normal Wyrmstake Explosion Lv6 | 37 | 31 | 0.5 | 0 | Not available. 10% more raw damage if placed with Multi Wyrmstake Full Blast |
| Normal Wyrmstake Explosion Lv7 | 41 | 34 | 0.5 | 0 | Not available. 10% more raw damage if placed with Multi Wyrmstake Full Blast |
| Long Shell Lv1 (Slightly Weak) | 10 | 14 | 0.2 | 3.25 |  |
| Long Shell Lv2 (Normal) | 11 | 15 | 0.2 | 3.25 |  |
| Long Shell Lv3 (Slightly Strong) | 12 | 16 | 0.2 | 3.25 |  |
| Long Shell Lv4 (Strong) | 13 | 17 | 0.2 | 3.25 |  |
| Long Shell Lv5 | 14 | 18 | 0.2 | 3.25 | Not available |
| Long Shell Lv6 | 15 | 19 | 0.2 | 3.25 | Not available |
| Long Shell Lv7 | 16 | 20 | 0.2 | 3.25 | Not available |
| Fullburst Long Shell Lv1 (Slightly Weak) | 14 | 14 | 0.3 | 1.56 |  |
| Fullburst Long Shell Lv2 (Normal) | 15.4 | 15 | 0.3 | 1.56 |  |
| Fullburst Long Shell Lv3 (Slightly Strong) | 16.8 | 16 | 0.3 | 1.56 |  |
| Fullburst Long Shell Lv4 (Strong) | 18.2 | 17 | 0.3 | 1.56 |  |
| Fullburst Long Shell Lv5 | 19.6 | 18 | 0.3 | 1.56 | Not available |
| Fullburst Long Shell Lv6 | 21 | 19 | 0.3 | 1.56 | Not available |
| Fullburst Long Shell Lv7 | 22.4 | 20 | 0.3 | 1.56 | Not available |
| Wyrmstake Fullburst Long Shell Lv1 (Slightly Weak) | 11 | 14 | 0.3 | 1.56 |  |
| Wyrmstake Fullburst Long Shell Lv2 (Normal) | 12.1 | 15 | 0.3 | 1.56 |  |
| Wyrmstake Fullburst Long Shell Lv3 (Slightly Strong) | 13.2 | 16 | 0.3 | 1.56 |  |
| Wyrmstake Fullburst Long Shell Lv4 (Strong) | 14.3 | 17 | 0.3 | 1.56 |  |
| Wyrmstake Fullburst Long Shell Lv5 | 15.4 | 18 | 0.3 | 1.56 | Not available |
| Wyrmstake Fullburst Long Shell Lv6 | 16.5 | 19 | 0.3 | 1.56 | Not available |
| Wyrmstake Fullburst Long Shell Lv7 | 17.6 | 20 | 0.3 | 1.56 | Not available |
| Charged Long Shell Lv1 (Slightly Weak) | 18 | 14 | 0.3 | 10.4 |  |
| Charged Long Shell Lv2 (Normal) | 19.8 | 15 | 0.3 | 10.4 |  |
| Charged Long Shell Lv3 (Slightly Strong) | 21.6 | 16 | 0.3 | 10.4 |  |
| Charged Long Shell Lv4 (Strong) | 23.4 | 17 | 0.3 | 10.4 |  |
| Charged Long Shell Lv5 | 25.2 | 18 | 0.3 | 10.4 | Not available |
| Charged Long Shell Lv6 | 27 | 19 | 0.3 | 10.4 | Not available |
| Charged Long Shell Lv7 | 28.8 | 20 | 0.3 | 10.4 | Not available |
| Long Wyvernfire Lv1 (Slightly Weak) | 39 | 22 | 2 | 0 |  |
| Long Wyvernfire Lv2 (Normal) | 43 | 24 | 2 | 0 |  |
| Long Wyvernfire Lv3 (Slightly Strong) | 47 | 26 | 2 | 0 |  |
| Long Wyvernfire Lv4 (Strong) | 52 | 29 | 2 | 0 |  |
| Long Wyvernfire Lv5 | 57 | 32 | 2 | 0 | Not available |
| Long Wyvernfire Lv6 | 63 | 35 | 2 | 0 | Not available |
| Long Wyvernfire Lv7 | 69 | 38 | 2 | 0 | Not available |
| Long Wyrmstake Explosion Lv1 (Slightly Weak) | 32 | 19 | 0.5 | 0 | 10% more raw damage if placed with Multi Wyrmstake Full Blast |
| Long Wyrmstake Explosion Lv2 (Normal) | 37 | 21 | 0.5 | 0 | 10% more raw damage if placed with Multi Wyrmstake Full Blast |
| Long Wyrmstake Explosion Lv3 (Slightly Strong) | 42 | 23 | 0.5 | 0 | 10% more raw damage if placed with Multi Wyrmstake Full Blast |
| Long Wyrmstake Explosion Lv4 (Strong) | 49 | 25 | 0.5 | 0 | 10% more raw damage if placed with Multi Wyrmstake Full Blast |
| Long Wyrmstake Explosion Lv5 | 56 | 27 | 0.5 | 0 | Not available. 10% more raw damage if placed with Multi Wyrmstake Full Blast |
| Long Wyrmstake Explosion Lv6 | 63 | 30 | 0.5 | 0 | Not available. 10% more raw damage if placed with Multi Wyrmstake Full Blast |
| Long Wyrmstake Explosion Lv7 | 70 | 33 | 0.5 | 0 | Not available. 10% more raw damage if placed with Multi Wyrmstake Full Blast |
| Wide Shell Lv1 (Slightly Weak) | 17 | 9 | 0.4 | 6.25 |  |
| Wide Shell Lv2 (Normal) | 19 | 10 | 0.4 | 6.25 |  |
| Wide Shell Lv3 (Slightly Strong) | 21 | 11 | 0.4 | 6.25 |  |
| Wide Shell Lv4 (Strong) | 23 | 12 | 0.4 | 6.25 |  |
| Wide Shell Lv5 | 25 | 13 | 0.4 | 6.25 | Not available |
| Wide Shell Lv6 | 27 | 14 | 0.4 | 6.25 | Not available |
| Wide Shell Lv7 | 30 | 15 | 0.4 | 6.25 | Not available |
| Fullburst Wide Shell Lv1 (Slightly Weak) | 22.95 | 9 | 0.3 | 3 |  |
| Fullburst Wide Shell Lv2 (Normal) | 25.65 | 10 | 0.3 | 3 |  |
| Fullburst Wide Shell Lv3 (Slightly Strong) | 28.35 | 11 | 0.3 | 3 |  |
| Fullburst Wide Shell Lv4 (Strong) | 31.05 | 12 | 0.3 | 3 |  |
| Fullburst Wide Shell Lv5 | 33.75 | 13 | 0.3 | 3 | Not available |
| Fullburst Wide Shell Lv6 | 36.45 | 14 | 0.3 | 3 | Not available |
| Fullburst Wide Shell Lv7 | 40.5 | 15 | 0.3 | 3 | Not available |
| Wyrmstake Fullburst Wide Shell Lv1 (Slightly Weak) | 17.85 | 9 | 0.3 | 3 |  |
| Wyrmstake Fullburst Wide Shell Lv2 (Normal) | 19.95 | 10 | 0.3 | 3 |  |
| Wyrmstake Fullburst Wide Shell Lv3 (Slightly Strong) | 22.05 | 11 | 0.3 | 3 |  |
| Wyrmstake Fullburst Wide Shell Lv4 (Strong) | 24.15 | 12 | 0.3 | 3 |  |
| Wyrmstake Fullburst Wide Shell Lv5 | 26.25 | 13 | 0.3 | 3 | Not available |
| Wyrmstake Fullburst Wide Shell Lv6 | 28.35 | 14 | 0.3 | 3 | Not available |
| Wyrmstake Fullburst Wide Shell Lv7 | 31.5 | 15 | 0.3 | 3 | Not available |
| Charged Wide Shell Lv1 (Slightly Weak) | 30.6 | 9 | 0.5 | 20 |  |
| Charged Wide Shell Lv2 (Normal) | 34.2 | 10 | 0.5 | 20 |  |
| Charged Wide Shell Lv3 (Slightly Strong) | 37.8 | 11 | 0.5 | 20 |  |
| Charged Wide Shell Lv4 (Strong) | 41.4 | 12 | 0.5 | 20 |  |
| Charged Wide Shell Lv5 | 45 | 13 | 0.5 | 20 | Not available |
| Charged Wide Shell Lv6 | 48.6 | 14 | 0.5 | 20 | Not available |
| Charged Wide Shell Lv7 | 54 | 15 | 0.5 | 20 | Not available |
| Wide Wyvernfire Lv1 (Slightly Weak) | 46 | 24 | 2 | 0 |  |
| Wide Wyvernfire Lv2 (Normal) | 50 | 26 | 2 | 0 |  |
| Wide Wyvernfire Lv3 (Slightly Strong) | 55 | 29 | 2 | 0 |  |
| Wide Wyvernfire Lv4 (Strong) | 60 | 32 | 2 | 0 |  |
| Wide Wyvernfire Lv5 | 66 | 35 | 2 | 0 | Not available |
| Wide Wyvernfire Lv6 | 73 | 38 | 2 | 0 | Not available |
| Wide Wyvernfire Lv7 | 80 | 42 | 2 | 0 | Not available |
| Wide Wyrmstake Explosion Lv1 (Slightly Weak) | 23 | 19 | 0.5 | 0 | 10% more raw damage if placed with Multi Wyrmstake Full Blast |
| Wide Wyrmstake Explosion Lv2 (Normal) | 25 | 21 | 0.5 | 0 | 10% more raw damage if placed with Multi Wyrmstake Full Blast |
| Wide Wyrmstake Explosion Lv3 (Slightly Strong) | 27 | 23 | 0.5 | 0 | 10% more raw damage if placed with Multi Wyrmstake Full Blast |
| Wide Wyrmstake Explosion Lv4 (Strong) | 30 | 25 | 0.5 | 0 | 10% more raw damage if placed with Multi Wyrmstake Full Blast |
| Wide Wyrmstake Explosion Lv5 | 33 | 27 | 0.5 | 0 | Not available. 10% more raw damage if placed with Multi Wyrmstake Full Blast |
| Wide Wyrmstake Explosion Lv6 | 36 | 30 | 0.5 | 0 | Not available. 10% more raw damage if placed with Multi Wyrmstake Full Blast |
| Wide Wyrmstake Explosion Lv7 | 40 | 33 | 0.5 | 0 | Not available. 10% more raw damage if placed with Multi Wyrmstake Full Blast |

### Charge Blade (gid 1512765617)

| Attack | Motion Value | Stun | Exhaust | Mount | Element | Status | Sharpness | Can Bounce | Can Crit | Gauge generation | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Sword Forward Slash | 18 | 0 | 0 | 0 | 1 | 1 | 1 | Yes | Yes | 3 |  |
| Sword Weak Slash | 15 | 0 | 0 | 0 | 1 | 1 | 1 | Yes | Yes | 3 |  |
| Sword Return Stroke | 18 | 0 | 0 | 0 | 1 | 1 | 1 | Yes | Yes | 3 |  |
| Sword Roundslash/Axe Morph Slash/Elemental Roundslash | 30 | 0 | 0 | 0 | 1 | 1 | 1 | Yes | Yes | 9 |  |
| Sword Fade Slash | 24 | 0 | 0 | 0 | 1 | 1 | 1 | Yes | Yes | 3 |  |
| Charged Rising Slash | 15 | 0 | 0 | 0 | 1 | 1 | 1 | Yes | Yes | 3 |  |
| Charged Double Slash 1 | 25 | 0 | 0 | 0 | 1 | 1 | 0.5 | Yes | Yes | 18 |  |
| Charged Double Slash 2 | 16 | 0 | 0 | 0 | 1 | 1 | 0.5 | Yes | Yes | 9 |  |
| Sword Shield Thrust 1 | 15 | 0 | 0 | 0 | 1 | 1 | 0.5 | Yes | Yes | 0 |  |
| Sword Shield Thrust 2 | 6 | 0 | 0 | 0 | 1 | 1 | 0.5 | Yes | Yes | 0 |  |
| Sword Condensed Elemental Slash | 48 | 0 | 0 | 0 | 1 | 1 | 1 | No | Yes | 9 |  |
| Sword Condensed Elemental Slash Impact Explosion | 25 | 35 | 7 | 0 | 0 | 0 | Yellow | No | Yes | 0 |  |
| Sword Condensed Elemental Slash Elemental Explosion | 0 | 0 | 0 | 0 | 4 | 0 | Yellow | No | Yes | 0 |  |
| Sword Morph Slash | 54 | 0 | 0 | 0 | 1 | 1 | 1 | Yes | Yes | 0 |  |
| Axe Overhead Slash | 54 | 0 | 0 | 0 | 1 | 1 | 1 | Yes | Yes | 0 |  |
| Axe Rising Slash | 38 | 0 | 0 | 0 | 1 | 1 | 1 | Yes | Yes | 0 |  |
| Axe Dash Slam | 48 | 0 | 0 | 0 | 1 | 1 | 1 | Yes | Yes | 0 |  |
| Axe Lateral Fade Slash/Backstep Slash | 42 | 0 | 0 | 0 | 1 | 1 | 1 | Yes | Yes | 0 |  |
| Axe Elemental Discharge I | 53 | 0 | 0 | 0 | 1 | 1 | 0.4 | No | Yes | 0 |  |
| Axe Elemental Discharge II 1 | 35 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | Yes | 0 |  |
| Axe Elemental Discharge II 2 | 58 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | Yes | 0 |  |
| Axe Amped Elemental Discharge | 68 | 0 | 0 | 0 | 1 | 1 | 1 | No | Yes | 0 |  |
| Axe Amped Elemental Discharge Followup | 78 | 0 | 0 | 0 | 1 | 1 | 1 | No | Yes | 0 |  |
| Axe Super Amped Elemental Discharge 1 | 16 | 0 | 0 | 0 | 1 | 1 | 0.5 | No | Yes | 0 |  |
| Axe Super Amped Elemental Discharge 2 | 78 | 0 | 0 | 0 | 1 | 1 | 0.5 | No | Yes | 0 |  |
| Axe Super Amped Elemental Discharge 3 | 45 | 0 | 0 | 0 | 1 | 1 | 1 | No | Yes | 0 |  |
| Savage Axe | 58 | 0 | 0 | 0 | 1 | 1 | 1 | No | Yes | 0 | Activates Power Axe |
| Power Axe Ticks | 12 | 0 | 0 | 0 | 0.2 | 0.3 | 0.1 | No | Yes | 0 | Up to 4 per swing |
| Phial Burst (GP) | 5 | 10 | 3 | 0 | 0 | 0 | Yellow | No | No | 0 |  |
| Phial Burst (Sword) | 5 | 15 | 3 | 0 | 0 | 0 | Yellow | No | No | 0 |  |
| Phial Burst ED | 12 | 24 | 5 | 0 | 0 | 0 | Yellow | No | No | 0 |  |
| Phial Burst ED Overcharged Phial | 14 | 26 | 8 | 0 | 0 | 0 | Yellow | No | No | 0 |  |
| Phial Burst AED | 16 | 30 | 7 | 0 | 0 | 0 | Yellow | No | No | 0 |  |
| Phial Burst AED Overcharged Phial | 20 | 32 | 10 | 0 | 0 | 0 | Yellow | No | No | 0 |  |
| Phial Burst SAED | 28 | 35 | 10 | 0 | 0 | 0 | Yellow | No | No | 0 |  |
| Phial Burst SAED Overcharged Phial | 30 | 37 | 13 | 0 | 0 | 0 | Yellow | No | No | 0 |  |
| Phial Burst Power SAED | 38 | 40 | 10 | 0 | 0 | 0 | Yellow | No | No | 0 | When performed after AED or AED Followup |
| Phial Burst Power SAED Overcharged Phial | 45 | 45 | 13 | 0 | 0 | 0 | Yellow | No | No | 0 | When performed after AED or AED Followup |
| Phial Burst (GP) | 0 | 0 | 0 | 0 | 1.5 | 0 | Yellow | No | No | 0 |  |
| Phial Burst (Sword) | 0 | 0 | 0 | 0 | 1.5 | 0 | Yellow | No | No | 0 |  |
| Phial Burst ED | 0 | 0 | 0 | 0 | 2.1 | 0 | Yellow | No | No | 0 |  |
| Phial Burst ED Overcharged Phial | 0 | 0 | 0 | 0 | 2.4 | 0 | Yellow | No | No | 0 |  |
| Phial Burst AED | 0 | 0 | 0 | 0 | 4 | 0 | Yellow | No | No | 0 |  |
| Phial Burst AED Overcharged Phial | 0 | 0 | 0 | 0 | 5 | 0 | Yellow | No | No | 0 |  |
| Phial Burst SAED | 0 | 0 | 0 | 0 | 8.2 | 0 | Yellow | No | No | 0 |  |
| Phial Burst SAED Overcharged Phial | 0 | 0 | 0 | 0 | 11 | 0 | Yellow | No | No | 0 |  |
| Phial Burst Power SAED | 0 | 0 | 0 | 0 | 10 | 0 | Yellow | No | No | 0 | When performed after AED or AED Followup |
| Phial Burst Power SAED Overcharged Phial | 0 | 0 | 0 | 0 | 14 | 0 | Yellow | No | No | 0 | When performed after AED or AED Followup |
| Sword Jumping Slash/Axe Jumping Morph Slash | 22 | 0 | 0 | 55 | 1 | 1 | 1 | No | Yes | 0 |  |
| Axe Jumping Slash/Sword Jumping Morph Slash | 46 | 0 | 0 | 55 | 1 | 1 | 1 | No | Yes | 0 |  |
| Sword Sliding Slash 1 | 12 | 0 | 0 | 0 | 1 | 1 | 0.5 | No | Yes | 3 |  |
| Sword Sliding Slash 2 | 30 | 0 | 0 | 0 | 1 | 1 | 0.5 | No | Yes | 2 |  |
| Sword Jumping Rising Slash | 25 | 0 | 0 | 45 | 1 | 1 | 1 | No | Yes | 3 |  |
| Sword Scaling Chop 1/Axe Scaling Roundslash 1 | 20 | 0 | 0 | 20 | 1 | 1 | 0.5 | No | Yes | 0 |  |
| Sword Scaling Chop 2/Axe Scaling Roundslash 2 | 40 | 0 | 0 | 40 | 1 | 1 | 0.5 | No | Yes | 0 |  |
| Seikret Attack I 1 | 12 | 0 | 0 | 0 | 1 | 1 | 0.5 | No | Yes | 5 |  |
| Seikret Attack I 2 | 18 | 0 | 0 | 0 | 1 | 1 | 0.5 | No | Yes | 8 |  |
| Seikret Attack II | 24 | 0 | 0 | 0 | 1 | 1 | 1 | No | Yes | 9 |  |
| Dismount Attack 1 | 30 | 0 | 0 | 55 | 1 | 1 | 1 | No | Yes | 9 |  |
| Dismount Attack 2 | 12 | 0 | 0 | 0 | 1 | 1 | 0.5 | No | Yes | 0 |  |
| Dismount Attack 3 | 6 | 0 | 0 | 0 | 1 | 1 | 0.5 | No | Yes | 0 |  |
| Double Rend 1 | 15 | 0 | 0 | 0 | 1 | 1 | 0.5 | No | Yes | 3 |  |
| Double Rend 2 | 15 | 0 | 0 | 0 | 1 | 1 | 0.5 | No | Yes | 3 |  |
| Double Rend Extra Hit | 15 | 0 | 0 | 0 | 1 | 1 | 1 | Yes | Yes | 0 |  |
| Focus Strike Savage Axe | 50 | 0 | 0 | 0 | 1 | 1 | 1 | No | Yes | 0 | Activates Power Axe |
| Focus Strike Savage Axe Ticks | 6 | 0 | 0 | 0 | 0.3 | 0.3 | 0.1 | No | Yes | 0 |  |
| Clash Stabs | 15 | 0 | 0 | 0 | 1 | 1 | 0 | No | Yes | 3 | Up to 3. Deals Hitzone-ignoring damage. For some reason. |
| Clash Shield Bash | 15 | 0 | 0 | 0 | 1 | 1 | 0 | No | Yes | 1 | Triggers a Phial Burst in Red Shield. Deals Hitzone-ignoring damage. For some reason. |
| Clash Finisher | 40 | 0 | 0 | 0 | 1 | 1 | 1 | No | Yes | 3 |  |
| Mount Attack 1 | 14 | 0 | 0 | 0 | 1 | 1 | 0.1 | No | Yes | 3 |  |
| Mount Attack 2 | 16 | 0 | 0 | 0 | 1 | 1 | 0.1 | No | Yes | 3 |  |
| Mount Finisher 1 | 20 | 0 | 0 | 0 | 1 | 1 | 0.1 | No | Yes | 0 |  |
| Mount Finisher 2 | 15 | 0 | 0 | 0 | 1 | 1 | 0.1 | No | Yes | 0 |  |
| Mount Finisher 3 | 40 | 0 | 0 | 0 | 1 | 1 | 0.1 | No | Yes | 0 | Activates Power Axe |
| Mount Finisher Ticks | 7 | 0 | 0 | 0 | 0.3 | 0.3 | 0.1 | No | Yes | 0 |  |
| Mount Finisher 4 | 70 | 0 | 0 | 0 | 1 | 1 | 0.1 | No | Yes | 0 |  |
| Sneak Attack 1 | 50 | 0 | 0 | 0 | 1 | 1 | 0.5 | No | Yes | 18 | Deals hitzone-ignoring damage |
| Sneak Attack 2 | 56 | 0 | 0 | 0 | 1 | 1 | 0.5 | No | Yes | 12 | Deals hitzone-ignoring damage |
| Aerial Sneak Attack | 106 | 0 | 0 | 0 | 1 | 1 | 0.5 | No | Yes | 12 | Deals hitzone-ignoring damage |
| Underwater Attack 1 | 36 | 0 | 0 | 0 | 0.8 | 0.8 | 1 | No | Yes | 0 |  |
| Underwater Attack 2 | 10 | 0 | 0 | 0 | 0.25 | 0.25 | 1 | No | Yes | 0 | Multihit |
| Underwater Attack 3 | 68 | 0 | 0 | 0 | 0.8 | 0.8 | 1 | No | Yes | 0 |  |

### Insect Glaive (gid 1319781659)

| Attack | Motion Value | Stun | Exhaust | Mount | Element | Status | Sharpness | Can Bounce | Notes |
|---|---|---|---|---|---|---|---|---|---|
| Rising Slash Combo 1 | 20 | 0 | 0 | 0 | 0.7 | 0.7 | 0.5 | Yes |  |
| Rising Slash Combo 2 | 14 | 0 | 0 | 0 | 0.8 | 0.8 | 0.5 | Yes |  |
| Reaping Slash | 18 | 0 | 0 | 0 | 0.8 | 0.8 | 1 | Yes |  |
| Double Slash 1 | 18 | 0 | 0 | 0 | 0.7 | 0.7 | 0.5 | Yes |  |
| Double Slash 2 | 24 | 0 | 0 | 0 | 0.8 | 0.8 | 0.5 | Yes |  |
| Sidestep Slash | 24 | 0 | 0 | 0 | 0.8 | 0.8 | 1 | Yes |  |
| Sidestep Slash Combo 1 | 14 | 0 | 0 | 0 | 0.8 | 0.8 | 0.5 | Yes |  |
| Sidestep Slash Combo 2 | 28 | 0 | 0 | 0 | 0.8 | 0.8 | 0.5 | Yes |  |
| Wide Sweep | 22 | 0 | 0 | 0 | 0.8 | 0.8 | 1 | Yes |  |
| Leaping Slash | 28 | 0 | 0 | 0 | 0.8 | 0.8 | 1 | Yes |  |
| Overhead Smash | 26 | 0 | 0 | 0 | 0.8 | 0.8 | 1 | Yes |  |
| Dodge Slash | 20 | 0 | 0 | 0 | 0.8 | 0.8 | 1 | Yes |  |
| Strong Rising Slash Combo 1 | 24 | 0 | 0 | 0 | 0.8 | 0.8 | 0.3 | Yes |  |
| Strong Rising Slash Combo 2 | 12 | 0 | 0 | 0 | 0.8 | 0.8 | 0.3 | Yes |  |
| Strong Rising Slash Combo 3 | 24 | 0 | 0 | 0 | 0.8 | 0.8 | 0.4 | Yes |  |
| Strong Rising Slash Extra Hit | 14 | 0 | 0 | 0 | 0.8 | 0.8 | 1 | Yes | Before Strong Rising Slash Combo 1 when performed from Strong Wide Slash |
| Strong Reaping Slash 1 | 18 | 0 | 0 | 0 | 0.8 | 0.8 | 0.5 | Yes |  |
| Strong Reaping Slash 2 | 28 | 0 | 0 | 0 | 0.8 | 0.8 | 0.5 | Yes |  |
| Strong Double Slash 1 | 26 | 0 | 0 | 0 | 0.8 | 0.8 | 0.3 | Yes |  |
| Strong Double Slash 2 | 18 | 0 | 0 | 0 | 0.8 | 0.8 | 0.3 | Yes |  |
| Strong Double Slash 3 | 40 | 0 | 0 | 0 | 1 | 0.8 | 0.4 | Yes |  |
| "Strong" Sidestep Slash | 26 | 0 | 0 | 0 | 0.8 | 0.8 | 1 | Yes | During Triple Up |
| "Strong" Sidestep Slash Combo 1 | 18 | 0 | 0 | 0 | 0.8 | 0.8 | 0.5 | Yes | During Triple Up |
| "Strong" Sidestep Slash Combo 2 | 30 | 0 | 0 | 0 | 0.8 | 0.8 | 0.5 | Yes | During Triple Up |
| Strong Wide Sweep 1 | 12 | 0 | 0 | 0 | 0.8 | 0.8 | 0.5 | Yes |  |
| Strong Wide Sweep 2 | 24 | 0 | 0 | 0 | 0.8 | 0.8 | 0.5 | Yes |  |
| Tornado Slash 1 | 22 | 0 | 0 | 0 | 1 | 0.8 | 0.5 | No |  |
| Tornado Slash 2 | 48 | 0 | 0 | 0 | 1 | 0.8 | 0.5 | No |  |
| Kinsect Mark (melee) | 5 | 5 | 5 | 0 | 0 | 0 | 0 | No | Marks the monster. Deals Blunt damage |
| Kinsect Mark (shot) | 0 | 0 | 0 | 0 | 0 | 0 | 0 | No |  |
| Jumping Slash | 18 | 0 | 0 | 30 | 1 | 1 | 1 | No | Gets a 1.1 damage multiplier after 1 Vaulting Dance and a 1.3 damage multiplier after 2 |
| Jumping Advancing Slash | 26 | 0 | 0 | 35 | 0.8 | 0.8 | 0.3 | No | Gets a 1.1 damage multiplier after 1 Vaulting Dance and a 1.3 damage multiplier after 2.Triggers an automatic Vaulting Dance on hit |
| Strong Jumping Slash | 14 | 0 | 0 | 20 | 0.6 | 0.6 | 1 | No | Multihit. Gets a 1.1 damage multiplier after 1 Vaulting Dance and a 1.3 damage multiplier after 2 |
| Strong Jumping Advancing Slash 1 | 9 | 0 | 0 | 15 | 0.5 | 0.5 | 0.1 | No | Gets a 1.1 damage multiplier after 1 Vaulting Dance and a 1.3 damage multiplier after 2 |
| Strong Jumping Advancing Slash 2 | 9 | 0 | 0 | 15 | 0.5 | 0.5 | 0.1 | No | Gets a 1.1 damage multiplier after 1 Vaulting Dance and a 1.3 damage multiplier after 2 |
| Strong Jumping Advancing Slash 3 | 9 | 0 | 0 | 15 | 0.5 | 0.5 | 0.1 | No | Gets a 1.1 damage multiplier after 1 Vaulting Dance and a 1.3 damage multiplier after 2 |
| Strong Jumping Advancing Slash 4 | 9 | 0 | 0 | 15 | 0.5 | 0.5 | 0.1 | No | Gets a 1.1 damage multiplier after 1 Vaulting Dance and a 1.3 damage multiplier after 2 |
| Strong Jumping Advancing Slash 5 | 32 | 0 | 0 | 10 | 0.8 | 0.8 | 0.5 | No | Gets a 1.1 damage multiplier after 1 Vaulting Dance and a 1.3 damage multiplier after 2. Triggers an automatic Vaulting Dance on hit |
| Scaling Double Slash 1 | 12 | 0 | 0 | 20 | 0.8 | 0.8 | 0.5 | No |  |
| Scaling Double Slash 2 | 20 | 0 | 0 | 20 | 0.8 | 0.8 | 0.5 | No |  |
| Descending Slash 1 Lv0 | 12 | 0 | 0 | 0 | 1 | 1 | 0.2 | No | 60 Parry. When perfored from idle. Actually a 0.6x modifier on the Lv2 version. |
| Descending Slash 2 Lv0 | 7.2 | 0 | 0 | 0 | 1 | 1 | 0.2 | No | When perfored from idle. Actually a 0.6x modifier on the Lv2 version |
| Descending Slash 3 Lv0 | 9.6 | 0 | 0 | 0 | 1 | 1 | 0.2 | No | When perfored from idle. Actually a 0.6x modifier on the Lv2 version |
| Descending Slash 4 Lv0 | 19.2 | 0 | 0 | 0 | 1 | 1 | 0.4 | No | When perfored from idle. Actually a 0.6x modifier on the Lv2 version |
| Descending Slash 1 Lv1 | 15 | 0 | 0 | 0 | 1 | 1 | 0.2 | No | 60 Parry. When performed with a half charge or after attacks specified below. Actually a 0.75x modifier on the Lv2 version |
| Descending Slash 2 Lv1 | 9 | 0 | 0 | 0 | 1 | 1 | 0.2 | No | When performed with a half charge or after attacks specified below. Actually a 0.75x modifier on the Lv2 version |
| Descending Slash 3 Lv1 | 12 | 0 | 0 | 0 | 1 | 1 | 0.2 | No | When performed with a half charge or after attacks specified below. Actually a 0.75x modifier on the Lv2 version |
| Descending Slash 4 Lv1 | 24 | 0 | 0 | 0 | 1 | 1 | 0.4 | No | When performed with a half charge or after attacks specified below. Actually a 0.75x modifier on the Lv2 version |
| Descending Slash 1 Lv2 | 20 | 0 | 0 | 0 | 1 | 1 | 0.2 | No | 60 Parry. When performed with a full charge or after attacks specified below. |
| Descending Slash 2 Lv2 | 12 | 0 | 0 | 0 | 1 | 1 | 0.2 | No | When performed with a full charge or after attacks specified below. |
| Descending Slash 3 Lv2 | 16 | 0 | 0 | 0 | 1 | 1 | 0.2 | No | When performed with a full charge or after attacks specified below. |
| Descending Slash 4 Lv2 | 32 | 0 | 0 | 0 | 1 | 1 | 0.4 | No | When performed with a full charge or after attacks specified below. |
| Strong Descending Slash 1  Lv0 | 19.2 | 0 | 0 | 0 | 1 | 1 | 0.2 | No | 90 Parry. When performed from idle. Actually a 0.6x modifier on the Lv2 version. |
| Strong Descending Slash 2  Lv0 | 7.2 | 0 | 0 | 0 | 1 | 1 | 0.2 | No | When perfored from idle. Actually a 0.6x modifier on the Lv2 version |
| Strong Descending Slash 3  Lv0 | 10.8 | 0 | 0 | 0 | 1 | 1 | 0.1 | No | When perfored from idle. Actually a 0.6x modifier on the Lv2 version |
| Strong Descending Slash 4  Lv0 | 10.8 | 0 | 0 | 0 | 1 | 1 | 0.1 | No | When perfored from idle. Actually a 0.6x modifier on the Lv2 version |
| Strong Descending Slash 5  Lv0 | 10.8 | 0 | 0 | 0 | 1 | 1 | 0.1 | No | When perfored from idle. Actually a 0.6x modifier on the Lv2 version |
| Strong Descending Slash 6  Lv0 | 27.6 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | When perfored from idle. Actually a 0.6x modifier on the Lv2 version |
| Strong Descending Slash 1  Lv1 | 24 | 0 | 0 | 0 | 1 | 1 | 0.2 | No | 60 Parry. When performed with a half charge or after attacks specified below. Actually a 0.75x modifier on the Lv2 version |
| Strong Descending Slash 2  Lv1 | 9 | 0 | 0 | 0 | 1 | 1 | 0.2 | No | When performed with a half charge or after attacks specified below. Actually a 0.75x modifier on the Lv2 version |
| Strong Descending Slash 3  Lv1 | 13.5 | 0 | 0 | 0 | 1 | 1 | 0.1 | No | When performed with a half charge or after attacks specified below. Actually a 0.75x modifier on the Lv2 version |
| Strong Descending Slash 4  Lv1 | 13.5 | 0 | 0 | 0 | 1 | 1 | 0.1 | No | When performed with a half charge or after attacks specified below. Actually a 0.75x modifier on the Lv2 version |
| Strong Descending Slash 5  Lv1 | 13.5 | 0 | 0 | 0 | 1 | 1 | 0.1 | No | When performed with a half charge or after attacks specified below. Actually a 0.75x modifier on the Lv2 version |
| Strong Descending Slash 6  Lv1 | 34.5 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | When performed with a half charge or after attacks specified below. Actually a 0.75x modifier on the Lv2 version |
| Strong Descending Slash 1  Lv2 | 32 | 0 | 0 | 0 | 1 | 1 | 0.2 | No | 90 Parry. When performed with a full charge or after attacks specified below. |
| Strong Descending Slash 2  Lv2 | 12 | 0 | 0 | 0 | 1 | 1 | 0.2 | No | When performed with a full charge or after attacks specified below. |
| Strong Descending Slash 3  Lv2 | 18 | 0 | 0 | 0 | 1 | 1 | 0.1 | No | When performed with a full charge or after attacks specified below. |
| Strong Descending Slash 4  Lv2 | 18 | 0 | 0 | 0 | 1 | 1 | 0.1 | No | When performed with a full charge or after attacks specified below. |
| Strong Descending Slash 5  Lv2 | 18 | 0 | 0 | 0 | 1 | 1 | 0.1 | No | When performed with a full charge or after attacks specified below. |
| Strong Descending Slash 6  Lv2 | 46 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | When performed with a full charge or after attacks specified below. |
| Descending Thrust 1 Lv1 | 21 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | 60 Parry. When performed with half charge or with no charge before a Vaulting Dance. Actually a 0.75x modifier on the Lv2 version. Gets a 1.2 damage multiplier after 1 Vaulting Dance and a 1.5 damage multiplier after 2 |
| Descending Thrust 2 Lv1 | 7.5 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | When performed with half charge or with no charge before a Vaulting Dance. Actually a 0.75x modifier on the Lv2 version. Gets a 1.2 damage multiplier after 1 Vaulting Dance and a 1.5 damage multiplier after 2 |
| Descending Thrust 3 Lv1 | 3.75 | 5 | 5 | 0 | 0 | 0 | 0 | No | When performed with half charge or with no charge before a Vaulting Dance. Actually a 0.75x modifier on the Lv2 version. Marks the monster. Deals Blunt damage. Gets a 1.2 damage multiplier after 1 Vaulting Dance and a 1.5 damage multiplier after 2 |
| Descending Thrust 4 Lv1 | 18 | 0 | 0 | 0 | 1 | 1 | 0.4 | No | When performed with half charge or with no charge before a Vaulting Dance. Actually a 0.75x modifier on the Lv2 version. Gets a 1.2 damage multiplier after 1 Vaulting Dance and a 1.5 damage multiplier after 2 |
| Descending Thrust 1 Lv2 | 28 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | When performed with a full charge or after at least one Vaulting Dance. 60 Parry. Gets a 1.2 damage multiplier after 1 Vaulting Dance and a 1.5 damage multiplier after 2 |
| Descending Thrust 2 Lv2 | 10 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | When performed with a full charge or after at least one Vaulting Dance. Gets a 1.2 damage multiplier after 1 Vaulting Dance and a 1.5 damage multiplier after 2 |
| Descending Thrust 3 Lv2 | 5 | 5 | 5 | 0 | 0 | 0 | 0 | No | When performed with a full charge or after at least one Vaulting Dance. Marks the monster. Deals Blunt damage. Gets a 1.2 damage multiplier after 1 Vaulting Dance and a 1.5 damage multiplier after 2 |
| Descending Thrust 4 Lv2 | 24 | 0 | 0 | 0 | 1 | 1 | 0.4 | No | When performed with a full charge or after at least one Vaulting Dance. Gets a 1.2 damage multiplier after 1 Vaulting Dance and a 1.5 damage multiplier after 2 |
| Strong Descending Thrust 1 Lv1 | 21 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | 90 Parry. When performed with half charge or with no charge before a Vaulting Dance. Actually a 0.75x modifier on the Lv2 version. Gets a 1.2 damage multiplier after 1 Vaulting Dance and a 1.5 damage multiplier after 2 |
| Strong Descending Thrust 2 Lv1 | 7.5 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | When performed with half charge or with no charge before a Vaulting Dance. Actually a 0.75x modifier on the Lv2 version. Gets a 1.2 damage multiplier after 1 Vaulting Dance and a 1.5 damage multiplier after 2 |
| Strong Descending Thrust 3 Lv1 | 3.75 | 5 | 5 | 0 | 0 | 0 | 0 | No | When performed with half charge or with no charge before a Vaulting Dance. Actually a 0.75x modifier on the Lv2 version. Marks the monster. Deals Blunt damage. Gets a 1.2 damage multiplier after 1 Vaulting Dance and a 1.5 damage multiplier after 2 |
| Strong Descending Thrust 4 Lv1 | 13 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | When performed with half charge or with no charge before a Vaulting Dance. Actually a 0.75x modifier on the Lv2 version. Gets a 1.2 damage multiplier after 1 Vaulting Dance and a 1.5 damage multiplier after 2 |
| Strong Descending Thrust 5 Lv1 | 13 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | When performed with half charge or with no charge before a Vaulting Dance. Actually a 0.75x modifier on the Lv2 version. Gets a 1.2 damage multiplier after 1 Vaulting Dance and a 1.5 damage multiplier after 2 |
| Strong Descending Thrust 6 Lv1 | 21 | 0 | 0 | 0 | 1 | 1 | 0.4 | No | When performed with half charge or with no charge before a Vaulting Dance. Actually a 0.75x modifier on the Lv2 version. Gets a 1.2 damage multiplier after 1 Vaulting Dance and a 1.5 damage multiplier after 2 |
| Strong Descending Thrust 1 Lv2 | 28 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | 90 Parry. When performed with a full charge or after at least one Vaulting Dance. Gets a 1.2 damage multiplier after 1 Vaulting Dance and a 1.5 damage multiplier after 2 |
| Strong Descending Thrust 2 Lv2 | 10 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | When performed with a full charge or after at least one Vaulting Dance. Gets a 1.2 damage multiplier after 1 Vaulting Dance and a 1.5 damage multiplier after 2 |
| Strong Descending Thrust 3 Lv2 | 5 | 5 | 5 | 0 | 0 | 0 | 0 | No | When performed with a full charge or after at least one Vaulting Dance. Marks the monster. Deals Blunt damage. Gets a 1.2 damage multiplier after 1 Vaulting Dance and a 1.5 damage multiplier after 2 |
| Strong Descending Thrust 4 Lv2 | 18 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | When performed with a full charge or after at least one Vaulting Dance. Gets a 1.2 damage multiplier after 1 Vaulting Dance and a 1.5 damage multiplier after 2 |
| Strong Descending Thrust 5 Lv2 | 18 | 0 | 0 | 0 | 1 | 1 | 0.3 | No | When performed with a full charge or after at least one Vaulting Dance. Gets a 1.2 damage multiplier after 1 Vaulting Dance and a 1.5 damage multiplier after 2 |
| Strong Descending Thrust 6 Lv2 | 28 | 0 | 0 | 0 | 1 | 1 | 0.4 | No | When performed with a full charge or after at least one Vaulting Dance. Gets a 1.2 damage multiplier after 1 Vaulting Dance and a 1.5 damage multiplier after 2 |
| Rising Spiral Slash LV1 | 48 | 0 | 0 | 0 | 0.8 | 0.8 | 0.2 | No | Hits 8 times. Inherits the level of the previous Strong Descending Slash/Thrust. If Lv0, becomes Lv1. Actually a 0.75x modifier on the Lv2 version. |
| Rising Spiral Slash Lv2 | 64 | 0 | 0 | 0 | 0.8 | 0.8 | 0.2 | No | Hits 8 times. Inherits the level of the previous Strong Descending Slash/Thrust. |
| Seikret Attack I | 12 | 0 | 0 | 0 | 1 | 1 | 1 | No |  |
| Seikret Attack II | 16 | 0 | 0 | 0 | 1 | 1 | 1 | No |  |
| Seikret Hold Attack 1 | 5 | 0 | 0 | 0 | 1 | 1 | 0.2 | No |  |
| Seikret Hold Attack 2 | 5 | 5 | 5 | 0 | 1 | 1 | 0 | No | Deals Blunt Damage |
| Seikret Hold Attack 3 | 5 | 0 | 0 | 0 | 1 | 1 | 0.2 | No |  |
| Seikret Hold Attack 4 | 10 | 0 | 0 | 0 | 1 | 1 | 1 | No |  |
| Dismount Attack 1 | 12 | 0 | 0 | 35 | 1 | 1 | 0.5 | No |  |
| Dismount Attack 2 | 8 | 5 | 5 | 30 | 1 | 1 | 0.5 | No | Deals Blunt Damage |
| Dismount Attack 3 | 24 | 0 | 0 | 0 | 1 | 1 | 1 | No |  |
| Leaping Thrust 1 | 16 | 0 | 0 | 0 | 1 | 1 | 1 | No |  |
| Leaping Thrust 2 | 18 | 0 | 0 | 0 | 0.8 | 0.8 | 1 | No |  |
| Leaping Thrust Extra Hit | 18 | 0 | 0 | 0 | 0.8 | 0.8 | 1 | No |  |
| Leaping Thrust 3 | 8 | 5 | 5 | 0 | 0.8 | 0.8 | 1 | No | Deals Blunt Damage |
| Mount Attack 1 | 25 | 0 | 0 | 0 | 1 | 1 | 0.1 | No |  |
| Mount Attack 2 | 20 | 0 | 0 | 0 | 1 | 1 | 0.1 | No |  |
| Mount Finisher 1 | 40 | 0 | 0 | 0 | 1 | 1 | 0.1 | No |  |
| Mount Finisher 2 | 6 | 0 | 0 | 0 | 1 | 1 | 0.1 | No |  |
| Mount Finisher 3 | 8 | 0 | 0 | 0 | 1 | 1 | 0.1 | No |  |
| Mount Finisher 4 | 6 | 0 | 0 | 0 | 1 | 1 | 0.1 | No |  |
| Mount Finisher 5 | 8 | 0 | 0 | 0 | 1 | 1 | 0.1 | No |  |
| Mount Finisher 6 | 6 | 0 | 0 | 0 | 1 | 1 | 0.1 | No |  |
| Mount Finisher 7 | 8 | 0 | 0 | 0 | 1 | 1 | 0.1 | No |  |
| Mount Finisher 8 | 6 | 0 | 0 | 0 | 1 | 1 | 0.1 | No |  |
| Mount Finisher 9 | 8 | 0 | 0 | 0 | 1 | 1 | 0.1 | No |  |
| Mount Finisher 10 | 50 | 0 | 0 | 0 | 1 | 1 | 0.1 | No |  |
| Sneak Attack 1 | 50 | 0 | 0 | 0 | 1 | 1 | 0.5 | No | Deals hitzone-ignoring damage |
| Sneak Attack 2 | 56 | 0 | 0 | 0 | 1 | 1 | 0.5 | No | Deals hitzone-ignoring damage |
| Aerial Sneak Attack | 106 | 0 | 0 | 0 | 1 | 1 | 1 | No | Deals hitzone-ignoring damage |
| Underwater Attack 1 | 44 | 0 | 0 | 0 | 0.7 | 0.7 | 1 | No |  |
| Underwater Attack 2 | 42 | 0 | 0 | 0 | 0.7 | 0.7 | 1 | No |  |
| Underwater Attack 3 | 57 | 0 | 0 | 0 | 0.7 | 0.7 | 1 | No |  |
| Kinsect Attack | 20 | 18 | 10 | 0 | 1 | - | - | - | Damage type depends on Kinsect. Stun and Exhaust damage is only applied by Blunt Kinsects |
| Kinsect Joint Attack (Base) | 30 | 18 | 8 | 0 | 1 | - | - | - | Damage type depends on Kinsect. Stun and Exhaust damage is only applied by Blunt Kinsects |
| Kinsect Joint Attack (Double Slash) | 15 | 12 | 5 | 0 | 1 | - | - | - | Multihit. Damage type depends on Kinsect. Stun and Exhaust damage is only applied by Blunt Kinsects |
| Kinsect Joint Attack (Triple Up) | 20 | 10 | 5 | 0 | 1 | - | - | - | Multihit. Damage type depends on Kinsect. Stun and Exhaust damage is only applied by Blunt Kinsects |
| Kinsect Joint Attack (Descending Thrust) | 12 | 10 | 5 | 0 | 1 | - | - | - | Multihit. Damage type depends on Kinsect. Stun and Exhaust damage is only applied by Blunt Kinsects |
| Kinsect Joint Attack (Rising Spiral Slash) | 24 | 15 | 8 | 0 | 1 | - | - | - | Multihit. Damage type depends on Kinsect. Stun and Exhaust damage is only applied by Blunt Kinsects |
| Kinsect Focus Strike | 30 | 0 | 0 | 0 | 1 | - | - | - | Multihit. Grabs all three extracts. Damage type depends on Kinsect. Stun and Exhaust damage is only applied by Blunt Kinsects |
| Blast Powder Explosion | 10 | 0 | 0 | 0 | 1 | Blast | - | - | Deals Hitzone-ignoring damage |
| Heal Powder Explosion | 0 | 0 | 0 | 0 | 1 | Heal | - | - |  |
| Paralysis Powder Explosion | 3 | 0 | 0 | 0 | 1 | Paralyisis | - | - | Deals Hitzone-ignoring damage |
| Poison Powder Explosion | 5 | 0 | 0 | 0 | 1 | Poison | - | - | Deals Hitzone-ignoring damage |

Descending Slash Lv1 is triggered after Rising Slash Combo, Sidestep Slash, Wide Sweep, Leaping Slash, Overhead Smash, Dodge Slash and Jumping Slash.

Descending Slash Lv2 is triggered after Reaping Slash, Sidestep Slash Combo and Double Slash.

Strong Descending Lv1 Slash is triggered after Strong Rising Slash Combo, Sidestep Slash, Strong Wide Sweep, Leaping Slash, Dodge Slash and Strong Jumping Slash.

Strong Descending Lv2 Slash is triggered after Strong Reaping Slash, Sidestep Slash Combo, Strong Double Slash and Tornado Slash.
