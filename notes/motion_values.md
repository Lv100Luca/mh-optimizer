# Motion values (Great Sword, Long Sword, Switch Axe)

Datamine for game version 1.0.11, from the community sheet linked in the GameFAQs thread "MH Wilds Motion Values (1.0.11 update)":
https://docs.google.com/spreadsheets/d/1Bine6S9MD6hjM85keFNyj9WbaeVb1ZVHYnhs93U3pVM (fetched 2026-10-07).

* **Motion Value** multiplies true raw as a percentage. **Element** / **Status** multiply true element / status for that attack (Wilds folds the old charge-level multipliers into these columns).
* **Sharpness** is sharpness consumed per hit; a color means the attack ignores sharpness and acts as that color.
* Numbers 1 / 2 in a name are hits within one input; "Power" is the damage under the attack's special condition.
* **Update (2026-10-07):** the author published a sheet for game version 1.040 (datamine 1.02.0, no balance changes in 1.030):
  https://docs.google.com/spreadsheets/d/1F_bOwQT6A6mFtCN7WCetnZDrmvLYu-ne4y97IsG6Fkk. The attack picker (`src/MHWildsOptimizer.Core/Damage/Attacks.cs`)
  uses its numbers; the Great Sword and Long Sword tables below are still the 1.0.11 ones. Great Sword changes in 1.040 that the attacks use:
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

## Long Sword

| Attack | Motion Value | Element | Status | Sharpness | Part Break | Notes |
|---|---|---|---|---|---|---|
| Overhead Slash I | 24 | 1 | 1 | 1 | 1 |  |
| Overhead Slash II | 18 | 1 | 1 | 1 | 1 |  |
| Crescent Slash | 32 | 1 | 1 | 1 | 1 |  |
| Crimson Slash I 1 | 33 | 0.7 | 0.7 | 0.5 | 1 |  |
| Crimson Slash I 2 | 16 | 0.7 | 0.7 | 0.5 | 1 |  |
| Crimson Slash II 1 | 9 | 0.7 | 0.7 | 0.5 | 1 |  |
| Crimson Slash II 2 | 18 | 0.7 | 0.7 | 0.5 | 1 |  |
| Crimson Slash III 1 | 28 | 0.7 | 0.7 | 0.3 | 1 |  |
| Crimson Slash III 2 | 16 | 0.7 | 0.7 | 0.3 | 1 |  |
| Crimson Slash III 3 | 49 | 0.7 | 0.7 | 0.3 | 1 |  |
| Thrust | 18 | 1 | 1 | 1 | 1 |  |
| Rising Slash | 18 | 1 | 1 | 1 | 1 |  |
| Fade Slash | 22 | 1 | 1 | 1 | 1 |  |
| Spirit Blade I | 31 | 1 | 1 | 1 | 1 |  |
| Spirit Blade I (No Gauge) | 14 | 1 | 1 | 1 | 1 |  |
| Directional Spirit Blade I 1 | 31 | 1 | 1 | 1 | 1 |  |
| Directional Spirit Blade I 1 (No Gauge) | 14 | 1 | 1 | 1 | 1 |  |
| Spirit Blade II | 30 | 1 | 1 | 1 | 1 |  |
| Spirit Blade II (No Gauge) | 14 | 1 | 1 | 1 | 1 |  |
| Directional Spirit Blade II 1 | 14 | 1 | 1 | 0.5 | 1 |  |
| Directional Spirit Blade II 2 | 26 | 1 | 1 | 0.5 | 1 |  |
| Directional Spirit Blade II 1 (No Gauge) | 8 | 1 | 1 | 0.5 | 1 |  |
| Directional Spirit Blade II 2 (No Gauge) | 18 | 1 | 1 | 0.5 | 1 |  |
| Spirit Blade III 1 | 14 | 1 | 1 | 0.3 | 1 |  |
| Spirit Blade III 2 | 19 | 1 | 1 | 0.3 | 1 |  |
| Spirit Blade III 3 | 34 | 1 | 1 | 0.3 | 1 |  |
| Spirit Blade III 1 (No Gauge) | 8 | 1 | 1 | 0.3 | 1 |  |
| Spirit Blade III 2 (No Gauge) | 11 | 1 | 1 | 0.3 | 1 |  |
| Spirit Blade III 3 (No Gauge) | 19 | 1 | 1 | 0.3 | 1 |  |
| Spirit Roundslash | 38 | 1 | 1 | 1 | 1 | Raises Spirit Gauge level by 1. In Red, Gauge generation depends on how many times it has been used during this Red Spirit Gauge period |
| Spirit Roundslash (No Gauge) | 19 | 1 | 1 | 1 | 1 | Raises Spirit Gauge level by 1. In Red, Gauge generation depends on how many times it has been used during this Red Spirit Gauge period |
| Spinning Crimson Slash | 55 | 1 | 1 | 1 | 1 | In Red, Gauge generation depends on how many times it has been used during this Red Spirit Gauge period |
| Spirit Step Slash | 28 | 1 | 1 | 1 | 1 |  |
| Spirit Step Slash (No Gauge) | 18 | 1 | 1 | 1 | 1 |  |
| Jumping Slash | 26 | 1 | 1 | 1 | 1 |  |
| Jumping Spirit Blade I | 30 | 1 | 1 | 1 | 1 |  |
| Jumping Spirit Blade I (No Gauge) | 16 | 1 | 1 | 1 | 1 |  |
| Jumping Spirit Blade II 1 | 12 | 1 | 1 | 0.5 | 1 |  |
| Jumping Spirit Blade II 2 | 26 | 1 | 1 | 0.5 | 1 |  |
| Jumping Spirit Blade II 1 (No Gauge) | 8 | 1 | 1 | 0.5 | 1 |  |
| Jumping Spirit Blade II 2 (No Gauge) | 15 | 1 | 1 | 0.5 | 1 |  |
| Jumping Spirit Blade III 1 | 12 | 1 | 1 | 0.3 | 1 |  |
| Jumping Spirit Blade III 2 | 22 | 1 | 1 | 0.3 | 1 |  |
| Jumping Spirit Blade III 3 (Not used?) | 24 | 1 | 1 | 0.3 | 1 |  |
| Jumping Spirit Blade III 1 (No Gauge) | 7 | 1 | 1 | 0.3 | 1 | Not available |
| Jumping Spirit Blade III 2 (No Gauge) | 13 | 1 | 1 | 0.3 | 1 | Not available |
| Jumping Spirit Blade III 3 (No Gauge) | 13 | 1 | 1 | 0.3 | 1 | Used even when you have gauge? |
| Jumping Rising Slash | 20 | 1 | 1 | 1 | 1 |  |
| Aerial Draw Spirit Blade | 48 | 1 | 1 | 1 | 1 |  |
| Aerial Draw Spirit Blade (No Gauge) | 26 | 1 | 1 | 1 | 1 |  |
| Foresight Slash | 18 | 1 | 1 | 1 | 1 |  |
| Foresight Slash (No Gauge) | 11 | 1 | 1 | 1 | 1 |  |
| Foresight Slash (Success) | 23.4 | 1 | 1 | 1 | 1 | Actually just a 1.3x multiplier. |
| Foresight Whirl Slash 1 | 22 | 1 | 1 | 0.5 | 1 |  |
| Foresight Whirl Slash 2 | 13 | 1 | 1 | 0.5 | 1 |  |
| Foresight Whirl Slash 1 (Sucessful) | 28.6 | 1 | 1 | 0.5 | 1 | Actually just a 1.3x multiplier |
| Foresight Whirl Slash 2 (Successful) | 16.9 | 1 | 1 | 0.5 | 1 | Actually just a 1.3x multiplier |
| Iai Slash 1 | 18 | 1 | 1 | 0.5 | 1 |  |
| Iai Slash 2 | 13 | 1 | 1 | 0.5 | 1 |  |
| Iai Spirit Slash 1 (None) | 17 | 1 | 1 | 1 | 1.5 | Increases Spirit Gauge level by 1 on a successful counter |
| Iai Spirit Slash 1 (White) | 19 | 1 | 1 | 1 | 1.5 | Increases Spirit Gauge level by 1 on a successful counter |
| Iai Spirit Slash 2 (White) | 55 | 1 | 1 | 0 | 1.5 | Only on a successful counter |
| Iai Spirit Slash 1 (Yellow) | 31 | 1 | 1 | 1 | 1.5 | Increases Spirit Gauge level by 1 on a successful counter |
| Iai Spirit Slash 2 (Yellow) | 72 | 1 | 1 | 0 | 1.5 | Only on a successful counter |
| Iai Spirit Slash 1 (Red) | 39 | 1 | 1 | 1 | 1.5 |  |
| Iai Spirit Slash 2 (Red) | 86 | 1 | 1 | 0 | 1.5 | Only on a successful counter |
| Spirit Thrust | 19 | 1 | 1 | 1 | 1 |  |
| Spirit Helmbreaker (White) | 15 | 0.2 | 0.2 | 0 | 0.9 | Hits 7 times. Last hit has a Part Break multiplier of 1. Uses no gauge multiplier. |
| Spirit Helmbreaker (Yellow) | 20 | 0.2 | 0.2 | 0 | 0.9 | Hits 7 times. Last hit has a Part Break multiplier of 1. Uses White gauge multiplier. |
| Spirit Helmbreaker (Red) | 23 | 0.2 | 0.2 | 0 | 0.9 | Hits 7 times. Last hit has a Part Break multiplier of 1. Uses Yellow gauge multiplier. |
| Spirit Release Slash 1 (x2) | 5 | 1 | 1 | 0.3 | 1 | Uses Yellow Gauge multiplier |
| Spirit Release Slash 2 (x3) | 9 | 0.1 | 0.1 | 0 | 1.5 | Uses White Gauge multiplier |
| Spirit Release Slash 3 (x3) | 14 | 0.1 | 0.1 | 0 | 1.5 | Uses White Gauge multiplier |
| Spirit Release Slash 4 (x3) | 20 | 0.1 | 0.1 | 0 | 1.5 | Uses White Gauge multiplier |
| Spirit Release Slash 5 (x3) | 32 | 0.1 | 0.1 | 0 | 3 | Uses White Gauge multiplier |
| Seikret Attack I 1 | 15 | 1 | 1 | 0.5 | 1 |  |
| Seikret Attack I 2 | 10 | 1 | 1 | 0.5 | 1 |  |
| Seikret Attack II | 20 | 1 | 1 | 1 | 1 |  |
| Dismount Attack 1 | 30 | 1 | 1 | 1 | 1 | Increases Spirit Gauge level by 1 on a successful counter |
| Dismount Attack 2 | 15 | 1 | 1 | 1 | 1 |  |
| Focus Strike: Unbound Thrust 1 | 26 | 1 | 1 | 1 | 1 |  |
| Focus Strike: Unbound Thrust Extra Hit | 26 | 1 | 1 | 1 | 1 |  |
| Focus Strike: Unbound Thrust 2 | 10 | 1 | 1 | 1 | 1 |  |
| Focus Strike: Unbound Thrust 3 | 40 | 1 | 1 | 1 | 1 |  |
| Focus Strike: Unbound Thrust 4 | 40 | 1 | 1 | 1 | 1 |  |
| Focus Strike: Unbound Thrust 5 | 40 | 1 | 1 | 0 | 1 | Applies once per wound. Increases Spirit Gauge level by 1. Uses post-level up modifiers |
| Mount Attack 1 | 23 | 1 | 1 | 0.1 | 1 |  |
| Mount Attack 2 | 20 | 1 | 1 | 0.1 | 1 |  |
| Mount Finisher 1 | 70 | 1 | 1 | 0.1 | 1 |  |
| Mount Finisher 2 | 100 | 1 | 1 | 0.1 | 1 |  |
| Sneak Attack 1 | 50 | 1 | 1 | 0.5 | 1 | Deals hitzone-ignoring damage |
| Sneak Attack 2 | 56 | 1 | 1 | 0.5 | 1 | Deals hitzone-ignoring damage |
| Aerial Sneak Attack 1 | 50 | 1 | 1 | 0.5 | 1 | Deals hitzone-ignoring damage |
| Aerial Sneak Attack 2 | 56 | 1 | 1 | 0.5 | 1 | Deals hitzone-ignoring damage |

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
