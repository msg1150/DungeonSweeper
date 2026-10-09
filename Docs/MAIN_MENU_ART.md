# Main menu art and UI direction

Current illustration: `Assets/Art/MainMenu/MainMenuCatJanitor-v4.png`.
Assigned in `Assets/Resources/MainMenuPresentation.asset`.
Generated with the built-in imagegen tool on 2026-10-08. No third-party reference images were supplied; later edits reference earlier generated versions.

## Character and environment

- White bipedal janitor cat; ears alone have a faint cream tint; no hat or headgear.
- Mint work overalls, cream shirt, brown work shoes, flat floor mop.
- Urgent cleanup/loot-and-escape action, looking back at a pursuing monster.
- One defeated slime on the floor, no blood or gore.
- Moderately dark dungeon with a restrained glow near the left exit gate.
- UI stays bright: cream panels, mint buttons, dark readable text.
- Left side is reserved for title and New Game / Continue / Options / Quit buttons.

The illustration is key art, not a playable animation sheet. Matching player idle/walk/dash sprites, town buildings and dungeon world art are now separate applied assets; see [ART_DIRECTION.md](ART_DIRECTION.md).

## Integration

MainMenuPresentationConfig.backgroundImage holds an explicit texture reference, so the selected image is included in builds without a Resources lookup. backgroundDarkness is 0: the artwork already contains its lighting. Use texture imports without Read/Write or mipmaps; the menu uses ScaleAndCrop. Important action stays in the right-center safe region. Check cropping when choosing a new aspect ratio.

GameUiTheme provides cached rounded buttons and a bright skin used by GameGuiScope. Scope restores the original skin after each component draws. Dungeon HUD panels stay bright independently of the world/vision shading. Buttons retain the existing input, save, and focus behavior.

The main menu card now uses menuPanelOpacity = 0.56 and its buttons use menuButtonOpacity = 0.8 from MainMenuPresentationConfig. Only GUI background alpha is adjusted; title and button text keep full opacity. Other modal panels retain their existing opacity. Town has a compact translucent status panel and small facility tags visible only on approach; see ART_DIRECTION.md for animation and attack-frame corrections.

Earlier generated alternatives v1-v3 are kept for comparison; only v4 is referenced by the game. They are outside Resources and are not automatically bundled with builds.

## Verification

2026-10-08: 477 Unity runtime checks, 361 non-development Windows player checks, and 10 restart checks passed. Captured 14 game screens and inspected the main menu, town, dungeon HUD, market, warehouse, and loot placement at standard/minimum window sizes. Native player logs contained no runtime exceptions. The isolated editor logged an internal UnityEditor.Search.SearchDatabase indexing exception; it did not prevent compilation, runtime checks, or the player build. Profiling source and captures were preserved.

## Final generation prompt (built-in edit)

```text
Use case: lighting-weather.
Edit target: the provided DungeonSweeper main-menu illustration.
Primary request: change the environment from a sunlit golden dungeon to a moderately dark, enclosed dungeon that matches a stealth escape game. Keep the frantic fleeing janitor cat, the pursuing goblin, the mop and the ONE defeated slime EXACTLY in place with the same designs and poses. Darkness must come from scene lighting, not a flat black overlay.
Lighting changes: REMOVE the strong golden daylight, broad sunlight beams, bright yellow-white left wall and sunlit vines. The interior should be muted cool slate / blue-gray stone with deep desaturated teal shadows, damp stone barely lit, especially the rear RIGHT corridor. Keep just a faint cool ambient fill sufficient to read the scene, no daylight or sky.
EXIT GATE: subtly suggest an escape portal/gateway just off the LEFT edge, with a partial stone arch or narrow rim visible and a small, restrained pale mint / cool ivory magical glow. This is the principal light source; its light should stay local to the gate area and cast only a soft, narrow rim on the cat's front/left edge, mop and nearby wet floor. Do NOT make the entire left half brightly luminous. The left menu area should be subdued and low-detail, comparable to the darker stone elsewhere. Reduce existing torch/lantern glow to tiny very dim amber accents or extinguish them; no bright orange fire.
Preserve the white cat's readable silhouette and details with gentle cool fill: soft WHITE base fur, ears alone faint pale cream, green eyes, mint janitor overalls, cream shirt, brown shoes, uncovered head no hat; running forward on TWO feet, urgently looking back at the goblin, sweat bead and worried expression. Preserve the diagonal flat floor mop with rectangular cloth head. Preserve exactly ONE small collapsed green slime corpse beside the mop, no gore. Keep the goblin pursuer mostly in shadow in the right rear arch, eyes faintly visible.
Style: same polished original hand-painted 2D illustration, cute character, tense action, cozy art vocabulary with an appropriately dark dungeon. Moderately dark and readable, NOT pitch black, not horror.
Composition: unchanged wide 16:9 landscape; retain clean low-detail LEFT 42% for later UI, cat action and pursuer on the right. No UI, text, watermark, logo or signature.
```

## Action/identity prompt for the preceding reference

```text
Use case: precise-object-edit.
Asset type: main-menu key art for a bright but tense stealth-looting game, DungeonSweeper.
Reference role: retain the original character identity, outfit, fur colors, floor mop, painted style, and the stone dungeon setting, but dramatically CHANGE the pose, expression and action.
Primary request: This cat is a noncombatant janitor being CHASED by a monster while frantically clearing/collecting a defeated slime before escaping. It must feel like "grab the loot quickly and get out", visibly hurried, NOT leisurely cozy housekeeping.
Character identity: soft WHITE cat, only ear fur subtly pale buttery cream/yellow, green eyes, uncovered head, NO HAT, mint janitor overalls, cream short-sleeved shirt, brown work shoes, white fluffy tail. Anthropomorphic TWO-legged locomotion, two front paws used as hands. Hold a long-handled rectangular flat FLOOR MOP, never a straw broom.
Action/pose: full body in the RIGHT half, around x=68%. Dynamic crouched running/pivot pose, one foot firmly pushing off and the other stepping toward the viewer/exit, torso leaning forward, bent knees and animated lifted tail. Both paws urgently pulling the mop across the slick stone toward the exit; use strong diagonal lines in the handle and body. Head turned back toward the right rear corridor, eyes wide, brows worried/determined, small visible sweat bead, slightly open mouth. This is tense and expressive, not a smiling relaxed static portrait. A cloth pocket towel and tail trail with movement; small slime droplets near the mop communicate one frantic pass.
Dungeon story: EXACTLY ONE small collapsed mint-green SLIME CORPSE on the foreground floor adjacent to the mop, flattened and inactive, closed eyes, one glossy puddle, no blood or gore. Remove any neat dust/coin pile. In the far RIGHT archway add ONE approaching live goblin-like monster silhouette: hulking hunched shape, pointed ears, two watchful eyes, one outstretched hand reaching from the shadow. Readable pursuit and danger but subordinate and smaller than the cat, not touching it, not comic gore or horror. Keep a cleaning bucket off to the side, out of the running path.
Environment/style: retain bright warm sunlit ivory/sage stone foreground with pale golden light and the cooler, subtly darker teal corridor behind the cat. Polished original hand-painted 2D game illustration, charming rounded forms and soft textured shading. Overall welcoming bright palette with urgency created by pose/expression/action, NOT a dark horror scene. No generic motion-blur filter; keep the character and tool crisp.
Composition for actual menu: LEFT 42% must remain calm, pale low-detail negative space for a dark title and four buttons. Action, corpse and pursuer stay on the RIGHT. Preserve safe margins for modest widescreen crop. Full landscape 16:9. Do not draw UI, words, title, text, logos, watermarks or signatures. No extra cat, no additional slime or corpse.
```
