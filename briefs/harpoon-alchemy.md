# Game Agent Brief: Harpoon Alchemy (Cable-Fusion Pang)

- **Slug:** `harpoon-alchemy`
- **Repository Fork:** [Kokanm/harpoon-alchemy](https://github.com/Kokanm/harpoon-alchemy) (`feat/harpoon-alchemy` tracking `fork/main`)
- **Date:** 2026-11-09
- **Creator Profile:** `game-trend-profile.md` (Solo dev + coding agents · Unity 2D URP `6000.6.7f1` · $0–$100/mo · Mobile + Desktop first, Web later)

---

## 1. Goal & Hypothesis

**We believe** upgrading *Bubblewright*'s 1D floor-bound *Pang* loop with a **3×3 Pairwise Cable-Fusion System** (combining structural harpoon modifiers with elemental/split payload cards to create visible, screen-clearing cable geometries) + a **1-per-run Card Reroll & Heart Relight hook** will reach:
- **D1 Retention ≥ 35%** and **D7 Retention ≥ 15%** on mobile `(Turbine Hybrid-Casual Playbook, Apr 2025; Gamigion Hybrid-Casual Overview, 2025)`
- **Daily Playtime ≥ 22 min/day** (P75 benchmark) and **Average Session ≥ 6.2 min** (372 s) `(InvestGame / GameAnalytics 2026 Mobile & PC Benchmarks, Jan 2026; Sensor Tower Hybrid-Casual Playbook, 2025)`
- **Stage 1 Clear Rate > 80%** with zero touch-control complaints.

This build exists to test that hypothesis with zero external art cost.

---

## 2. Non-Goals (Explicitly Left Out of MVP)

Do **not** build any of the following in this MVP:
1. **No town/base-building meta** (avoids *BALL x PIT*'s #1 Steam complaint of tedious building rearrangement between runs).
2. **No 50-passive stat bloat** (avoids diluted RNG pools; every upgrade in the core pool is either a Structural card, a Payload card, or a Survival/Utility card).
3. **No external PNG/SVG textures or audio clips** (100% of sprites, icons, and fusion FX are painted in code via `PixelCanvas.cs` / `PlaceholderArt.cs`, and sounds are synthesized in `Sfx.cs`).
4. **No live third-party Ad or Analytics SDK packages** (stub behind single clean C# interfaces `GameServices.Ads` and `GameServices.Analytics`).
5. **No multi-world background sets, ledges, or boss cutscenes** in MVP.
6. **No online backend or account system.**

---

## 3. Core Loop Spec (Exact Rules & Numbers)

- **Arena & Screen:** Reference resolution `480 × 270` at `15 PPU` (`32 × 14 u` arena, floor at `y = 0`, ceiling at `y = 14`, walls at `x = -16` and `+16`, ground strip `y = -4..0`).
- **Player (Wick):** Runs horizontally on `y = 0` at `9 u/s` (`0.06 s` accel, `0.04 s` stop/skid). Hitbox `1.0 × 1.47 u` (`15 × 22 px`). Starts run with `3` flame hearts (`MaxHearts = 3`).
- **Bubbles:** 4 tiers (`XL` diam `3.5 u` / peak `13 u`, `L` `2.5 u` / `11 u`, `M` `1.6 u` / `8.5 u`, `S` `0.9 u` / `6 u`) and 4 kinds (`Gloop` coral 2-split, `Trigloop` azure 3-split from Stage 2, `Lead` plum fast low-arc 2-split from Stage 3, `Lucky` lemon bonus +500 score).
- **Run Structure:** `6` procedural stages (`StageGenerator.Budget(stage) = 8 + 4 * (stage - 1)`).
- **Storm Timer:** `60 s + 6 s * (stage - 1)`. Warning blinks lemon in last `10 s`. At `0:00`, `STORM!` starts: bubbles move at `1.6×` speed and deal `2` hearts of damage.
- **Upgrade Pick & Cable-Fusion Matrix (Between Stages 1–5):**
  - On stage clear, player drafts **1 of 3** cards (`State.Pick`).
  - **Structural Cards (3):**
    1. `Twin Cable` (`Max = 2`): +1 simultaneous harpoon on screen (`r.Shots++`).
    2. `Anchor Tip` (`Max = 1`): Harpoon tips hook into the ceiling (`y = 14`) and hold their vertical cable taut for `1.5 s` (`r.CableHold = 1.5f`).
    3. `Wide Barbs` (`Max = 1`): Harpoon tip and cable hitboxes are `2×` wider (`r.HarpoonWidthMultiplier = 2f`).
  - **Payload Cards (3):**
    4. `Spark Wire` (`Max = 2`): Popping any bubble discharges a lightning bolt to the nearest bubble within `6.5 u` (+`2.5 u` per extra stack), popping it after a `0.06 s` electric arc flash (`r.SparkRank++`).
    5. `Cross-Stitch` (`Max = 2`): Whenever a harpoon pops a bubble or hooks the ceiling, it fires `2` horizontal steel needle shards (`left` and `right` at `22 u/s`, range `5.5 u` + `2.5 u` per extra stack) that pop bubbles in their path (`r.NeedleRank++`).
    6. `Alkahest` (`Max = 2`): Every popped bubble spawns a rising mint/lemon ember orb (`radius = 0.55 u`, rises at `2.2 u/s` for `1.6 s` + `0.6 s` per stack) that pops the first bubble touching it (`r.EmberRank++`).
  - **Utility / Survival Cards (2):**
    7. `Swift Boots` (`Max = 2`): Run `15%` faster and stay invulnerable `+0.6 s` longer after a hit.
    8. `Relight` (`Max = 0`, offered only when `Hearts < MaxHearts`): Restore `+1` lost flame heart immediately (and if at `MaxHearts - 1` with `MaxHearts < 4`, grants `+1` heart).
  - **Automatic 3×3 Pairwise Fusions (9 Named Synergies):**
    Whenever a run holds at least 1 rank of a **Structural Card** AND 1 rank of a **Payload Card**, that **Fusion** activates automatically and appears in `run.ActiveFusions()` and on the HUD/Pick screen:
    1. `Twin Cable` + `Anchor Tip` = **Tripwire Gate** *(Structural Pair Bonus)*: When 2 cables are simultaneously anchored (or airborne above `y = 6`), a horizontal plasma tripwire snaps between their tips (`y = min(tipA, tipB)`), popping any bubble that crosses the wire!
    2. `Twin Cable` + `Spark Wire` = **Arc Twin**: Firing a second harpoon while one is active snaps a chain-lightning arc between the two cables, zapping any bubble within `1.2 u` of the line connecting them.
    3. `Twin Cable` + `Cross-Stitch` = **Volley Loom**: Every harpoon fires a diagonal twin-needle burst on pop, doubling shard coverage.
    4. `Twin Cable` + `Alkahest` = **Twin Flasks**: Popping a bubble drops two counter-drifting Alkahest embers (left and right) instead of one.
    5. `Anchor Tip` + `Spark Wire` = **Tesla Rod**: Anchored ceiling cables pulse an electric shockwave every `0.45 s` while hooked, zapping any bubble within `2.4 u` of the cable.
    6. `Anchor Tip` + `Cross-Stitch` = **Ceiling Loom**: When a cable hooks the ceiling, it fires horizontal needle volleys both at ceiling height AND mid-arena (`y = 7 u`).
    7. `Anchor Tip` + `Alkahest` = **Acid Drip**: Anchored ceiling cables drip a falling Alkahest glob down the cable at `0.75 s`.
    8. `Wide Barbs` + `Spark Wire` = **Storm Lance**: Chain lightning range increases by `+3.5 u` and arcs can jump to a second bubble.
    9. `Wide Barbs` + `Cross-Stitch` = **Sawblade Barbs**: Horizontal needles become wide spinning saw-teeth (`2.5×` vertical thickness) that pierce through `2` bubbles instead of `1`.
    10. `Wide Barbs` + `Alkahest` = **Caustic Cloud**: Alkahest embers expand to `1.8×` radius (`1.0 u`).
  - **Card Preview Badge on `State.Pick`:** If an offered card on `State.Pick` will unlock a new Fusion with an already-owned card, the card draws a bright lemon/mint banner: `⚡ FUSES: <FUSION NAME>`.
  - **Reroll Hook on `State.Pick`:** Player gets `1` free Reroll per run (`[REROLL (1 FREE)]` button / `Tab` or `Y` key) + `1` Rewarded Ad Reroll (`[📺 REROLL]`) to prevent RNG bricking.

---

## 4. Controls & Feel (The 3-Second Muted Clip Hook)

- **Touch Controls:** Left half = relative drag pad; right half = hold-to-fire button (`TouchControlsView.cs`).
- **Control Side Flip Option:** Pause menu (`Game.Menus.cs`) includes a `FLIP TOUCH SIDES` toggle (`input.FlipTouchSides`), putting Move on the right and Fire on the left for players who prefer PC-style left-hand fire (`Bubble Trouble Classic`'s top review request).
- **Juice List for Muted 3-Second Clip:**
  - **Electric Arc (`Effects.Zap`):** Segmented 1 px jagged lemon/cream pixel bolt connecting pop origin to zapped target (or between two anchored cables for **Tripwire Gate**), visible for 3 frames with a high-pitched synth chirp.
  - **Horizontal Needle Shards (`Cross-Stitch`):** Crisp `5 × 3 px` steel/mint darts flying horizontally at `22 u/s` with pixel trail puffs.
  - **Alkahest Ember (`Alkahest`):** Glowing `9 × 9 px` mint/lemon bubbling orb rising slowly and bobbing horizontally (`sin` wave), bursting into a green/lemon ring when touched.
  - **Fusion Banner & Card Glow:** Cards that complete a fusion pulse with a lemon-mint border and show `FUSES: <NAME>`.

---

## 5. Content Pipeline & Automated Validator (Build First — M0)

- **`StageGenerator` + `StageValidator` (in `Run.cs` and `Tests/Editor/HarpoonAlchemyTests.cs`):**
  - Deterministic stage generation from `seed` and `stage` (`1..6`).
  - **Difficulty metric:** Total Pop Count required to clear the stage (`Gloop` tier `t` requires `2^(3-t) - 1` pops; `Trigloop` tier `t` requires `(3^(4-t) - 1) / 2` pops; `Lead` adds `1.35×` speed weight).
  - **Automated Validator checks:**
    1. Every stage `1..6` across `200` seeds spawns `1..maxStartBubbles` bubbles inside valid arena bounds (`x ∈ [-13.5, +13.5]`, `y + r ≤ 13.5`) and outside Wick's spawn safety zone (`|x| ≥ 3.5 u` at start).
    2. Monotonic difficulty: average weighted pop difficulty strictly rises from Stage 1 to Stage 6.
    3. Every upgrade in `Upgrade.All` has a unique, high-contrast `18 × 18 px` procedural icon (`UpgradeCardTests`) and every Structural × Payload pair produces a valid named `Fusion`.

---

## 6. Systems, in Build Order

1. **`Run.cs` — Upgrade Pool & Fusion Matrix (`FusionSpec`, `Upgrade.All`):** Replace pure-stat filler upgrades with `Spark Wire`, `Cross-Stitch`, and `Alkahest`, plus the 10 named synergies and `FusesWith(Upgrade candidate)` preview helper.
2. **`PlaceholderArt.cs` — New Upgrade Icons & Projectile Sprites:** Add crisp `18 × 18 px` icons for `Spark Wire`, `Cross-Stitch`, and `Alkahest` (passing all contrast and uniqueness unit tests) plus sprites for horizontal Needle Shards and Alkahest Embers.
3. **`Effects.cs` & `Game.cs` — Active Hazards & Chain Reactions (`ActiveShard`, `ActiveEmber`, `Tripwire`, `Zap`):** Implement horizontal needle projectiles, rising alkahest embers, chain-lightning zaps, Tesla Rod pulses, and the dual-cable **Tripwire Gate** wire between two anchored harpoons.
4. **`GameServices.cs` — Stubbed Monetization & Analytics Interfaces:** Clean 1-file interface + in-memory stub (`ShowRewarded(placement, onReward)`, `ShowInterstitial(placement)`, and the 8 required analytics events).
5. **`Game.cs` & `Game.Menus.cs` — Fusion UI Badges, Reroll Button, Relight Revive, & Flip Controls Toggle:**
   - On `State.Pick`: show `⚡ FUSES: <NAME>` on cards that complete a fusion, plus a clickable/keybindable `REROLL CARDS` button.
   - On `State.Over`: offer a 1-per-run `RELIGHT LAST HEART (AD)` button so players can test the rewarded revive loop.
   - On `Pause` menu: add `TOUCH SIDES: LEFT MOVE / RIGHT MOVE` toggle.
6. **`Tests/Editor/HarpoonAlchemyTests.cs` — Automated Verification Suite:** Unit tests for the 200-seed stage validator, difficulty curve, fusion matrix detection, reroll state, and icon readability.

---

## 7. Monetization Hooks (Stubbed Behind `GameServices.Ads`)

- `GameServices.Ads.ShowRewarded("relight_heart", onReward)` — Offered once per run on `State.Over` (`RELIGHT +1 HEART`). Restores 1 heart, grants `1.5 s` invulnerability, and resumes `State.Play` on the current stage.
- `GameServices.Ads.ShowRewarded("reroll_upgrades", onReward)` — Offered on `State.Pick` after the 1 free reroll is used. Rolls 3 fresh upgrade choices.
- `GameServices.Ads.ShowInterstitial("run_end")` — Called only when a run fully ends and the player restarts (`sessionTime >= 180f`), never mid-stage or behind active bubbles.

---

## 8. Analytics Events (Stubbed Behind `GameServices.Analytics`)

Minimum event contract logged via `GameServices.Analytics.Log(...)`:
- `session_start`
- `session_end` (`duration_s`)
- `level_start` (`stage`, `seed`, `build`)
- `level_complete` (`stage`, `time_s`, `hearts_left`, `fusions`)
- `level_fail` (`stage`, `time_s`, `storm_active`, `build`)
- `booster_used` (`type`: `"free_reroll"`, `"fusion_unlocked"`)
- `ad_rewarded_shown` (`placement`: `"relight_heart"` | `"reroll_upgrades"`)
- `ad_interstitial_shown` (`placement`: `"run_end"`)

---

## 9. Tech Constraints

- **Engine:** Unity `6000.6.7f1`, 2D URP (`PixelPerfectCamera` at `480 × 270`, `15 PPU`).
- **Target Platforms:** Mobile (Android + iOS, Landscape) and Desktop (macOS/Windows Steam + itch.io).
- **Performance Budget:** 60 FPS locked (`Application.targetFrameRate = 60`), zero GC allocations in per-frame `Bubble.Tick` and `Effects.Tick` loops, APK size < 35 MB.
- **Dependencies:** Zero new external packages.

---

## 10. Milestones with Acceptance Criteria

- **M0 — Content Validator & Fusion Matrix:** `StageGenerator` validated across 200 seeds (all stages 1–6 within bounds, spawn-safe, monotonically increasing pop difficulty) + all 8 upgrades and 10 fusions unit-tested.
- **M1 — Playable Cable-Fusion Loop:** `Spark Wire`, `Cross-Stitch`, `Alkahest`, and all pairwise fusions (including **Tripwire Gate** between 2 anchored cables and **Tesla Rod** pulses) work live in `State.Play`.
- **M2 — Fusion Pick UI, Reroll, Relight Revive, & Touch Side Flip:** `State.Pick` highlights `FUSES: <NAME>` on synergistic cards and lets the player Reroll; `State.Over` allows 1 Relight Revive per run; Pause menu allows flipping touch sides.
- **M3 — Verification & Fork Push:** All EditMode tests (`ReadabilityTests`, `UpgradeCardTests`, `HarpoonAlchemyTests`) pass with zero compile/linter errors, and changes are committed and pushed to `Kokanm/harpoon-alchemy`.

---

## 11. Verification

1. Run C# linter / compile diagnostics (`ReadLints`) across `Assets/Bubblewright/`.
2. Run EditMode test suite (`UpgradeCardTests`, `ReadabilityTests`, `HarpoonAlchemyTests`) verifying:
   - Every new upgrade icon (`Spark Wire`, `Cross-Stitch`, `Alkahest`) is unique, `18 × 18 px`, Point-filtered, and has ≥ 80% of drawn pixels at ≥ 3:1 contrast against `Palette.Ink`.
   - Every upgrade name is ≤ 12 chars and description ≤ 72 chars (`UpgradeCardTests.CardTextFits`).
   - 200-seed stage generation passes bounds, Wick spawn safety, and rising difficulty checks.
   - Pairwise fusions activate deterministically when their two parent upgrades are taken.

---

## 12. Open Questions (with Defaults Used)

- *Should fusions require a manual button press or activate automatically when both parent cards are owned?* → **Default used:** Automatic activation as soon as both cards are owned, with a glowing `FUSES: <NAME>` preview banner on the card before you pick it.
