# Idea Evaluation: Bubblewright — Harpoon Alchemy (Cable-Fusion Pang)

- **Date:** 2026-11-09
- **Creator Profile:** `game-trend-profile.md` (Solo dev + coding agents · Unity 2D URP · $0–$100/mo · Mobile + Desktop first, Web later)
- **Existing Codebase Evaluated:** `Assets/Bubblewright/` (Steps 1–4 complete)

---

## 1. Verdict

**GO (33/35)** — *Bubblewright* already solves the #1 mobile failure mode of both classic *Pang* ports and *BALL x PIT* (clunky mobile touch aiming/movement) with its relative left-thumb drag + right-thumb hold-to-fire controls, and upgrading its 8 placeholder stat cards (`Run.cs`) into **visual 2-card cable fusions** unlocks a proven, rising Steam/mobile roguelite hook in **10–14 days of pure C# work**.

---

## 2. Core Loop

Drag anywhere on the left half of the screen to dodge kinematic bouncing bubbles and hold the right half to fire vertical steel harpoons → pop XL/L/M/S bubbles before the storm timer expires → draft and **fuse** two compatible upgrade cards between stages to trigger screen-clearing cable geometries and chain-pop reactions across a 6-stage permadeath run.

---

## 3. Scorecard (33 / 35)

*Hard Gates Check:*
- **Third-party IP:** PASS (Original character Wick, original code-painted palette and procedural sprites in `PlaceholderArt.cs`).
- **Within budget & skills:** PASS (100% C# code-drawn pixel art and synth audio; $0 external art cost).
- **Build time < trend window:** PASS (10–14 days to MVP vs. ≥6–12 month rising arcade-fusion roguelite window).
- **Visible 3-second muted twist:** PASS (Planting two harpoon cables that snap a horizontal electric wire or prism beam between them reads immediately in 3 seconds without audio).

| Criterion | Score | Evidence-Based Reason |
|---|:---:|---|
| **Simplicity** | **4/5** | Single 32×14 arena scene and kinematic bounce loop already run at 60 FPS in `Game.cs` and `Bubble.cs`; adding 6 base modifiers + 6 pairwise fusions and a card-swap toggle takes ~10–14 days in C#. |
| **Content cost** | **5/5** | `StageGenerator.Generate` (`Run.cs:122-160`) already builds infinite seeded stages via tier/kind budgets (`XL 8, L 4, M 2, S 1`), and deterministic trajectory math in `Bubble.Tick` allows automated headless solvability/time-to-clear tests. |
| **Virality** | **5/5** | Muted 3-second clip shows two vertical cables anchoring into the ceiling, snapping a horizontal laser/lightning wire between them, and slicing an XL Trigloop into 27 cascading pops; end-of-run screen generates a shareable seeded build card (`seed` already tracked in `Run.cs:18`). |
| **Monetization** | **4/5** | Fits both Desktop ($4.99–$6.99 Steam / free demo) and Mobile (F2P with 3 natural rewarded moments: **Relight 1 Heart**, **Reroll Upgrade Cards**, **Preview Fusion**, plus a $3.99–$4.99 Full/No-Ads Unlock or low-priced failure pack, matching 2025's +88% YoY hybrid-casual IAP growth `(AppMagic Monetization Report, 2025)`). |
| **Competition gap** | **5/5** | Zero roguelite *Pang* competitors exist on mobile or Steam; existing *Pang* ports have broken/cramped touch controls and static 20-level campaigns `(Gamezebo; Google Play Reviews, Oct–Nov 2026)`, while *BALL x PIT* players on mobile complain about sluggish touch aiming `(Reddit r/BALLxPIT, Mar 2026)`. |
| **Trend timing** | **5/5** | **Rising.** *BALL x PIT* hit 95% Overwhelmingly Positive across 15,830 Steam reviews (Oct 2025) and launched on iOS/Android in March 2026 `(Steam Store; Engadget, Mar 2026)`, while mobile Roguelike revenue grew +26.2% YoY to $371.9M `(AppMagic Monetization Report, 2025)`. |
| **Creator fit** | **5/5** | Plays directly to the creator's strongest assets: clean C# architecture, procedural pixel rendering (`PixelCanvas.cs`), custom kinematic physics, and chiptune synthesis (`Sfx.cs`). |
| **TOTAL** | **33/35** | **GO** (≥ 24/35 and no score of 1) |

### Existing Prototype Audit (Sunk-Cost Neutral)
- **Distance to shippable MVP:** **10–14 days** (or **5–7 days** if shipping the Daily Seed + Mutator slice first).
- **Reusable parts (90%+ carry-over):**
  - `Wick.cs` & `GameInput.cs` / `TouchControlsView.cs`: Relative left-thumb drag + right-thumb hold-to-fire, keyboard & gamepad support, squash/stretch/skid/celebrate animations.
  - `Bubble.cs` & `Tuning.cs`: Frame-rate-independent ballistic bounce math with fixed apex per tier (`XL 13u, L 11u, M 8.5u, S 6u`) and 4 bubble kinds (`Gloop`, `Trigloop`, `Lead`, `Lucky`).
  - `Run.cs` & `Game.cs`: Seeded RNG, stage budget generator, storm timer, hit-stop swell (`40 ms`), screen shake, contrast-tested UI (`Readability.cs` + unit tests).

---

## 4. Comparables (Live Store & Review Data)

| Title & Publisher | Launch & Platform | Chart / Traction | Monetization Model | Top Complaints from 1–3★ Reviews |
|---|---|---|---|---|
| **BALL x PIT** *(Kenny Sun / Devolver Digital)* | Steam: Oct 15, 2025 · iOS/Android: Mar 2026 | **Overwhelmingly Positive** on Steam (95% of 15,830 reviews; 90% recent) `(Steam Store, Nov 2026)` | Steam Premium · Mobile: Free Act 1 trial + **$9.99 full unlock** `(Engadget, Mar 2026)` | **1.** Mobile touchscreen aiming feels clunky/slow without a gamepad `(Reddit r/BALLxPIT, Mar 2026)`. **2.** Too many weak/bloated passives (50+) makes early picks 100% RNG-dependent; players want starter rerolls. **3.** Grindy town-building meta forces tedious building rearrangement between runs `(Steam Reviews, 2025–2026)`. |
| **Peglin** *(Red Nexus Games / IndieArk)* | Steam 1.0: Aug 2024 · iOS/Android: May 2023 | **Very Positive** on Steam (88% of 10,364 reviews); 400k+ PC copies `(Steam Store; Pocket Gamer)` | Steam Premium · Mobile: Free trial + **one-time full game IAP** | **1.** Bad orb/relic RNG can brick a run early with no agency to recover. **2.** Physics randomness sometimes causes frustrating unwinnable boards. |
| **Pang Adventures** *(PastaGames / DotEmu)* | Steam & iOS/Android: Apr 2016 | 4.2★ App Store; Very Positive Steam nostalgia niche `(App Store; Steam Community)` | Premium ($3.99–$9.99, no ads/IAP) | **1.** **Fixed small left/right mobile touch buttons** cause missed inputs and unfair deaths (`"If your finger misses these buttons... you freeze up"` — `Gamezebo; App Store 1★ reviews`). **2.** Short ~2-hour campaign with low replayability once beaten. **3.** Shooting only straight up with no dash/defensive options feels restrictive `(Steam Reviews)`. |
| **Bubble Trouble Classic** *(Kresimir Cvitanovic / One up)* | Android & iOS: Apr 2020 (Updated Oct 6, 2026) | 330k total Android installs, ~2.7k/mo recent, 3.95★ (870 ratings) `(Google Play / AppBrain, Oct 2026)` | Free with frequent interstitial/revive ads + Remove Ads IAP | **1.** **Intrusive ads between rounds** that play while the next round already started behind the ad, causing instant deaths `(App Store & Google Play 1★ reviews, 2025–2026)`. **2.** Broken revive button after watching an ad `(Google Play review, Aug 2026)`. **3.** Reversed/cramped default button placement; players asked for side-flipping & floating controls `(Google Play, Oct 2026)`. |

---

## 5. Gaps to Exploit

1. **Best-in-class 1D touch controls vs. 2D aiming frustration:** *BALL x PIT* struggles on touchscreens because players must simultaneously move and drag-aim 2D angles (`r/BALLxPIT, Mar 2026`). *Bubblewright* only moves horizontally on the floor (`y = 0`) and fires straight up (`+90°`), meaning its relative left-thumb slide + right-thumb hold is effortless on a phone while delivering the same ricochet/popping dopamine. Add a **Swap Left/Right Hands toggle** in the Pause menu (`Game.Menus.cs`) to also preempt *Bubble Trouble Classic*'s #1 control complaint.
2. **Small, high-impact Fusion Pool (No "50-passive trash RNG" or tedious town-rearranging):** *BALL x PIT*'s negative reviews center on 50+ diluted passives (where only 2–3 combos feel good), lack of an early reroll, and chore-like town building. *Bubblewright* should ship with **8–10 tight, visual upgrades where every pair fuses**, plus **1 free Reroll per run** (and 1 optional Rewarded Ad / Spark reroll) and **zero town-management bloat**.
3. **Respectful, non-breaking ad/unlock flow on mobile:** *Bubble Trouble Classic*'s 1-star reviews are dominated by ads firing while gameplay continues unpaused behind the overlay. *Bubblewright* already has a clean state machine (`State.Intro`, `State.Play`, `State.Clear`, `State.Pick`, `State.Over`); never trigger an ad during `State.Intro` or `State.Play`, and offer a clean $3.99–$4.99 one-time unlock.

---

## 6. Sharpen (Single Highest-Leverage Change to Current Prototype)

**Replace the 5 pure-math stat upgrades in `Run.cs` (`Upgrade.All`) with a 6×6 Pairwise Cable-Fusion System (`Harpoon Alchemy`) and show a live "FUSES INTO ⚡" banner on the upgrade card (`DrawPick` in `Game.cs`) whenever a offered card combines with one you already hold.**

Specifically:
- Keep **Twin Cable** (+1 harpoon), **Anchor Tip** (cables stay anchored in ceiling for 1.5 s), and **Wide Barbs** (2× hit width) as the 3 structural base cards.
- Replace the pure speed/invulnerability filler cards with 3 elemental/split payload cards:
  1. **Spark Wire** (cables hum with electricity; popping a bubble zaps the nearest S-bubble).
  2. **Cross-Stitch** (when a cable hits the ceiling or pops a bubble, it fires two short horizontal needle shards left and right).
  3. **Alkahest Drop** (popped children leave a 1.5 s rising acid/ember bubble that pops anything touching it).
- Define **explicit visual fusions** when a structural card meets a payload card (e.g., **Twin Cable + Anchor Tip = Tripwire Gate**; **Anchor Tip + Spark Wire = Lightning Rod** that pulses every 0.5 s while anchored; **Wide Barbs + Cross-Stitch = Sawblade Harpoon**).

---

## 7. Kill Switch (Metrics & Live-Sourced Thresholds)

Hand off to `tracking-game-metrics` once the 14-day MVP build is live on itch.io / Android test track / Steam demo:

| Metric | KILL / PIVOT Threshold | ITERATE Range | SCALE / GO Threshold | Benchmark Source |
|---|---|---|---|---|
| **D1 Retention (Mobile)** | **< 22%** (below global P50 median) | **22% – 34%** | **≥ 35%** (P75+ / Hybrid-casual target) | `InvestGame / GameAnalytics 2026 Mobile & PC Benchmarks (Jan 2026)` & `Turbine Hybrid-Casual Playbook (Apr 2025)` |
| **D7 Retention (Mobile)** | **< 7%** (below global P75) | **7% – 14%** | **≥ 15% – 20%** (Top performers hit 30%) | `InvestGame Benchmarks (Jan 2026)`, `Gamigion Hybrid-Casual Overview (2025)`, `Supersonic Hybrid Lessons (2025)` |
| **Daily Playtime / Session** | **< 6 min** (< 372s average session) | **6 – 12 min** (P50 median is 12 min/day) | **≥ 22 min/day** (P75 daily playtime) | `Sensor Tower Hybrid-Casual Playbook (2025)` & `InvestGame Benchmarks (Jan 2026)` |
| **Stage 1 Clear Rate** | **< 65%** (controls or XL bounce read is too punishing) | **65% – 80%** | **> 80%** with first death occurring around Stage 3–4 | Estimate (internal roguelike pacing target) |

---

## 8. Assumptions & Unknowns

- **Assumed:** All fusion visuals (horizontal arc wires, horizontal needle shards, rising ember bubbles) will be rendered via existing `PixelCanvas.cs` / `PlaceholderArt.cs` procedural pixel primitives so art production takes 0 external budget.
- **Assumed:** Mobile monetization will launch with a clean F2P + optional Rewarded Relight/Reroll + one-time $3.99–$4.99 Full Unlock (avoiding *Bubble Trouble Classic*'s forced between-round interstitial backlash).
- **Unknown (to test first):** Whether players prefer fusions to happen **automatically** as soon as they pick two compatible cards, or via a deliberate **"Fuse at the Anvil"** choice after Stage 3. (Default to **automatic fusion with a glowing badge preview on the card** for maximum simplicity).
