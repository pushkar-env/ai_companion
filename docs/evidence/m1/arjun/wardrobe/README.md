# Arjun's modular wardrobe (2026-10-10)

The owner asked to make the characters modular so their outfits can be customised and swapped
while the overall look stays the same. The first new outfit is from the owner's reference
image (not stored in the repository):

- open light-blue chambray shirt over a white crew-neck tee;
- olive chinos;
- white sneakers;
- a steel watch.

It is for Arjun only for now; the female characters follow later. Male characters must never be
offered female outfits, and the other way round. ADR-075 records the decisions.

## What a player sees

In the app: **Style → Outfit → Chambray casual → Save look**. Style for Arjun offers:

| Picker | Choices |
|---|---|
| Outfit | Signature look (default), Chambray casual, Mix & match |
| Top | Brown shirt, Chambray shirt & white tee |
| Bottom | Grey trousers, Olive chinos |
| Accessory | None, Steel watch |
| Colours | Top, bottom, hair tint, sneaker colour and skin tone (as before) |

Shoes are not a picker: both outfits use the same white sneakers. Any top can be combined with
any bottom ("Mix & match"). The look is saved on the device per character. Meera, Tara and Alita
are unchanged.

## How it is built

- **Base body plus garments.** Arjun's single Tripo mesh was split into a base body (skin, hair,
  eyes, mouth and the full face rig) and garments. Each garment is its own skinned mesh on the
  same skeleton (`Imported/Arjun/Wardrobe/*.fbx`).
- **Signature look unchanged.** The original shirt, trousers and sneakers became the signature
  garments with their original texture. The signature render matches the earlier one with a mean
  pixel difference of 0.1.
- **Chambray shirt.** Made from the original shirt:
  - the folded button strip was removed;
  - both panels were opened along a smoothed shirt shell;
  - the hem was straightened and the open edges given a folded fabric edge;
  - white buttons sit on the wearer's right panel, painted buttonholes on the left.
  - The tee is a new lofted surface, tucked into the waistband, with a ribbed crew neck. It is
    held 3.5 mm off the skin and 3 mm inside the shirt.
- **Trousers.** One fitted mesh serves both trousers. It gained a real waist (4 cm waistband,
  folded top edge, belt loops, J-stitched fly, slant pockets and a tab button) so the open shirt
  shows a proper waistband. The grey version is the original texture re-baked; olive recolours
  it and keeps the creases and shading.
- **Watch.** Rigid on the left forearm, 34 mm case, dark dial, link bracelet.
- **Textures.** Procedural fabric:
  - chambray with slub streaks, weft flecks, the original shirt's fold shading, worn edges and
    topstitching;
  - cotton tee and buttons share the chambray atlas;
  - olive twill;
  - steel watch. Atlases are 2048 px (watch 512 px).
- **Physics.** Each shirt owns its spring chains:
  - brown shirt: 8 hem chains;
  - chambray: 2 open-edge chains of 3 bones plus 5 hem chains.
  - The import grafts these bones under the body bones. Chains of a garment that is not worn
    are paused.
- **Category rule.** Arjun's profile is Male with the fitted body `arjun`. A garment is worn or
  offered only when its category and fitted body match. Anything else is hidden, never listed,
  and refused if equipped directly. A body without a profile never wears modular garments.

## Owner review fixes (2026-10-10)

The owner reported two faults with Chambray casual: the shirt colour showed on Arjun's neck, and
the new shirt stretched unnaturally near the armpits when he raised his arms.

- **Neck.** The split had left shadowed neck skin inside both shirts. The brown shirt kept the
  original texture there, so it still looked like skin; the chambray copy painted it blue.
  - Those faces are now part of the base body again (from the pre-split mesh, with their weights and
    face-shape data), so every outfit shows skin there.
  - The tee's neckline was lifted where the restored skin came through it.
  - The chambray collar's saw-tooth back edge, the torn end of the right lapel and loose flaps at the
    collar sides were cleaned up.
- **Armpits.**
  - Each shirt has a half-rotation share bone at each shoulder that the app turns by half the
    upper arm's rotation.
  - The underarm is re-weighted across the fold: chest, share bone, arm. Before, the side panel
    followed the upper arm 10–15 cm below the armpit, so raised arms pulled the whole side into a
    web from the elbow to the hem.
  - Both shirts got this. The idle (arms down) look is unchanged.

Close-ups (Unity review renders, isolated copy):

- `signature-neck.png`, `chambray-casual-neck.png`: front three-quarter of the neck.
- `*-armpit-level.png`: the yawn as the right arm passes horizontal (the owner's screenshot).
- `*-armpit-raised.png`: yawn peak.
- `*-armpit-stretch.png`: side-stretch peak, three-quarter view.

## Checks

- **`../rig-checks.txt`: 132 PASS.** Includes 25 wardrobe checks:
  - garments bound;
  - the default signature look;
  - both presets;
  - only the worn shirt's chains run (34 of 50 joints);
  - chambray chains swing up to 1.7 cm while the stored shirt's chains rest;
  - chains stay outside the colliders;
  - the watch stays on the forearm through gestures (0.0 mm drift);
  - mix & match, and the accessory slot can be emptied;
  - unknown ids are refused;
  - 50 switches create no materials or objects;
  - tints reach only the worn shirt and chinos;
  - dispose restores the scene;
  - a female garment on Arjun is hidden and refused, and a male garment on a female body is
    hidden;
  - occlusion masks hide only while worn;
  - the shoulder share joints turn half the upper arm (0.00° error);
  - skin, not a shirt, is in front of the neck: 26 of 26 rays per outfit;
  - the shirt's side below the armpit stays within 3 cm of the chest as the arms rise (yawn
    1.1–1.2 cm, side stretch 2.1 cm; the old weights measured about 5 cm in Blender).
- **`../app-checks.txt`: 32 PASS (Play).** Style lists only the two outfits and Arjun's male
  garments. Choosing Chambray casual dresses him, only its springs run, Save look keeps it, and
  reopening Style shows it. The check starts from the default look whatever the device saved and
  restores the saved look afterwards.
- **Regressions, all passing:**
  - Meera: rig 105, in-app 27.
  - Tara: rig 106, in-app 27.
  - Face performance: 68.
  - Body idle: 34.
  - Alita: wardrobe UI 8, skin-tone material 13, skin-tone UI 24.
- **Blender pose tests:**
  - No tee poke-through when relaxed, with arms raised (at most 1.8 mm at the armpits) or
    twisting one way.
  - One point crosses by 9 mm in an extreme combined spine bend and waist twist.

## Files here

- `signature-{front,three-quarter,side,back,chest}.png` and
  `chambray-casual-{front,three-quarter,side,back,chest}.png`: Unity review renders (isolated copy).
- `chambray-casual-gesture-{Yawn,SideStretch,HipTurn}.png`: the open shirt through the app's gestures.
- `mix-front.png`: brown shirt with olive chinos.
- `{signature,chambray-casual}-neck.png` and `-armpit-{level,raised,stretch}.png`: the review
  close-ups (see above).
- `blender/01-modular-parts.png` to `blender/04-texture-atlases.png`: the split, the details, the
  rebuilt waistband and arms-raised test, and the garment atlases.
- `blender/05-review-fixes.png`: neck and raised arms before and after the owner review fixes.
- In the app: `../app-arjun-wardrobe.png` (Style panel), `../app-arjun-chambray-casual-wardrobe.png`,
  `../app-arjun-chambray-casual.png` and `../app-arjun-new-chat.png`.

## Limits

- **Sleeves:** the chambray sleeves stay rolled just below the elbow like the signature shirt;
  the reference shows them rolled a little lower.
- **Textures:** the fabric textures are procedural, and the open shirt is derived from the
  original closed shirt rather than modelled from scratch.
- **Edge marks:** a small mark remains on the pocket-side edge where an old button sat; a
  painted buttonhole covers it.
- **Collar sides:** small uneven bits remain where the collar meets the neck at each side, mostly
  hidden by the hair and ears from the front.
- **Arm level:** with the arm straight out, a soft fold of fabric still hangs under the arm. It
  comes from the T-pose shirt. It no longer pulls the side panel, but the shape was not remodelled.
- **Back crease:** a small crease appears near the right shoulder blade with both arms fully
  raised (present before the fix too).
- **Loading:** every garment loads with Arjun; nothing is loaded on demand yet (WARD-01/ASSET-01).
- **Device testing:** there is no device or performance evidence.
