# AI Companion — local development prototypes

Current progress and handoff: [STATUS](docs/STATUS.md), [MEMORY](docs/MEMORY.md),
[decisions](docs/DECISIONS.md). Independent M2 database foundations can be checked locally
with `python tools/check-database.py`; see [database setup](docs/runbooks/DATABASE.md).
This does not enable saved history in the Unity prototype.

**Clone on another machine:** install Git LFS, run `git lfs install`, then
`git clone https://github.com/pushkar-env/ai_companion.git` and `git lfs pull` inside
the clone. Source character exports, imported FBX/textures and evidence images are
included; keep all committed `.meta` files to preserve Unity GUIDs. Open `apps/unity/`
with Unity 6000.5.9f1 and allow a fresh import. Unity caches, generated builds, secrets
and machine-specific service configuration are intentionally regenerated locally.
For the talking scene, also install Node 24.12.0 and Ollama with `qwen2.5:7b`, and make
the Microsoft Zira Desktop and David Desktop voices available in Windows; see the linked setup guide below.

**Playable talking character:** choose **Companion → Open Talking Companion**, then
**Play**. Type a message or click a suggested prompt. The supplied CC character speaks
real local Ollama replies with mouth animation, blinking and facial expressions.
Windows Editor/English speech only; no cloud account or paid calls. See
[setup and testing](docs/runbooks/TALKING_COMPANION.md). Portrait mode and Editor layout
are preserved. Android builds are deferred until this Editor experience is established.

The original M0 scene below remains a labeled offline mock with fixed replies and a
geometric character. Neither prototype provides production accounts, billing or memory.

## Open and test Unity

1. In **Unity Hub → Projects → Add project from disk**, select
   `D:\Unity\ai_companion\apps\unity` (not the repository root).
2. Open with the existing **Unity 6000.5.9f1**. Allow package import/compilation to finish.
   Keep the existing package manifest and lockfile; no upgrades are needed for M0.
3. Choose **Companion → Open M0 Mock Scene**, or open
   `Assets/Companion/Scenes/MockCompanion.unity` in the Project window.
4. Choose **Companion → Portrait Preview (390 x 844)**, then press **Play** and use the
   **Game** view. The app is portrait-first: character above chat, composer and actions below.
   Mobile startup is locked to portrait. The preview menu changes only the Editor Game view.
5. Type `Can we plan a relaxing evening?` and select **Send** (Enter also sends;
   Shift+Enter adds a newline). Observe loading, streaming, and completion.
6. Open **Demo controls**, select **Slow loading**, then **Close controls**, send and **Cancel**. Partial responses remain marked
   cancelled. **Retry** starts a fresh attempt in the same bubble (maximum two retries).
7. Test **Error before response** and **Disconnect midway**. Each fails on its first
   attempt and succeeds after **Retry**. **Replay** starts a new normal mock turn.
8. Under **Demo controls**, **Clear session** cancels work and removes local text. Stopping
   Play Mode also clears the session. Try **Reduce character motion** and **Larger chat text**.
9. Optional: while playing, choose **Companion → Run M0 Play Mode Checks**. In roughly
   20 seconds it exercises 11 cases and writes `docs/evidence/unity-smoke.txt` plus a screenshot.
10. Run **Companion → Run Portrait Layout Checks** separately (not concurrently with the
    smoke check). It exercises 360×640, 390×844 and 1080×1920, simulated safe-area/keyboard
    insets and larger text/long messages. Evidence goes to `docs/evidence/portrait/`.
    Restart Play Mode afterward to clear the simulated insets and test content.

The existing SampleScene and build settings are preserved. The mock scene is deliberately
not added to the release build list. Development builds can include it; release builds
containing it are rejected by the mock guard. Mobile builds are not yet validated.

## Local tools and checks

Unity demo playback needs only the Unity Editor. For repository tests install the pinned
**.NET SDK 10.0.301**, **Python 3.14.2**, **Node 24.12.0**, Git and PowerShell 7.
The first bootstrap downloads ordinary pinned dev packages from their package registries;
after installation the demo and tests run locally with no provider credentials.

From `D:\Unity\ai_companion`:

```powershell
./tools/bootstrap.ps1
./tools/test.ps1
```

The test entrypoint fails on a failing check and covers generated drift, C#/TS contracts,
JSON Schema formats and negative fixtures, local HTTP/SSE, original asset integrity,
metadata/GUID uniqueness and a basic secret-pattern check. Play Mode/device/hosted CI
checks are reported separately, never folded into a false “all tests passed”.

Current host limitation: Windows Application Control rejected the rebuilt optional API
DLL. The suite reports that as **blocked** (exit 77); use normal Windows/IT trust approval
before rerunning. The Unity demo is independent and its Play Mode checks have passed.

Optional API simulator (the Unity scene does **not** depend on or call it):

```powershell
dotnet run --project services/api/Companion.MockApi.csproj
```

It binds only `127.0.0.1:8080`, exposes `/health` and four mock text routes documented in
`packages/contracts/openapi.json`. No auth/persistence: synthetic local development only.
Stop it with Ctrl+C before running the test suite (which starts its own API on that port).
The local executable reads APP_ENV/MOCK_EXTERNAL_SERVICES from process environment;
`.env` is a future server configuration template, not automatically loaded into Unity/API.
Production mode is rejected. Restarting the simulator clears its own in-memory fixtures.

## Scope and next work

### CC character test scene

Open the existing `apps/unity/` project in Unity **6000.5.9f1**. Stop Play Mode if needed,
choose **Companion → Open CC Character Test**, then press Play. Scene:
`Assets/Companion/Scenes/CCCharacterTest.unity`.

- Try **Blink**, **Smile**, **Frown**, **Open**.
- Try **Half blink** to inspect the exported eyelid corrective curves; these also follow
  individual blink sliders. This is a partial CC constraint implementation.
- Choose one of 29 native CC facial channels and drag **Weight**.
- **Sweep native channels** demonstrates each control at 70%.
- **Play synthetic tone + mouth cues** plays a short generated tone with 15 illustrative
  cues. This is not speech recognition, real speech or calibrated lip sync.
- **Interrupt / reset to neutral** immediately stops the tone, sweep and facial pose.
- After tone playback, scroll to the playback report to see the sample-clock duration
  captured before reset. It is a local synthetic observation, not proof of hearing.
- Toggle **Prototype jaw-bone assist** to compare morph-only and assisted opening.
- Adjust **Provisional jaw angle (degrees)** from 0–30, then use **Preview jaw bone
  only • 70%** to isolate skeletal motion with all facial morphs neutral. Angle changes
  stop playback and reset the face; **Restore jaw angle • 16°** restores the prototype
  default. This session-only control is not an exported CC bone conversion or saved
  production calibration. [Comparison checks](docs/evidence/m1/jaw-comparison/verification.md).
- Scroll the controls on short screens; reset stays fixed below them.

Run **Companion → Run CC Character Play Mode Checks** for the repeatable Editor checks.
Outside Play Mode, **Companion → Audit CC Mesh Deformation** measures 30 native controls
at three weights on a disposable import copy. [Calibration evidence](docs/evidence/m1/cc-calibration/verification.md)
includes per-mesh motion and neutral reset; this does not certify speech quality.
**Companion → Check CC Build Mesh Subset** verifies that the mobile diagnostic build
retains every facial control it uses with exact frame data. Build-only copies omit unused
shapes; the Editor keeps the complete rig. [Details](docs/evidence/m1/mesh-subset/verification.md).
For the current **Editor-only** workflow, change the existing Game pane's dropdown to
**Simulator** without moving or resizing the pane. Select **Punch Hole Center** or
**iOS Notch Device**, keep portrait orientation, and run the checks in Play Mode.
Both profiles passed 98 checks; see [Simulator evidence](docs/evidence/m1/simulator/verification.md).
Scene-opening menus preserve your view size. Portrait is enforced in mobile settings,
runtime initialization and a mobile build guard; native rotation remains unverified.
The test asset is not optimized for mobile, mappings are provisional and production
rights remain unconfirmed. Existing chat/synthetic scenes are separate and preserved.


### Synthetic M1 lab

The independent backend session simulator runs with `npm run check:voice` (41 session
and 22 worker/transport, 22 heard-response, 10 Unity report and 19 receipt assertions); `npm run check` also type-checks it. It covers session leases, cancellation,
stale frames and duplicate requests without a provider or microphone. See
[voice-session simulator](services/voice-agent/README.md). It is not yet wired to Unity.

In the same Unity project, stop Play Mode, choose **Companion → Open M1 Synthetic
Diagnostics**, then press Play. The scene is
`Assets/Companion/Scenes/FacialDiagnostics.unity`. Select a named channel and change
its weight, sweep all 52 pads, play the synthetic tone with 15 viseme cues, and interrupt.
Use **Companion → Run M1 Synthetic Play Mode Checks** to reproduce Editor checks.
This uses no microphone or external service. It is a diagnostic board, not avatar art
or speech-quality evidence. To return to chat, stop and open
`Assets/Companion/Scenes/MockCompanion.unity`.

Android development build and package checks passed after the owner freed space.
Latest APK: `artifacts/android-test/20260927-195903/output/Companion-CC-Test.apk` (171.8 MB).
Includes partial-blink correctives and jaw comparison controls; build policy retains
32 names including correctives. Build/package checks passed; APK execution is unverified.
[Build evidence](docs/evidence/m1/android/build-20260927-195903/verification.md).
Run `./tools/build-android-test.ps1` in PowerShell for an isolated local development
build (Python 3 and pinned Unity Android tools required). It verifies source-copy hashes
before launching Unity and saves `source-snapshot.json` beside the APK. It preserves
the interactive Editor's target and layout and does not install the APK.
Physical Galaxy S23 testing remains deferred by the owner. Follow the
[M1 matrix](docs/runbooks/M1.md) when device work resumes.

Approved product direction: adults-only, non-explicit, no clinical claims; India with
English and Hindi; original stylized adult avatar and a temporary neutral brand.
M0 UI/replies are English-only, explicitly mocked. Those approvals do not include legal
signoff, hosting/provider spend, retention, payments, third-party likeness/voice or release.

See [status/evidence](docs/STATUS.md), [decisions](docs/DECISIONS.md),
[open questions](docs/requirements/QUESTIONS.md), and [M1 preparation](docs/runbooks/M1.md).
