# Asset delivery, Addressables and wardrobe

## ASSET-01 — distribution and compatibility

Use Addressables with platform-specific bundles, immutable content hashes, staged remote catalogs and a CDN/object store. Addressables supports asynchronous local/remote asset loading; exact package compatibility must be pinned using the [Unity package reference](https://docs.unity.com/en-us/engine/6000.0/manual/packages-list/packages-all/pack-safe/com-unity-addressables). Ship a lightweight base avatar/outfit and critical UI locally. Remote delivery never downloads executable code or bypasses store platform rules.

Publish pipeline: validate licenses/rig/performance → build Android/iOS content → hash and malware/source checks → upload immutable bundles → run device preview → publish versioned manifest → canary catalog pointer → roll out. Keep previous compatible catalog and bundles for supported client versions. Never mutate an already published bundle URL. Catalog declares min/max client schema, platform, rig/body schema, hashes, size and dependencies. Rollback restores the pointer and valid outfit fallback, not an incompatible old binary.

Downloads show size/progress, allow cancellation, retry with bounded backoff, verify content and handle insufficient storage. Resume when supported; otherwise restart visibly. Cache uses a size-bounded LRU with pinned active/base assets and explicit reference counts. User can clear downloaded content without losing inventory. Handle partial/corrupt bundles, CDN failures and expired signed URLs; UI remains usable. CDN access control discourages unauthorized downloads but cannot guarantee DRM against an extracted app.

## WARD-01 — customization and ownership

Catalog item: ID, item type, rig families/body compatibility, slot, occlusion masks, material/color options, release/version, asset refs, product mappings and availability. Slots initially hair, top, bottom, shoes and accessory; expansion uses versioned compatibility rules. Full outfits may occupy multiple slots atomically. Skin here means an approved cosmetic appearance variant; distinguish body material, rig variant and clothing in data and UI.

Try-on is a local temporary preview. Server equip operation validates ownership, entitlement expiry, item availability and rig compatibility, then updates loadout with optimistic concurrency. Define revocation behavior: expired subscription items become unavailable and revert to a free compatible item; permanent purchases remain owned unless refunded/revoked by verified store state. Offline can display last validated cached outfit, but cannot mint entitlement or validate a new purchase.

Body occlusion masks prevent covered geometry showing through; garment test matrix covers all advertised body sliders and gesture extremes. Do not sell arbitrary body deformation before compatible garments exist. Store images and preview must match delivered appearance.

Acceptance: wrong-platform/hash/rig catalog is rejected with base fallback; update and rollback work without an app update within compatibility bounds; low-disk and interrupted download recover; 50 wardrobe switches return asset memory near baseline after cleanup; forged equip and stale version requests fail; advertised outfit combinations pass silhouette, clipping and animation review.
